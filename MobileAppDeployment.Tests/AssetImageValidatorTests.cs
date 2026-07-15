using MobileAppDeployment.Application.Validation;

namespace MobileAppDeployment.Tests;

/// <summary>
/// Unit tests for PNG/JPEG binary header detection used by asset validation.
/// </summary>
public class AssetImageValidatorTests
{
    /// <summary>
    /// Detects valid PNG dimensions from the IHDR chunk.
    /// </summary>
    [Fact]
    public void TryGetDimensions_ReturnsTrue_ForValidPngHeader()
    {
        byte[] pngBytes = CreateMinimalPng(512, 512);

        using var stream = new MemoryStream(pngBytes);
        bool result = ImageHeaderReader.TryGetDimensions(stream, out int width, out int height);

        Assert.True(result);
        Assert.Equal(512, width);
        Assert.Equal(512, height);
    }

    /// <summary>
    /// Detects valid JPEG dimensions from SOF markers.
    /// </summary>
    [Fact]
    public void TryGetDimensions_ReturnsTrue_ForValidJpegHeader()
    {
        byte[] jpegBytes = CreateMinimalJpeg(1024, 500);

        using var stream = new MemoryStream(jpegBytes);
        bool result = ImageHeaderReader.TryGetDimensions(stream, out int width, out int height);

        Assert.True(result);
        Assert.Equal(1024, width);
        Assert.Equal(500, height);
    }

    /// <summary>
    /// Rejects byte sequences that are not PNG or JPEG images.
    /// </summary>
    [Fact]
    public void TryGetDimensions_ReturnsFalse_ForInvalidBytes()
    {
        byte[] invalidBytes = "not-an-image"u8.ToArray();

        using var stream = new MemoryStream(invalidBytes);
        bool result = ImageHeaderReader.TryGetDimensions(stream, out int _, out int _);

        Assert.False(result);
    }

    private static byte[] CreateMinimalPng(int width, int height)
    {
        byte[] signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        byte[] ihdrLength = [0x00, 0x00, 0x00, 0x0D];
        byte[] ihdrType = "IHDR"u8.ToArray();
        byte[] dimensions =
        [
            (byte)(width >> 24), (byte)(width >> 16), (byte)(width >> 8), (byte)width,
            (byte)(height >> 24), (byte)(height >> 16), (byte)(height >> 8), (byte)height,
            0x08, 0x06, 0x00, 0x00, 0x00
        ];
        byte[] crc = [0x00, 0x00, 0x00, 0x00];

        return signature
            .Concat(ihdrLength)
            .Concat(ihdrType)
            .Concat(dimensions)
            .Concat(crc)
            .ToArray();
    }

    private static byte[] CreateMinimalJpeg(int width, int height)
    {
        return
        [
            0xFF, 0xD8,
            0xFF, 0xC0,
            0x00, 0x11,
            0x08,
            (byte)(height >> 8), (byte)height,
            (byte)(width >> 8), (byte)width,
            0x03, 0x01, 0x11, 0x00, 0x02, 0x11, 0x01, 0x03, 0x11, 0x01,
            0xFF, 0xD9
        ];
    }
}
