using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using System.Text.Json;
using Xunit;
using WebTestingAiAgent.Core.Models;

namespace WebTestingAiAgent.Api.Tests;

public class RunsControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public RunsControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task CreateRun_WithValidRequest_ShouldReturnRunId()
    {
        // Arrange
        var request = new CreateRunRequest
        {
            Objective = "Test login functionality",
            BaseUrl = "https://example.com",
            Config = new AgentConfig()
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/runs", content);

        // Assert
        response.EnsureSuccessStatusCode();
        
        var responseBody = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<CreateRunResponse>(responseBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(result);
        Assert.False(string.IsNullOrEmpty(result.RunId));
    }

    [Fact]
    public async Task CreateRun_WithMinimalRequest_ShouldReturnRunId()
    {
        // Arrange
        var request = new CreateRunRequest
        {
            BaseUrl = "https://example.com"
            // Objective is optional and can be auto-generated
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/runs", content);

        // Assert
        response.EnsureSuccessStatusCode();
        
        var responseBody = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<CreateRunResponse>(responseBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(result);
        Assert.False(string.IsNullOrEmpty(result.RunId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid-url")]
    [InlineData("ftp://example.com")]
    public async Task CreateRun_WithInvalidBaseUrl_ShouldReturnBadRequest(string baseUrl)
    {
        // Arrange
        var request = new CreateRunRequest
        {
            Objective = "Test functionality",
            BaseUrl = baseUrl
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/runs", content);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetRunStatus_WithValidRunId_ShouldReturnStatus()
    {
        // Arrange - First create a run
        var createRequest = new CreateRunRequest
        {
            Objective = "Test login functionality",
            BaseUrl = "https://example.com"
        };

        var json = JsonSerializer.Serialize(createRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        var createResponse = await _client.PostAsync("/api/runs", content);
        createResponse.EnsureSuccessStatusCode();
        
        var createResponseBody = await createResponse.Content.ReadAsStringAsync();
        var createResult = JsonSerializer.Deserialize<CreateRunResponse>(createResponseBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Act
        var statusResponse = await _client.GetAsync($"/api/runs/{createResult!.RunId}");

        // Assert
        statusResponse.EnsureSuccessStatusCode();
        
        var statusResponseBody = await statusResponse.Content.ReadAsStringAsync();
        var statusResult = JsonSerializer.Deserialize<RunStatus>(statusResponseBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(statusResult);
        Assert.Equal(createResult.RunId, statusResult.RunId);
        Assert.NotNull(statusResult.Status);
        Assert.True(statusResult.Progress >= 0 && statusResult.Progress <= 100);
    }

    [Fact]
    public async Task GetRunStatus_WithInvalidRunId_ShouldReturnNotFound()
    {
        // Arrange
        var invalidRunId = "non-existent-run-id";

        // Act
        var response = await _client.GetAsync($"/api/runs/{invalidRunId}");

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }
}