using System.Diagnostics;
using System.Text.Json;
using Xunit;
using WebTestingAiAgent.Core.Models;

namespace WebTestingAiAgent.Cli.Tests;

public class CliIntegrationTests
{
    private readonly string _cliPath;

    public CliIntegrationTests()
    {
        _cliPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "WebTestingAiAgent.Cli", "bin", "Debug", "net8.0", "ai-agent.dll");
    }

    [Fact]
    public async Task Cli_Help_ShouldDisplayUsageInformation()
    {
        // Arrange & Act
        var result = await RunCliCommand("--help");

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Web Testing AI Agent CLI", result.Output);
        Assert.Contains("plan", result.Output);
        Assert.Contains("run", result.Output);
        Assert.Contains("report", result.Output);
    }

    [Fact]
    public async Task Cli_PlanHelp_ShouldDisplayPlanUsage()
    {
        // Arrange & Act
        var result = await RunCliCommand("plan --help");

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Create a test plan from objective", result.Output);
        Assert.Contains("--baseUrl", result.Output);
        Assert.Contains("--objective", result.Output);
        Assert.Contains("--out", result.Output);
    }

    [Fact]
    public async Task Cli_Plan_WithValidArguments_ShouldCreatePlanFile()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var tempJsonFile = Path.ChangeExtension(tempFile, ".json");

        try
        {
            // Act
            var result = await RunCliCommand($"plan --baseUrl https://example.com --objective \"Test functionality\" --out \"{tempJsonFile}\"");

            // Assert
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Plan saved to:", result.Output);
            Assert.True(File.Exists(tempJsonFile));

            // Verify the JSON structure
            var jsonContent = await File.ReadAllTextAsync(tempJsonFile);
            var plan = JsonSerializer.Deserialize<PlanJson>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            Assert.NotNull(plan);
            Assert.Equal("https://example.com", plan.BaseUrl);
            Assert.Equal("Test functionality", plan.Objective);
            Assert.NotEmpty(plan.Steps);
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempFile)) File.Delete(tempFile);
            if (File.Exists(tempJsonFile)) File.Delete(tempJsonFile);
        }
    }

    [Fact]
    public async Task Cli_Plan_WithoutBaseUrl_ShouldReturnError()
    {
        // Arrange & Act
        var result = await RunCliCommand("plan --objective \"Test functionality\"");

        // Assert
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("baseUrl", result.Output);
    }

    [Fact]
    public async Task Cli_Plan_WithAutoGeneration_ShouldCreatePlan()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var tempJsonFile = Path.ChangeExtension(tempFile, ".json");

        try
        {
            // Act - No objective provided, should auto-generate
            var result = await RunCliCommand($"plan --baseUrl https://example.com --out \"{tempJsonFile}\"");

            // Assert
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Plan saved to:", result.Output);
            Assert.True(File.Exists(tempJsonFile));

            // Verify the JSON structure
            var jsonContent = await File.ReadAllTextAsync(tempJsonFile);
            var plan = JsonSerializer.Deserialize<PlanJson>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            Assert.NotNull(plan);
            Assert.Equal("https://example.com", plan.BaseUrl);
            Assert.NotNull(plan.Objective); // Should be auto-generated
            Assert.NotEmpty(plan.Steps);
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempFile)) File.Delete(tempFile);
            if (File.Exists(tempJsonFile)) File.Delete(tempJsonFile);
        }
    }

    private async Task<(int ExitCode, string Output)> RunCliCommand(string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{_cliPath}\" {arguments}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        var allOutput = string.Join(Environment.NewLine, output, error).Trim();
        return (process.ExitCode, allOutput);
    }
}