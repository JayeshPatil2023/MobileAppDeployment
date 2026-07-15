using Moq;
using MobileAppDeployment.Application.Services;
using MobileAppDeployment.Core.Domain.Entities;
using MobileAppDeployment.Core.Interfaces.Repositories;
using MobileAppDeployment.Core.Models.Responses;

namespace MobileAppDeployment.Tests;

/// <summary>
/// Unit tests for <see cref="FormAccessTokenService.IssueAsync"/>.
/// </summary>
public class FormAccessTokenServiceTests
{
    /// <summary>
    /// Issues a new token when no active token exists for the client.
    /// </summary>
    [Fact]
    public async Task IssueAsync_CreatesNewToken_WhenNoActiveTokenExists()
    {
        var repository = new Mock<IFormAccessTokenRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        repository
            .Setup(r => r.FindActiveByClientAsync("Acme", "My App", It.IsAny<CancellationToken>()))
            .ReturnsAsync((FormAccessToken?)null);

        repository
            .Setup(r => r.InsertAsync(It.IsAny<FormAccessToken>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new FormAccessTokenService(repository.Object, unitOfWork.Object);

        FormAccessTokenResponse response = await service.IssueAsync(
            "Acme",
            "My App",
            token => $"https://example.test/form/{token}");

        Assert.False(response.AlreadyExisted);
        Assert.False(string.IsNullOrWhiteSpace(response.Token));
        Assert.Equal("Acme", response.ClientName);
        Assert.Equal("My App", response.ClientAppName);
        Assert.StartsWith("https://example.test/form/", response.FormUrl, StringComparison.Ordinal);
        repository.Verify(r => r.InsertAsync(It.IsAny<FormAccessToken>(), It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Returns the existing active token for the same client/app pair.
    /// </summary>
    [Fact]
    public async Task IssueAsync_ReturnsExistingToken_WhenActiveTokenExists()
    {
        var existing = new FormAccessToken
        {
            Id = 7,
            Token = "existing-token",
            ClientName = "Acme",
            ClientAppName = "My App",
            IsActive = true
        };

        var repository = new Mock<IFormAccessTokenRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        repository
            .Setup(r => r.FindActiveByClientAsync("Acme", "My App", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var service = new FormAccessTokenService(repository.Object, unitOfWork.Object);

        FormAccessTokenResponse response = await service.IssueAsync(
            "Acme",
            "My App",
            token => $"https://example.test/form/{token}");

        Assert.True(response.AlreadyExisted);
        Assert.Equal("existing-token", response.Token);
        repository.Verify(r => r.InsertAsync(It.IsAny<FormAccessToken>(), It.IsAny<CancellationToken>()), Times.Never);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Creates a new token when the prior token for the client was revoked/inactive.
    /// </summary>
    [Fact]
    public async Task IssueAsync_CreatesNewToken_WhenOnlyRevokedTokenExists()
    {
        var repository = new Mock<IFormAccessTokenRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        repository
            .Setup(r => r.FindActiveByClientAsync("Acme", "My App", It.IsAny<CancellationToken>()))
            .ReturnsAsync((FormAccessToken?)null);

        repository
            .Setup(r => r.InsertAsync(It.IsAny<FormAccessToken>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new FormAccessTokenService(repository.Object, unitOfWork.Object);

        FormAccessTokenResponse response = await service.IssueAsync(
            " Acme ",
            " My App ",
            token => $"https://example.test/form/{token}");

        Assert.False(response.AlreadyExisted);
        repository.Verify(
            r => r.FindActiveByClientAsync("Acme", "My App", It.IsAny<CancellationToken>()),
            Times.Once);
        repository.Verify(r => r.InsertAsync(It.Is<FormAccessToken>(t => t.IsActive), It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
