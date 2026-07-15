using System.Net;
using System.Net.Http.Json;
using MobileAppDeployment.Core.Models.Requests;
using MobileAppDeployment.Core.Models.Responses;

namespace MobileAppDeployment.Tests;

/// <summary>
/// Integration tests for <c>POST /api/form-access-tokens</c>.
/// </summary>
public class FormAccessTokensApiTests : IClassFixture<MobileAppWebApplicationFactory>
{
    private readonly HttpClient _client;

    /// <summary>
    /// Creates the API integration test fixture.
    /// </summary>
    public FormAccessTokensApiTests(MobileAppWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    /// <summary>
    /// Returns 401 when the API key header is missing.
    /// </summary>
    [Fact]
    public async Task Post_WithoutApiKey_Returns401()
    {
        HttpResponseMessage response = await PostTokenRequestAsync(new CreateFormAccessTokenRequest
        {
            ClientName = "Acme",
            ClientAppName = "Portal"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Returns 401 when the API key header is wrong.
    /// </summary>
    [Fact]
    public async Task Post_WithWrongApiKey_Returns401()
    {
        using var request = BuildRequest(new CreateFormAccessTokenRequest
        {
            ClientName = "Acme",
            ClientAppName = "Portal"
        });
        request.Headers.Add("X-Api-Key", "wrong-key");

        HttpResponseMessage response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Returns 200 with token and form URL when the API key is valid.
    /// </summary>
    [Fact]
    public async Task Post_WithCorrectApiKey_Returns200WithTokenAndFormUrl()
    {
        HttpResponseMessage response = await PostTokenRequestAsync(
            new CreateFormAccessTokenRequest
            {
                ClientName = "Acme",
                ClientAppName = "Portal"
            },
            MobileAppWebApplicationFactory.TestApiKey);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        FormAccessTokenResponse? body = await response.Content.ReadFromJsonAsync<FormAccessTokenResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.Token));
        Assert.Contains("/AppDeployment/Form/", body.FormUrl, StringComparison.Ordinal);
    }

    /// <summary>
    /// Returns the same token when issuing twice for the same client/app pair.
    /// </summary>
    [Fact]
    public async Task Post_SecondRequestForSameClient_ReturnsSameToken()
    {
        var requestBody = new CreateFormAccessTokenRequest
        {
            ClientName = "Dedup Co",
            ClientAppName = "Dedup App"
        };

        HttpResponseMessage first = await PostTokenRequestAsync(requestBody, MobileAppWebApplicationFactory.TestApiKey);
        HttpResponseMessage second = await PostTokenRequestAsync(requestBody, MobileAppWebApplicationFactory.TestApiKey);

        FormAccessTokenResponse? firstBody = await first.Content.ReadFromJsonAsync<FormAccessTokenResponse>();
        FormAccessTokenResponse? secondBody = await second.Content.ReadFromJsonAsync<FormAccessTokenResponse>();

        Assert.NotNull(firstBody);
        Assert.NotNull(secondBody);
        Assert.Equal(firstBody.Token, secondBody.Token);
        Assert.True(secondBody.AlreadyExisted);
    }

    private async Task<HttpResponseMessage> PostTokenRequestAsync(
        CreateFormAccessTokenRequest body,
        string? apiKey = null)
    {
        HttpRequestMessage request = BuildRequest(body);
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            request.Headers.Add("X-Api-Key", apiKey);
        }

        return await _client.SendAsync(request);
    }

    private static HttpRequestMessage BuildRequest(CreateFormAccessTokenRequest body)
    {
        return new HttpRequestMessage(HttpMethod.Post, "/api/form-access-tokens")
        {
            Content = JsonContent.Create(body)
        };
    }
}
