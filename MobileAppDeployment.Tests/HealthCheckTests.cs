using System.Net;

namespace MobileAppDeployment.Tests;

/// <summary>
/// Integration tests for the public health check endpoint.
/// </summary>
public class HealthCheckTests : IClassFixture<MobileAppWebApplicationFactory>
{
    private readonly HttpClient _client;

    /// <summary>
    /// Creates the health check integration test fixture.
    /// </summary>
    public HealthCheckTests(MobileAppWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    /// <summary>
    /// Returns HTTP 200 from <c>GET /health</c>.
    /// </summary>
    [Fact]
    public async Task GetHealth_Returns200()
    {
        HttpResponseMessage response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
