using System.Net;
using System.Net.Http.Json;
using AskYourData.Core.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace AskYourData.Tests.Integration;

/// <summary>
/// Integration tests for the Chatbot API.
/// These tests spin up the real ASP.NET Core host but do NOT require live external dependencies
/// (Qdrant, SQL Server, Ollama). They validate route wiring, request/response shapes,
/// and safety guardrails.
///
/// To run against live services, set environment variables:
///   INTEGRATION_TEST_LIVE=true  ASPNETCORE_ENVIRONMENT=Development
/// </summary>
public class ChatbotIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ChatbotIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    // ─── Status endpoint ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetStatus_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/chatbot/status");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetStatus_ResponseContainsExpectedFields()
    {
        var response = await _client.GetAsync("/api/chatbot/status");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("status", body, StringComparison.OrdinalIgnoreCase);
    }

    // ─── Databases endpoint ───────────────────────────────────────────────────

    [Fact]
    public async Task GetDatabases_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/chatbot/databases");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ─── Ask endpoint ─────────────────────────────────────────────────────────

    [Fact]
    public async Task PostAsk_EmptyQuestion_ReturnsBadRequestOrErrorResponse()
    {
        var request  = new ChatRequest { Question = "" };
        var response = await _client.PostAsJsonAsync("/api/chatbot/ask", request);

        // Either 400 or a ChatResponse with Success=false is acceptable
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var body = await response.Content.ReadFromJsonAsync<ChatResponse>();
            Assert.NotNull(body);
            Assert.False(body!.Success);
        }
        else
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public async Task PostAsk_ValidQuestion_ReturnsJsonChatResponse()
    {
        // This will fail if Ollama / Qdrant / SQL Server are not running.
        // That is expected in CI without live services — the test is skipped if env var not set.
        if (Environment.GetEnvironmentVariable("INTEGRATION_TEST_LIVE") != "true")
        {
            return; // graceful skip
        }

        var request  = new ChatRequest { Question = "আজকের total production কতো?" };
        var response = await _client.PostAsJsonAsync("/api/chatbot/ask", request);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<ChatResponse>();
        Assert.NotNull(body);
        Assert.NotNull(body!.Answer);
    }

    // ─── Ingest endpoint (auth guard) ────────────────────────────────────────

    [Fact]
    public async Task PostIngest_NoApiKey_ReturnsUnauthorized()
    {
        var response = await _client.PostAsync("/api/chatbot/ingest", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostIngest_WrongApiKey_ReturnsUnauthorized()
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/chatbot/ingest");
        req.Headers.Add("X-Api-Key", "wrong-key");
        var response = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
