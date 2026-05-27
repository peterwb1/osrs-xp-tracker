using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace OsrsTracker.Tests.Integration;

[Collection("Integration")]
public class AuthIntegrationTests
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public AuthIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        factory.ResetDatabase();
    }

    [Fact]
    public async Task Register_WithValidCredentials_ReturnsToken()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register",
            new { email = "alice@example.com", password = "password123" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("token").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsBadRequest()
    {
        await _client.PostAsJsonAsync("/api/auth/register",
            new { email = "alice@example.com", password = "password123" });

        var response = await _client.PostAsJsonAsync("/api/auth/register",
            new { email = "alice@example.com", password = "differentpass" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_WithCorrectPassword_ReturnsToken()
    {
        await _client.PostAsJsonAsync("/api/auth/register",
            new { email = "bob@example.com", password = "password123" });

        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { email = "bob@example.com", password = "password123" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("token").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        await _client.PostAsJsonAsync("/api/auth/register",
            new { email = "carol@example.com", password = "password123" });

        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { email = "carol@example.com", password = "wrongpassword" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { email = "nobody@example.com", password = "password123" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_WithValidToken_ReturnsUserInfo()
    {
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register",
            new { email = "dave@example.com", password = "password123" });
        var body = await registerResponse.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("token").GetString()!;

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var meResponse = await _client.GetAsync("/api/auth/me");

        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var meBody = await meResponse.Content.ReadFromJsonAsync<JsonElement>();
        meBody.GetProperty("email").GetString().Should().Be("dave@example.com");

        _client.DefaultRequestHeaders.Authorization = null;
    }
}
