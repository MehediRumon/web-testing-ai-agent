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
        var recordImportCommand = new Command("import-interactions", "Import interactions from text file");
        var recordValidateCommand = new Command("validate-interactions", "Validate interaction text format");

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

        // Add options for record import command
        var importFileOption = new Option<string>("--file", "Path to interaction text file") { IsRequired = true };
        var importSessionNameOption = new Option<string>("--name", "Name for the recording session") { IsRequired = true };
        var importBaseUrlOption = new Option<string>("--base-url", "Base URL for the test");
        var importOutputOption = new Option<string>("--out", "Output file for the recording session JSON");

        recordImportCommand.AddOption(importFileOption);
        recordImportCommand.AddOption(importSessionNameOption);
        recordImportCommand.AddOption(importBaseUrlOption);
        recordImportCommand.AddOption(importOutputOption);

        // Add options for validate command
        var validateFileOption = new Option<string>("--file", "Path to interaction text file") { IsRequired = true };
        recordValidateCommand.AddOption(validateFileOption);

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

        recordImportCommand.SetHandler(async (file, name, baseUrl, output) =>
        {
            await ImportInteractionsAsync(file, name, baseUrl, output);
        }, importFileOption, importSessionNameOption, importBaseUrlOption, importOutputOption);

        recordValidateCommand.SetHandler(async (file) =>
        {
            await ValidateInteractionsAsync(file);
        }, validateFileOption);

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
        recordingCommand.AddCommand(recordImportCommand);
        recordingCommand.AddCommand(recordValidateCommand);

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

    private static async Task ImportInteractionsAsync(string filePath, string sessionName, string? baseUrl, string? outputFile)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"✗ File not found: {filePath}");
                return;
            }

            var interactionText = await File.ReadAllTextAsync(filePath);
            Console.WriteLine($"📄 Reading interactions from: {filePath}");
            Console.WriteLine($"📝 Session name: {sessionName}");

            // Call the API to import interactions
            using var client = new HttpClient();
            var request = new
            {
                sessionName = sessionName,
                baseUrl = baseUrl ?? "https://example.com",
                interactionText = interactionText
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await client.PostAsync("http://localhost:5146/api/recording/import", content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync();
                
                if (!string.IsNullOrEmpty(outputFile))
                {
                    await File.WriteAllTextAsync(outputFile, responseJson);
                    Console.WriteLine($"✓ Recording session imported and saved to: {outputFile}");
                }
                else
                {
                    Console.WriteLine("✓ Recording session imported successfully:");
                    
                    // Parse and display summary
                    var sessionData = JsonSerializer.Deserialize<JsonElement>(responseJson);
                    if (sessionData.TryGetProperty("id", out var idElement) &&
                        sessionData.TryGetProperty("steps", out var stepsElement))
                    {
                        Console.WriteLine($"   Session ID: {idElement.GetString()}");
                        Console.WriteLine($"   Steps imported: {stepsElement.GetArrayLength()}");
                    }
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"✗ Error importing interactions: {response.StatusCode}");
                Console.WriteLine($"   {errorContent}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error importing interactions: {ex.Message}");
            Console.WriteLine("   Make sure the API server is running: cd WebTestingAiAgent.Api && dotnet run");
        }
    }

    private static async Task ValidateInteractionsAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"✗ File not found: {filePath}");
                return;
            }

            var interactionText = await File.ReadAllTextAsync(filePath);
            Console.WriteLine($"🔍 Validating interactions from: {filePath}");

            // Call the API to validate interactions
            using var client = new HttpClient();
            var request = new { interactionText = interactionText };
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await client.PostAsync("http://localhost:5146/api/recording/validate", content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync();
                var validationResult = JsonSerializer.Deserialize<JsonElement>(responseJson);
                
                if (validationResult.TryGetProperty("isValid", out var isValidElement) &&
                    validationResult.TryGetProperty("message", out var messageElement))
                {
                    var isValid = isValidElement.GetBoolean();
                    var message = messageElement.GetString();
                    
                    if (isValid)
                    {
                        Console.WriteLine($"✓ {message}");
                        
                        if (validationResult.TryGetProperty("stepCount", out var stepCountElement))
                        {
                            Console.WriteLine($"   📊 Total steps: {stepCountElement.GetInt32()}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"✗ {message}");
                    }
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"✗ Error validating interactions: {response.StatusCode}");
                Console.WriteLine($"   {errorContent}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error validating interactions: {ex.Message}");
            Console.WriteLine("   Make sure the API server is running: cd WebTestingAiAgent.Api && dotnet run");
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
