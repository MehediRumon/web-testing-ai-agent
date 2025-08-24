using System.CommandLine;
using System.Text.Json;
using WebTestingAiAgent.Core.Models;

namespace WebTestingAiAgent.Cli;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Web Testing AI Agent CLI - Record, save, and replay web tests");

        // Test case commands
        var testCaseCommand = new Command("testcase", "Manage test cases");
        var createCommand = new Command("create", "Create a new test case");
        var listCommand = new Command("list", "List test cases");
        var exportCommand = new Command("export", "Export a test case");
        var importCommand = new Command("import", "Import a test case");

        // Recording commands
        var recordCommand = new Command("record", "Start a recording session");
        var recordStartCommand = new Command("start", "Start recording");
        var recordStopCommand = new Command("stop", "Stop recording");
        var recordSaveCommand = new Command("save", "Save recording as test case");

        // Execution commands
        var runCommand = new Command("run", "Execute test cases");
        var executeCommand = new Command("execute", "Execute a specific test case");
        var statusCommand = new Command("status", "Check execution status");

        // Legacy plan command for compatibility
        var planCommand = new Command("plan", "Create a test plan (legacy)");

        // Add options for create command
        var nameOption = new Option<string>("--name", "Test case name") { IsRequired = true };
        var urlOption = new Option<string>("--url", "Base URL") { IsRequired = true };
        var descriptionOption = new Option<string>("--description", "Test case description");
        var formatOption = new Option<string>("--format", () => "json", "Output format (json, yaml, gherkin)");
        var outputOption = new Option<string>("--out", "Output file path");

        createCommand.AddOption(nameOption);
        createCommand.AddOption(urlOption);
        createCommand.AddOption(descriptionOption);
        createCommand.AddOption(formatOption);
        createCommand.AddOption(outputOption);

        // Add options for record start command
        var recordNameOption = new Option<string>("--name", "Recording session name") { IsRequired = true };
        var recordUrlOption = new Option<string>("--url", "Base URL to start recording") { IsRequired = true };

        recordStartCommand.AddOption(recordNameOption);
        recordStartCommand.AddOption(recordUrlOption);

        // Add options for execute command
        var testCaseIdOption = new Option<string>("--id", "Test case ID") { IsRequired = true };
        var browserOption = new Option<string>("--browser", () => "chrome", "Browser to use");
        var headlessOption = new Option<bool>("--headless", () => true, "Run in headless mode");

        executeCommand.AddOption(testCaseIdOption);
        executeCommand.AddOption(browserOption);
        executeCommand.AddOption(headlessOption);

        // Set up command handlers
        createCommand.SetHandler(async (name, url, description, format, output) =>
        {
            await CreateTestCaseAsync(name, url, description, format, output);
        }, nameOption, urlOption, descriptionOption, formatOption, outputOption);

        listCommand.SetHandler(async () =>
        {
            await ListTestCasesAsync();
        });

        recordStartCommand.SetHandler(async (name, url) =>
        {
            await StartRecordingAsync(name, url);
        }, recordNameOption, recordUrlOption);

        executeCommand.SetHandler(async (id, browser, headless) =>
        {
            await ExecuteTestCaseAsync(id, browser, headless);
        }, testCaseIdOption, browserOption, headlessOption);

        // Legacy plan command
        var objectiveOption = new Option<string>("--objective", "Test objective") { IsRequired = true };
        var baseUrlOption = new Option<string>("--baseUrl", "Base URL") { IsRequired = true };
        var outOption = new Option<string>("--out", "Output file") { IsRequired = true };

        planCommand.AddOption(objectiveOption);
        planCommand.AddOption(baseUrlOption);
        planCommand.AddOption(outOption);

        planCommand.SetHandler(async (objective, baseUrl, outFile) =>
        {
            await CreateLegacyPlanAsync(objective, baseUrl, outFile);
        }, objectiveOption, baseUrlOption, outOption);

        // Add commands to structure
        testCaseCommand.AddCommand(createCommand);
        testCaseCommand.AddCommand(listCommand);
        testCaseCommand.AddCommand(exportCommand);
        testCaseCommand.AddCommand(importCommand);

        var recordingCommand = new Command("recording", "Recording session management");
        recordingCommand.AddCommand(recordStartCommand);
        recordingCommand.AddCommand(recordStopCommand);
        recordingCommand.AddCommand(recordSaveCommand);

        var executionCommand = new Command("execution", "Test execution management");
        executionCommand.AddCommand(executeCommand);
        executionCommand.AddCommand(statusCommand);

        rootCommand.AddCommand(testCaseCommand);
        rootCommand.AddCommand(recordingCommand);
        rootCommand.AddCommand(executionCommand);
        rootCommand.AddCommand(runCommand);
        rootCommand.AddCommand(planCommand);

        return await rootCommand.InvokeAsync(args);
    }

    private static async Task CreateTestCaseAsync(string name, string url, string? description, string format, string? outputFile)
    {
        try
        {
            var testCase = new TestCase
            {
                Name = name,
                BaseUrl = url,
                Description = description ?? "",
                Format = ParseFormat(format),
                Tags = new List<string> { "cli-created" }
            };

            var json = JsonSerializer.Serialize(testCase, new JsonSerializerOptions { WriteIndented = true });

            if (!string.IsNullOrEmpty(outputFile))
            {
                await File.WriteAllTextAsync(outputFile, json);
                Console.WriteLine($"✓ Test case created and saved to {outputFile}");
            }
            else
            {
                Console.WriteLine(json);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error creating test case: {ex.Message}");
        }
    }

    private static async Task ListTestCasesAsync()
    {
        try
        {
            // In a real implementation, this would call the API
            Console.WriteLine("📋 Test Cases:");
            Console.WriteLine("Use the API server to manage test cases dynamically.");
            Console.WriteLine("Start the API with: cd WebTestingAiAgent.Api && dotnet run");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error listing test cases: {ex.Message}");
        }
    }

    private static async Task StartRecordingAsync(string name, string url)
    {
        try
        {
            Console.WriteLine($"🎬 Starting recording session: {name}");
            Console.WriteLine($"📍 Base URL: {url}");
            Console.WriteLine("Use the Web UI for interactive recording functionality.");
            Console.WriteLine("Open browser to: http://localhost:5201/recording");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error starting recording: {ex.Message}");
        }
    }

    private static async Task ExecuteTestCaseAsync(string id, string browser, bool headless)
    {
        try
        {
            Console.WriteLine($"🚀 Executing test case: {id}");
            Console.WriteLine($"🌐 Browser: {browser} (headless: {headless})");
            Console.WriteLine("Use the API server for test execution functionality.");
            Console.WriteLine("Start the API with: cd WebTestingAiAgent.Api && dotnet run");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error executing test case: {ex.Message}");
        }
    }

    private static async Task CreateLegacyPlanAsync(string objective, string baseUrl, string outputFile)
    {
        try
        {
            var plan = new PlanJson
            {
                RunId = Guid.NewGuid().ToString(),
                BaseUrl = baseUrl,
                Objective = objective,
                Steps = new List<TestStep>
                {
                    new TestStep
                    {
                        Id = "step-001",
                        Action = "navigate",
                        Target = new Target
                        {
                            Primary = new Locator { By = "url", Value = baseUrl }
                        },
                        Metadata = new StepMetadata { Tags = new List<string> { "@navigation" } }
                    }
                }
            };

            var json = JsonSerializer.Serialize(plan, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(outputFile, json);
            
            Console.WriteLine($"✓ Legacy plan created: {outputFile}");
            Console.WriteLine($"📝 Objective: {objective}");
            Console.WriteLine($"🔗 Base URL: {baseUrl}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error creating plan: {ex.Message}");
        }
    }

    private static TestCaseFormat ParseFormat(string format)
    {
        return format.ToLower() switch
        {
            "json" => TestCaseFormat.Json,
            "yaml" => TestCaseFormat.Yaml,
            "gherkin" => TestCaseFormat.Gherkin,
            _ => TestCaseFormat.Json
        };
    }
}
