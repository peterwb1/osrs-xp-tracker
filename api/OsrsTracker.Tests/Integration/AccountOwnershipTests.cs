using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace OsrsTracker.Tests.Integration;

[Collection("Integration")]
public class AccountOwnershipTests
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public AccountOwnershipTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        factory.ResetDatabase();
    }

    [Fact]
    public async Task PostAccount_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/accounts",
            new { osrsUsername = "Zezima", displayName = "Test" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostAccount_WithValidToken_ReturnsCreated()
    {
        var token = await RegisterAndGetToken("user1@example.com");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsJsonAsync("/api/accounts",
            new { osrsUsername = "Zezima", displayName = "Test" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task PostAccount_SameUsernameDifferentUsers_BothSucceed()
    {
        // Two users can both track the same OSRS account
        var tokenA = await RegisterAndGetToken("usera@example.com");
        var tokenB = await RegisterAndGetToken("userb@example.com");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        var responseA = await _client.PostAsJsonAsync("/api/accounts",
            new { osrsUsername = "Zezima", displayName = "A's account" });

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        var responseB = await _client.PostAsJsonAsync("/api/accounts",
            new { osrsUsername = "Zezima", displayName = "B's account" });

        responseA.StatusCode.Should().Be(HttpStatusCode.Created);
        responseB.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task PostAccount_SameUserSameUsername_ReturnsConflict()
    {
        var token = await RegisterAndGetToken("user2@example.com");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        await _client.PostAsJsonAsync("/api/accounts",
            new { osrsUsername = "Zezima", displayName = "First" });

        var response = await _client.PostAsJsonAsync("/api/accounts",
            new { osrsUsername = "Zezima", displayName = "Duplicate" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private async Task<string> RegisterAndGetToken(string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register",
            new { email, password = "password123" });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("token").GetString()!;
    }
}
