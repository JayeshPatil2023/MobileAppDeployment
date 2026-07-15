namespace MobileAppDeployment.Application.Validation;

/// <summary>
/// Reads width/height from PNG or JPEG binary headers without loading the full image.
/// </summary>
public static class ImageHeaderReader
{
    /// <summary>
    /// Attempts to read pixel dimensions from a PNG or JPEG stream.
    /// </summary>
    /// <param name="stream">Image stream positioned at the start of the file.</param>
    /// <param name="width">Detected width when successful.</param>
    /// <param name="height">Detected height when successful.</param>
    /// <returns><c>true</c> when dimensions were read successfully.</returns>
    public static bool TryGetDimensions(Stream stream, out int width, out int height)
    {
        width = 0;
        height = 0;

        if (!stream.CanRead)
        {
            return false;
        }

        long originalPosition = stream.CanSeek ? stream.Position : 0;
        try
        {
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            Span<byte> header = stackalloc byte[8];
            if (stream.Read(header) < 8)
            {
                return false;
            }

            // PNG signature: 89 50 4E 47 0D 0A 1A 0A
            if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
            {
                return TryReadPng(stream, out width, out height);
            }

            // JPEG starts with FF D8
            if (header[0] == 0xFF && header[1] == 0xD8)
            {
                if (stream.CanSeek)
                {
                    stream.Position = 0;
                }

                return TryReadJpeg(stream, out width, out height);
            }

            return false;
        }
        finally
        {
            if (stream.CanSeek)
            {
                stream.Position = originalPosition;
            }
        }
    }

    /// <summary>
    /// Reads width/height from the PNG IHDR chunk (immediately after the 8-byte signature).
    /// </summary>
    private static bool TryReadPng(Stream stream, out int width, out int height)
    {
        width = 0;
        height = 0;

        // After signature, next 8 bytes are length + "IHDR", then 4+4 width/height.
        Span<byte> ihdrPrefix = stackalloc byte[8];
        if (stream.Read(ihdrPrefix) < 8)
        {
            return false;
        }

        Span<byte> dimensions = stackalloc byte[8];
        if (stream.Read(dimensions) < 8)
        {
            return false;
        }

        width = ReadBigEndianInt32(dimensions);
        height = ReadBigEndianInt32(dimensions[4..]);
        return width > 0 && height > 0;
    }

    /// <summary>
    /// Scans JPEG markers until a Start-Of-Frame segment provides image size.
    /// </summary>
    private static bool TryReadJpeg(Stream stream, out int width, out int height)
    {
        width = 0;
        height = 0;

        // Skip SOI (FF D8) already verified by caller; re-read from start for clarity.
        int b1 = stream.ReadByte();
        int b2 = stream.ReadByte();
        if (b1 != 0xFF || b2 != 0xD8)
        {
            return false;
        }

        while (true)
        {
            int markerPrefix = stream.ReadByte();
            if (markerPrefix < 0)
            {
                return false;
            }

            if (markerPrefix != 0xFF)
            {
                continue;
            }

            int marker;
            do
            {
                marker = stream.ReadByte();
                if (marker < 0)
                {
                    return false;
                }
            } while (marker == 0xFF);

            // Standalone markers without length.
            if (marker == 0xD9 || marker == 0xDA)
            {
                return false;
            }

            int lengthHigh = stream.ReadByte();
            int lengthLow = stream.ReadByte();
            if (lengthHigh < 0 || lengthLow < 0)
            {
                return false;
            }

            int segmentLength = (lengthHigh << 8) | lengthLow;
            if (segmentLength < 2)
            {
                return false;
            }

            int payloadLength = segmentLength - 2;

            // SOF0–SOF3, SOF5–SOF7, SOF9–SOF11, SOF13–SOF15 carry dimensions.
            bool isStartOfFrame =
                (marker >= 0xC0 && marker <= 0xC3) ||
                (marker >= 0xC5 && marker <= 0xC7) ||
                (marker >= 0xC9 && marker <= 0xCB) ||
                (marker >= 0xCD && marker <= 0xCF);

            if (isStartOfFrame)
            {
                if (payloadLength < 5)
                {
                    return false;
                }

                _ = stream.ReadByte(); // sample precision
                int heightHigh = stream.ReadByte();
                int heightLow = stream.ReadByte();
                int widthHigh = stream.ReadByte();
                int widthLow = stream.ReadByte();
                if (heightHigh < 0 || heightLow < 0 || widthHigh < 0 || widthLow < 0)
                {
                    return false;
                }

                height = (heightHigh << 8) | heightLow;
                width = (widthHigh << 8) | widthLow;
                return width > 0 && height > 0;
            }

            if (stream.CanSeek)
            {
                stream.Position += payloadLength;
            }
            else
            {
                // Prefer heap buffer — stackalloc in a marker loop can overflow (CA2014).
                byte[] skipBuffer = new byte[Math.Min(payloadLength, 8192)];
                int remaining = payloadLength;
                while (remaining > 0)
                {
                    int toRead = Math.Min(remaining, skipBuffer.Length);
                    int read = stream.Read(skipBuffer, 0, toRead);
                    if (read <= 0)
                    {
                        return false;
                    }

                    remaining -= read;
                }
            }
        }
    }

    private static int ReadBigEndianInt32(ReadOnlySpan<byte> bytes) =>
        (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
}
