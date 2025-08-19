using System.CommandLine;
using System.Text.Json;
using WebTestingAiAgent.Core.Models;

namespace WebTestingAiAgent.Cli;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Web Testing AI Agent CLI");

        // ai-agent plan command
        var planCommand = new Command("plan", "Create a test plan from objective");
        var objectiveOption = new Option<string>("--objective", "Natural language test objective (optional - will auto-generate if not provided)")
        {
            IsRequired = false
        };
        var baseUrlOption = new Option<string>("--baseUrl", "Base URL of the application to test")
        {
            IsRequired = true
        };
        var configOption = new Option<FileInfo?>("--config", "Path to configuration file");
        var outputOption = new Option<FileInfo>("--out", "Output file for the plan JSON");

        planCommand.AddOption(objectiveOption);
        planCommand.AddOption(baseUrlOption);
        planCommand.AddOption(configOption);
        planCommand.AddOption(outputOption);

        planCommand.SetHandler(async (string? objective, string baseUrl, FileInfo? config, FileInfo output) =>
        {
            await HandlePlanCommand(objective, baseUrl, config, output);
        }, objectiveOption, baseUrlOption, configOption, outputOption);

        // ai-agent run command
        var runCommand = new Command("run", "Execute a test plan");
        var planFileOption = new Option<FileInfo>("--plan", "Path to plan JSON file")
        {
            IsRequired = true
        };
        var configRunOption = new Option<FileInfo?>("--config", "Path to configuration file");
        var parallelOption = new Option<int>("--parallel", () => 4, "Number of parallel workers");

        runCommand.AddOption(planFileOption);
        runCommand.AddOption(configRunOption);
        runCommand.AddOption(parallelOption);

        runCommand.SetHandler(async (FileInfo plan, FileInfo? config, int parallel) =>
        {
            await HandleRunCommand(plan, config, parallel);
        }, planFileOption, configRunOption, parallelOption);

        // ai-agent report command
        var reportCommand = new Command("report", "Generate or view test reports");
        var runIdOption = new Option<string>("--runId", "Run ID to generate report for")
        {
            IsRequired = true
        };
        var formatOption = new Option<string>("--format", () => "html", "Report format (html, json, junit)");
        var openOption = new Option<bool>("--open", "Open the report in default browser");

        reportCommand.AddOption(runIdOption);
        reportCommand.AddOption(formatOption);
        reportCommand.AddOption(openOption);

        reportCommand.SetHandler(async (string runId, string format, bool open) =>
        {
            await HandleReportCommand(runId, format, open);
        }, runIdOption, formatOption, openOption);

        rootCommand.AddCommand(planCommand);
        rootCommand.AddCommand(runCommand);
        rootCommand.AddCommand(reportCommand);

        return await rootCommand.InvokeAsync(args);
    }

    static async Task HandlePlanCommand(string? objective, string baseUrl, FileInfo? configFile, FileInfo outputFile)
    {
        try
        {
            // Use PlannerService for enhanced plan generation
            objective = objective ?? "Automatically test basic functionality of the web application";
            Console.WriteLine($"Creating plan for objective: {objective}");
            Console.WriteLine($"Base URL: {baseUrl}");

            // Load config if provided
            var config = new AgentConfig();
            if (configFile != null && configFile.Exists)
            {
                var configJson = await File.ReadAllTextAsync(configFile.FullName);
                config = JsonSerializer.Deserialize<AgentConfig>(configJson) ?? new AgentConfig();
            }

            // Use PlannerService to create enhanced plan with auto-discovery
            var plannerService = new WebTestingAiAgent.Api.Services.PlannerService();
            var plan = await plannerService.CreatePlanAsync(objective, baseUrl, config);

            // Save plan to file
            var planJson = JsonSerializer.Serialize(plan, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await File.WriteAllTextAsync(outputFile.FullName, planJson);
            Console.WriteLine($"Plan saved to: {outputFile.FullName}");
            Console.WriteLine($"Generated {plan.Steps.Count} test steps with auto-discovery");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating plan: {ex.Message}");
        }
    }

    static async Task HandleRunCommand(FileInfo planFile, FileInfo? configFile, int parallel)
    {
        try
        {
            Console.WriteLine($"Executing plan from: {planFile.FullName}");
            Console.WriteLine($"Parallel workers: {parallel}");

            if (!planFile.Exists)
            {
                Console.WriteLine("Plan file not found");
                return;
            }

            var planJson = await File.ReadAllTextAsync(planFile.FullName);
            var plan = JsonSerializer.Deserialize<PlanJson>(planJson);

            if (plan == null)
            {
                Console.WriteLine("Invalid plan file");
                return;
            }

            // Load config if provided
            var config = new AgentConfig { Parallel = parallel };
            if (configFile != null && configFile.Exists)
            {
                var configJson = await File.ReadAllTextAsync(configFile.FullName);
                config = JsonSerializer.Deserialize<AgentConfig>(configJson) ?? config;
                config.Parallel = parallel;
            }

            Console.WriteLine($"Plan contains {plan.Steps.Count} steps");
            Console.WriteLine("Execution would start here (not implemented in this demo)");

            // TODO: Implement actual execution
            Console.WriteLine("Execution completed (simulated)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error executing plan: {ex.Message}");
        }
    }

    static async Task HandleReportCommand(string runId, string format, bool open)
    {
        try
        {
            Console.WriteLine($"Generating {format} report for run: {runId}");

            // TODO: Implement report generation via API call
            Console.WriteLine("Report generation would happen here (not implemented in this demo)");

            if (open)
            {
                Console.WriteLine("Report would be opened in browser");
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error generating report: {ex.Message}");
        }
    }
}
