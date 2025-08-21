using Xunit;
using WebTestingAiAgent.Api.Services;
using WebTestingAiAgent.Core.Models;

namespace WebTestingAiAgent.Api.Tests;

public class ValidationServiceTests
{
    private readonly ValidationService _validationService;

    public ValidationServiceTests()
    {
        _validationService = new ValidationService();
    }

    [Fact]
    public async Task ValidateCreateRunRequestAsync_WithValidRequest_ShouldReturnNoErrors()
    {
        // Arrange
        var request = new CreateRunRequest
        {
            Objective = "Test login functionality",
            BaseUrl = "https://example.com",
            Config = new AgentConfig()
        };

        // Act
        var errors = await _validationService.ValidateCreateRunRequestAsync(request);

        // Assert
        Assert.Empty(errors);
    }

    [Fact]
    public async Task ValidateCreateRunRequestAsync_WithNullRequest_ShouldReturnErrors()
    {
        // Arrange
        CreateRunRequest request = null!;

        // Act
        var errors = await _validationService.ValidateCreateRunRequestAsync(request);

        // Assert
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Field == "request" && e.Message.Contains("cannot be null"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ValidateCreateRunRequestAsync_WithEmptyBaseUrl_ShouldReturnErrors(string baseUrl)
    {
        // Arrange
        var request = new CreateRunRequest
        {
            Objective = "Test functionality",
            BaseUrl = baseUrl
        };

        // Act
        var errors = await _validationService.ValidateCreateRunRequestAsync(request);

        // Assert
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Field == "BaseUrl" && e.Message.Contains("required"));
    }

    [Theory]
    [InlineData("invalid-url")]
    [InlineData("ftp://example.com")]
    public async Task ValidateBaseUrlAsync_WithInvalidUrl_ShouldReturnErrors(string baseUrl)
    {
        // Act
        var errors = await _validationService.ValidateBaseUrlAsync(baseUrl);

        // Assert
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Field == "baseUrl");
    }

    [Fact]
    public async Task ValidateBaseUrlAsync_WithEmptyUrl_ShouldReturnNoErrorsFromMethod()
    {
        // Empty URLs are handled by ValidateCreateRunRequestAsync, not ValidateBaseUrlAsync
        
        // Act
        var errors = await _validationService.ValidateBaseUrlAsync("");

        // Assert
        Assert.Empty(errors); // ValidateBaseUrlAsync doesn't validate empty strings
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://localhost:3000")]
    [InlineData("http://127.0.0.1:8080")]
    public async Task ValidateBaseUrlAsync_WithValidUrl_ShouldReturnNoErrors(string baseUrl)
    {
        // Act
        var errors = await _validationService.ValidateBaseUrlAsync(baseUrl);

        // Assert
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("Test")]
    [InlineData("1234")]
    public async Task ValidateObjectiveAsync_WithTooShortObjective_ShouldReturnErrors(string objective)
    {
        // Act
        var errors = await _validationService.ValidateObjectiveAsync(objective);

        // Assert
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Field == "objective" && e.Message.Contains("at least 5 characters"));
    }

    [Fact]
    public async Task ValidateObjectiveAsync_WithEmptyObjective_ShouldReturnNoErrors()
    {
        // Empty objectives are allowed (auto-generated), so ValidateObjectiveAsync returns no errors
        
        // Act
        var errors = await _validationService.ValidateObjectiveAsync("");

        // Assert
        Assert.Empty(errors); // ValidateObjectiveAsync doesn't validate empty strings
    }

    [Fact]
    public async Task ValidateObjectiveAsync_WithTooLongObjective_ShouldReturnErrors()
    {
        // Arrange
        var objective = new string('a', 4001); // Exceeds 4000 character limit

        // Act
        var errors = await _validationService.ValidateObjectiveAsync(objective);

        // Assert
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Field == "objective" && e.Message.Contains("must not exceed 4000 characters"));
    }

    [Theory]
    [InlineData("Test login functionality")]
    [InlineData("Verify user can successfully complete checkout process")]
    public async Task ValidateObjectiveAsync_WithValidObjective_ShouldReturnNoErrors(string objective)
    {
        // Act
        var errors = await _validationService.ValidateObjectiveAsync(objective);

        // Assert
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData(29)]
    [InlineData(3601)]
    public async Task ValidateConfigAsync_WithInvalidTimeBudget_ShouldReturnErrors(int timeBudget)
    {
        // Arrange
        var config = new AgentConfig();
        config.Exploration.TimeBudgetSec = timeBudget;

        // Act
        var errors = await _validationService.ValidateConfigAsync(config);

        // Assert
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Field == "config.exploration.timeBudgetSec");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(6)]
    public async Task ValidateConfigAsync_WithInvalidExplorationDepth_ShouldReturnErrors(int depth)
    {
        // Arrange
        var config = new AgentConfig();
        config.Exploration.MaxDepth = depth;

        // Act
        var errors = await _validationService.ValidateConfigAsync(config);

        // Assert
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Field == "config.exploration.maxDepth");
    }

    [Fact]
    public async Task ValidateConfigAsync_WithValidConfig_ShouldReturnNoErrors()
    {
        // Arrange
        var config = new AgentConfig();
        config.Exploration.TimeBudgetSec = 600;
        config.Exploration.MaxDepth = 2;

        // Act
        var errors = await _validationService.ValidateConfigAsync(config);

        // Assert
        Assert.Empty(errors);
    }
}