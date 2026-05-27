using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace OsrsTracker.Tests.Integration;

[Collection("Integration")]
public class ReadEndpointsTests
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public ReadEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        factory.ResetDatabase();
    }

    // ── GET /api/accounts ────────────────────────────────────────────────────

    [Fact]
    public async Task GetAccounts_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/accounts");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAccounts_ReturnsOwnedAccounts()
    {
        var token = await RegisterAndGetToken("list-user@example.com");
        SetToken(token);

        await AddAccountAsync("Zezima");

        var response = await _client.GetAsync("/api/accounts");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetArrayLength().Should().Be(1);
        body[0].GetProperty("osrsUsername").GetString().Should().Be("Zezima");
    }

    [Fact]
    public async Task GetAccounts_DoesNotReturnOtherUsersAccounts()
    {
        var tokenA = await RegisterAndGetToken("owner-a@example.com");
        var tokenB = await RegisterAndGetToken("owner-b@example.com");

        SetToken(tokenA);
        await AddAccountAsync("Zezima");

        SetToken(tokenB);
        var response = await _client.GetAsync("/api/accounts");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        body.GetArrayLength().Should().Be(0);
    }

    // ── DELETE /api/accounts/{id} ─────────────────────────────────────────────

    [Fact]
    public async Task DeleteAccount_RemovesAccount()
    {
        var token = await RegisterAndGetToken("delete-user@example.com");
        SetToken(token);

        var id = await AddAccountAsync("Zezima");
        var deleteResponse = await _client.DeleteAsync($"/api/accounts/{id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.GetAsync("/api/accounts");
        var body = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        body.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task DeleteAccount_OtherUsersAccount_ReturnsNotFound()
    {
        var tokenA = await RegisterAndGetToken("del-a@example.com");
        var tokenB = await RegisterAndGetToken("del-b@example.com");

        SetToken(tokenA);
        var id = await AddAccountAsync("Zezima");

        SetToken(tokenB);
        var response = await _client.DeleteAsync($"/api/accounts/{id}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── GET /api/accounts/{id}/skills ─────────────────────────────────────────

    [Fact]
    public async Task GetSkills_ReturnsAllSkillsWithSnapshots()
    {
        var token = await RegisterAndGetToken("skills-user@example.com");
        SetToken(token);

        var id = await AddAccountAsync("Zezima");

        var response = await _client.GetAsync($"/api/accounts/{id}/skills");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        // FakeHiscoresClient returns 24 entries — all skills should have snapshots
        body.GetArrayLength().Should().Be(24);
        body[0].GetProperty("xp").GetInt64().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetSkills_OtherUsersAccount_ReturnsNotFound()
    {
        var tokenA = await RegisterAndGetToken("skills-a@example.com");
        var tokenB = await RegisterAndGetToken("skills-b@example.com");

        SetToken(tokenA);
        var id = await AddAccountAsync("Zezima");

        SetToken(tokenB);
        var response = await _client.GetAsync($"/api/accounts/{id}/skills");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── GET /api/accounts/{id}/skills/{skillId}/history ──────────────────────

    [Fact]
    public async Task GetSkillHistory_ReturnsSnapshots()
    {
        var token = await RegisterAndGetToken("history-user@example.com");
        SetToken(token);

        var accountId = await AddAccountAsync("Zezima");

        // Fetch the first skillId from the skills endpoint
        var skillsResponse = await _client.GetAsync($"/api/accounts/{accountId}/skills");
        var skills = await skillsResponse.Content.ReadFromJsonAsync<JsonElement>();
        var skillId = skills[0].GetProperty("skillId").GetInt32();

        var response = await _client.GetAsync($"/api/accounts/{accountId}/skills/{skillId}/history?days=30");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var history = await response.Content.ReadFromJsonAsync<JsonElement>();
        history.GetArrayLength().Should().BeGreaterThan(0);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<string> RegisterAndGetToken(string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register",
            new { email, password = "password123" });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("token").GetString()!;
    }

    private async Task<int> AddAccountAsync(string username)
    {
        var response = await _client.PostAsJsonAsync("/api/accounts",
            new { osrsUsername = username, displayName = username });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetInt32();
    }

    private void SetToken(string token) =>
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
}
