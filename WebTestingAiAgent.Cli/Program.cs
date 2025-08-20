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
            var plan = JsonSerializer.Deserialize<PlanJson>(planJson, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

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
                config = JsonSerializer.Deserialize<AgentConfig>(configJson, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }) ?? config;
                config.Parallel = parallel;
            }

            Console.WriteLine($"Plan contains {plan.Steps.Count} steps");
            
            // Check if we should run in mock mode (when Chrome is not available)
            bool useMockExecution = ShouldUseMockExecution();
            
            if (useMockExecution)
            {
                Console.WriteLine("\n⚠️  Chrome browser not detected - Running in MOCK MODE for demonstration");
                Console.WriteLine("This will simulate execution without actually opening a browser.\n");
                
                // Simulate execution for demonstration
                var mockResults = SimulatePlanExecution(plan);
                
                Console.WriteLine($"Mock execution completed. Results:");
                Console.WriteLine($"  Total steps: {mockResults.Count}");
                Console.WriteLine($"  Passed: {mockResults.Count(r => r.Status == "passed")}");
                Console.WriteLine($"  Failed: {mockResults.Count(r => r.Status == "failed")}");
                Console.WriteLine($"  Errors: {mockResults.Count(r => r.Error != null)}");
                
                // Display summary of failures and potential bugs
                DisplayExecutionSummary(mockResults);
            }
            else
            {
                // Initialize and execute the plan using ExecutorService
                var executorService = new WebTestingAiAgent.Api.Services.ExecutorService();
                var results = await executorService.ExecutePlanAsync(plan, config);
                
                Console.WriteLine($"Execution completed. Results:");
                Console.WriteLine($"  Total steps: {results.Count}");
                Console.WriteLine($"  Passed: {results.Count(r => r.Status == "passed")}");
                Console.WriteLine($"  Failed: {results.Count(r => r.Status == "failed")}");
                Console.WriteLine($"  Errors: {results.Count(r => r.Error != null)}");
                
                // Display summary of failures and potential bugs
                DisplayExecutionSummary(results);
            }
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

    static bool ShouldUseMockExecution()
    {
        // Check if we're in a headless environment without Chrome
        return string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")) || 
               !System.IO.File.Exists("/usr/bin/google-chrome") && 
               !System.IO.File.Exists("/usr/bin/chromium-browser");
    }

    static List<StepResult> SimulatePlanExecution(PlanJson plan)
    {
        var results = new List<StepResult>();
        var random = new Random(42); // Fixed seed for consistent results
        
        Console.WriteLine("🎭 Simulating test execution...\n");
        
        foreach (var step in plan.Steps)
        {
            Console.WriteLine($"Executing step {step.Id}: {step.Action}");
            
            var stepResult = new StepResult
            {
                StepId = step.Id,
                Start = DateTime.UtcNow.AddSeconds(-1),
                End = DateTime.UtcNow,
                Evidence = new Evidence
                {
                    Console = new List<string>(),
                    Network = new List<NetworkRequest>()
                }
            };

            // Simulate different outcomes based on step type and URL
            if (step.Action == "navigate")
            {
                var url = step.Target?.Primary?.Value ?? "";
                if (url.Contains("/admin") || url.Contains("/private"))
                {
                    // Simulate access denied for admin/private pages
                    stepResult.Status = "failed";
                    stepResult.Error = new StepError { Message = "403 Forbidden - Access denied to admin area" };
                    stepResult.Notes = "🐛 POTENTIAL BUG: Admin page accessible without proper authorization";
                }
                else if (url.Contains("/user") && random.NextDouble() < 0.3)
                {
                    // Simulate occasional 404 error
                    stepResult.Status = "failed";
                    stepResult.Error = new StepError { Message = "404 Not Found - User page not found" };
                    stepResult.Notes = "🐛 POTENTIAL BUG: User page returns 404 error";
                }
                else
                {
                    stepResult.Status = "passed";
                    stepResult.Notes = $"Successfully navigated to {url}";
                }
            }
            else if (step.Action == "click" && step.Metadata.Tags.Contains("@login"))
            {
                stepResult.Status = "passed";
                stepResult.Notes = "Login button clicked successfully";
            }
            else if (step.Action == "input" && step.Metadata.Tags.Contains("@login"))
            {
                stepResult.Status = "passed";
                stepResult.Notes = "Login credentials entered successfully";
            }
            else if (step.Action == "assert" && step.Metadata.Tags.Contains("@error-detection"))
            {
                // Simulate finding some errors
                if (random.NextDouble() < 0.2)
                {
                    stepResult.Status = "failed";
                    stepResult.Error = new StepError { Message = "Error element detected on page" };
                    stepResult.Notes = "🐛 POTENTIAL BUG: Error message visible on page";
                }
                else
                {
                    stepResult.Status = "passed";
                    stepResult.Notes = "No error indicators found";
                }
            }
            else
            {
                // Default simulation
                stepResult.Status = random.NextDouble() < 0.85 ? "passed" : "failed";
                if (stepResult.Status == "failed")
                {
                    stepResult.Error = new StepError { Message = "Simulated test failure" };
                    stepResult.Notes = "🐛 POTENTIAL BUG: Simulated failure for demonstration";
                }
                else
                {
                    stepResult.Notes = $"Step {step.Action} completed successfully";
                }
            }
            
            results.Add(stepResult);
            Thread.Sleep(100); // Simulate execution time
        }
        
        return results;
    }

    static void DisplayExecutionSummary(List<StepResult> results)
    {
        var failures = results.Where(r => r.Status == "failed" || r.Error != null).ToList();
        if (failures.Any())
        {
            Console.WriteLine("\n=== POTENTIAL BUGS AND ERRORS DETECTED ===");
            var bugCount = 0;
            foreach (var failure in failures)
            {
                bugCount++;
                Console.WriteLine($"\n🐛 Bug #{bugCount} - Step {failure.StepId}:");
                Console.WriteLine($"  Status: {failure.Status}");
                if (failure.Error != null)
                {
                    Console.WriteLine($"  Error: {failure.Error.Message}");
                }
                if (!string.IsNullOrEmpty(failure.Notes))
                {
                    Console.WriteLine($"  Notes: {failure.Notes}");
                }
            }
            Console.WriteLine($"\n🔍 Summary: {bugCount} potential bugs detected across {results.Count} test steps");
        }
        else
        {
            Console.WriteLine("\n✅ All tests passed - No bugs detected!");
        }
    }
}
