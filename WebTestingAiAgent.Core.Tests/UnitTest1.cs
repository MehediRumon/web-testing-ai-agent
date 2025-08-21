using Xunit;
using WebTestingAiAgent.Core.Models;

namespace WebTestingAiAgent.Core.Tests;

public class AgentConfigTests
{
    [Fact]
    public void AgentConfig_DefaultValues_ShouldBeValid()
    {
        // Arrange & Act
        var config = new AgentConfig();

        // Assert
        Assert.False(config.Headless); // Default is false to show browser by default
        Assert.Equal(10000, config.ExplicitTimeoutMs);
        Assert.Equal(1, config.RetryPolicy.MaxStepRetries);
        Assert.Equal(500, config.RetryPolicy.RetryWaitMs);
        Assert.Equal(4, config.Parallel);
        Assert.Equal("./artifacts", config.ArtifactsPath);
        Assert.False(config.Evidence.Verbose);
        Assert.True(config.Evidence.CaptureConsole);
        Assert.True(config.Evidence.CaptureNetwork);
        Assert.Equal(2, config.Exploration.MaxDepth);
        Assert.Equal(600, config.Exploration.TimeBudgetSec);
        Assert.False(config.Security.AllowCrossOrigin);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(600)]
    [InlineData(3600)]
    public void AgentConfig_ValidTimeBudget_ShouldAccept(int timeBudget)
    {
        // Arrange & Act
        var config = new AgentConfig();
        config.Exploration.TimeBudgetSec = timeBudget;

        // Assert
        Assert.Equal(timeBudget, config.Exploration.TimeBudgetSec);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(5)]
    public void AgentConfig_ValidExplorationDepth_ShouldAccept(int depth)
    {
        // Arrange & Act
        var config = new AgentConfig();
        config.Exploration.MaxDepth = depth;

        // Assert
        Assert.Equal(depth, config.Exploration.MaxDepth);
    }
}