using Xunit;
using WebTestingAiAgent.Core.Models;

namespace WebTestingAiAgent.Core.Tests;

public class ApiModelsTests
{
    [Fact]
    public void CreateRunRequest_DefaultValues_ShouldBeEmpty()
    {
        // Arrange & Act
        var request = new CreateRunRequest();

        // Assert
        Assert.Equal(string.Empty, request.Objective);
        Assert.Equal(string.Empty, request.BaseUrl);
        Assert.Null(request.Config);
    }

    [Fact]
    public void CreateRunRequest_WithValues_ShouldStoreCorrectly()
    {
        // Arrange
        var objective = "Test login functionality";
        var baseUrl = "https://example.com";
        var config = new AgentConfig();

        // Act
        var request = new CreateRunRequest
        {
            Objective = objective,
            BaseUrl = baseUrl,
            Config = config
        };

        // Assert
        Assert.Equal(objective, request.Objective);
        Assert.Equal(baseUrl, request.BaseUrl);
        Assert.Equal(config, request.Config);
    }

    [Fact]
    public void CreateRunResponse_WithRunId_ShouldStoreCorrectly()
    {
        // Arrange
        var runId = "test-run-123";

        // Act
        var response = new CreateRunResponse { RunId = runId };

        // Assert
        Assert.Equal(runId, response.RunId);
    }

    [Fact]
    public void RunStatus_DefaultValues_ShouldBeValid()
    {
        // Arrange & Act
        var status = new RunStatus();

        // Assert
        Assert.Equal(string.Empty, status.RunId);
        Assert.Equal(string.Empty, status.Status);
        Assert.Equal(0, status.Progress);
        Assert.NotNull(status.PartialResults);
        Assert.Empty(status.PartialResults);
    }

    [Fact]
    public void ValidationError_WithValues_ShouldStoreCorrectly()
    {
        // Arrange
        var field = "baseUrl";
        var message = "Base URL is required";

        // Act
        var error = new ValidationError
        {
            Field = field,
            Message = message
        };

        // Assert
        Assert.Equal(field, error.Field);
        Assert.Equal(message, error.Message);
    }

    [Fact]
    public void ApiErrorResponse_WithErrors_ShouldStoreCorrectly()
    {
        // Arrange
        var message = "Validation failed";
        var errors = new List<ValidationError>
        {
            new ValidationError { Field = "baseUrl", Message = "Required" }
        };

        // Act
        var response = new ApiErrorResponse
        {
            Message = message,
            Errors = errors
        };

        // Assert
        Assert.Equal(message, response.Message);
        Assert.Equal(errors, response.Errors);
        Assert.Single(response.Errors);
    }
}