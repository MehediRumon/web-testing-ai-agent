using WebTestingAiAgent.Core.Interfaces;
using WebTestingAiAgent.Core.Models;
using System.Text.RegularExpressions;

namespace WebTestingAiAgent.Api.Services;

public class PlannerService : IPlannerService
{
    public async Task<PlanJson> CreatePlanAsync(string objective, string baseUrl, AgentConfig config)
    {
        // If no objective provided, auto-generate based on site discovery
        if (string.IsNullOrWhiteSpace(objective))
        {
            objective = "Automatically test basic functionality of the web application";
        }
        
        // Auto-discover pages and generate test cases from BaseURL only
        var discoveredElements = await DiscoverSiteStructureAsync(baseUrl, config.Exploration.MaxDepth);
        var steps = await GenerateAutomaticTestCasesAsync(baseUrl, discoveredElements);
        
        var plan = new PlanJson
        {
            RunId = Guid.NewGuid().ToString(),
            BaseUrl = baseUrl,
            Objective = objective,
            TimeBudgetSeconds = config.Exploration.TimeBudgetSec,
            ExplorationDepth = config.Exploration.MaxDepth,
            Steps = steps
        };

        return plan;
    }

    public async Task<PlanJson> ReplanAsync(PlanJson originalPlan, string feedback, AgentConfig config)
    {
        await Task.CompletedTask;
        
        // TODO: Implement re-planning logic based on feedback
        return originalPlan;
    }

    public async Task<bool> ValidatePlanAsync(PlanJson plan)
    {
        await Task.CompletedTask;
        
        // Basic validation - objective can be auto-generated so it's not strictly required
        return !string.IsNullOrEmpty(plan.RunId) &&
               !string.IsNullOrEmpty(plan.BaseUrl) &&
               plan.Steps.Any();
    }

    private async Task<DiscoveredElements> DiscoverSiteStructureAsync(string baseUrl, int maxDepth)
    {
        await Task.CompletedTask;
        
        // TODO: Implement real site crawling with HttpClient
        // For now, generate common elements that typical websites have
        var discovered = new DiscoveredElements
        {
            Links = GenerateCommonLinks(baseUrl),
            Forms = GenerateCommonForms(),
            Buttons = GenerateCommonButtons(),
            InputFields = GenerateCommonInputs()
        };

        return discovered;
    }

    private async Task<List<TestStep>> GenerateAutomaticTestCasesAsync(string baseUrl, DiscoveredElements discovered)
    {
        await Task.CompletedTask;
        
        var steps = new List<TestStep>();
        int stepCounter = 1;

        // 1. Navigate to main page and verify it loads
        steps.Add(new TestStep
        {
            Id = $"step-{stepCounter++:D3}",
            Action = "navigate",
            Target = new Target
            {
                Primary = new Locator { By = "url", Value = baseUrl }
            },
            Assertions = new List<Assertion>
            {
                new Assertion { Type = "statusOk", Value = "200" },
                new Assertion { Type = "elementVisible", Value = "body" }
            },
            TimeoutMs = 10000,
            Metadata = new StepMetadata { Tags = new List<string> { "@navigation", "@basic" } }
        });

        // 2. Verify main banner/header visibility
        steps.Add(new TestStep
        {
            Id = $"step-{stepCounter++:D3}",
            Action = "assert",
            Target = new Target
            {
                Primary = new Locator { By = "css", Value = "header, .header, #header, h1, .banner" }
            },
            Assertions = new List<Assertion>
            {
                new Assertion { Type = "elementVisible", Value = "true" }
            },
            TimeoutMs = 5000,
            Metadata = new StepMetadata { Tags = new List<string> { "@visibility", "@basic" } }
        });

        // 3. Test navigation links
        foreach (var link in discovered.Links.Take(5)) // Limit to first 5 links
        {
            steps.Add(new TestStep
            {
                Id = $"step-{stepCounter++:D3}",
                Action = "click",
                Target = new Target
                {
                    Primary = new Locator { By = "linkText", Value = link },
                    Fallbacks = new List<Locator>
                    {
                        new Locator { By = "partialLinkText", Value = link },
                        new Locator { By = "css", Value = $"a[href*='{link.ToLower()}']" }
                    }
                },
                Assertions = new List<Assertion>
                {
                    new Assertion { Type = "statusOk", Value = "200" },
                    new Assertion { Type = "elementVisible", Value = "body" }
                },
                TimeoutMs = 10000,
                Metadata = new StepMetadata { Tags = new List<string> { "@navigation", "@links" } }
            });

            // Navigate back to main page
            steps.Add(new TestStep
            {
                Id = $"step-{stepCounter++:D3}",
                Action = "navigate",
                Target = new Target
                {
                    Primary = new Locator { By = "url", Value = baseUrl }
                },
                TimeoutMs = 10000,
                Metadata = new StepMetadata { Tags = new List<string> { "@navigation", "@back" } }
            });
        }

        // 4. Test forms if found
        foreach (var form in discovered.Forms.Take(2)) // Limit to first 2 forms
        {
            // Find form inputs and fill them
            foreach (var input in discovered.InputFields.Where(i => form.Contains(i, StringComparison.OrdinalIgnoreCase)).Take(3))
            {
                steps.Add(new TestStep
                {
                    Id = $"step-{stepCounter++:D3}",
                    Action = "input",
                    Target = new Target
                    {
                        Primary = new Locator { By = "name", Value = input },
                        Fallbacks = new List<Locator>
                        {
                            new Locator { By = "id", Value = input },
                            new Locator { By = "css", Value = $"input[placeholder*='{input}']" }
                        }
                    },
                    Value = GenerateTestValueForInput(input),
                    TimeoutMs = 5000,
                    Metadata = new StepMetadata { Tags = new List<string> { "@forms", "@input" } }
                });
            }

            // Submit form
            steps.Add(new TestStep
            {
                Id = $"step-{stepCounter++:D3}",
                Action = "click",
                Target = new Target
                {
                    Primary = new Locator { By = "css", Value = "input[type='submit'], button[type='submit'], button:contains('Submit')" },
                    Fallbacks = new List<Locator>
                    {
                        new Locator { By = "css", Value = "button" },
                        new Locator { By = "css", Value = "[type='submit']" }
                    }
                },
                Assertions = new List<Assertion>
                {
                    new Assertion { Type = "statusOk", Value = "200" }
                },
                TimeoutMs = 10000,
                Metadata = new StepMetadata { Tags = new List<string> { "@forms", "@submit" } }
            });
        }

        // 5. Test clickable elements/buttons
        foreach (var button in discovered.Buttons.Take(3)) // Limit to first 3 buttons
        {
            steps.Add(new TestStep
            {
                Id = $"step-{stepCounter++:D3}",
                Action = "click",
                Target = new Target
                {
                    Primary = new Locator { By = "css", Value = $"button:contains('{button}'), input[value='{button}']" },
                    Fallbacks = new List<Locator>
                    {
                        new Locator { By = "partialLinkText", Value = button },
                        new Locator { By = "css", Value = $"[title*='{button}']" }
                    }
                },
                TimeoutMs = 5000,
                Metadata = new StepMetadata { Tags = new List<string> { "@buttons", "@interaction" } }
            });
        }

        return steps;
    }

    private List<string> GenerateCommonLinks(string baseUrl)
    {
        var domain = new Uri(baseUrl).Host;
        return new List<string>
        {
            "Home", "About", "About Us", "Contact", "Contact Us", "Login", "Sign In", 
            "Register", "Sign Up", "Services", "Products", "Blog", "News", "Help", 
            "Support", "FAQ", "Privacy", "Terms", "Documentation", "Docs"
        };
    }

    private List<string> GenerateCommonForms()
    {
        return new List<string>
        {
            "login", "signin", "contact", "register", "signup", "search", "newsletter", "comment"
        };
    }

    private List<string> GenerateCommonButtons()
    {
        return new List<string>
        {
            "Submit", "Send", "Login", "Sign In", "Register", "Sign Up", "Search", 
            "Subscribe", "Download", "Get Started", "Learn More", "Continue"
        };
    }

    private List<string> GenerateCommonInputs()
    {
        return new List<string>
        {
            "email", "username", "password", "name", "firstname", "lastname", "phone", 
            "message", "subject", "search", "query", "comment", "title"
        };
    }

    private string GenerateTestValueForInput(string inputName)
    {
        return inputName.ToLower() switch
        {
            "email" => "test@example.com",
            "username" => "testuser",
            "password" => "TestPassword123!",
            "name" or "firstname" => "Test",
            "lastname" => "User",
            "phone" => "555-0123",
            "message" or "comment" => "This is a test message.",
            "subject" or "title" => "Test Subject",
            "search" or "query" => "test",
            _ => "test value"
        };
    }

    private List<TestStep> GenerateBasicSteps(string objective, string baseUrl)
    {
        // Generate basic steps based on objective
        var steps = new List<TestStep>();

        // Always start with navigation
        steps.Add(new TestStep
        {
            Id = "navigate-01",
            Action = "navigate",
            Target = new Target
            {
                Primary = new Locator { By = "url", Value = baseUrl }
            },
            TimeoutMs = 10000,
            Metadata = new StepMetadata { Tags = new List<string> { "@navigation" } }
        });

        // Add basic assertion for page load
        steps.Add(new TestStep
        {
            Id = "assert-page-load",
            Action = "assert",
            Assertions = new List<Assertion>
            {
                new Assertion { Type = "statusOk", Value = "200" }
            },
            TimeoutMs = 5000,
            Metadata = new StepMetadata { Tags = new List<string> { "@assertion" } }
        });

        return steps;
    }
}

public class ExecutorService : IExecutorService
{
    public async Task<List<StepResult>> ExecutePlanAsync(PlanJson plan, AgentConfig config)
    {
        await Task.CompletedTask;
        
        // TODO: Implement plan execution
        var results = new List<StepResult>();
        
        foreach (var step in plan.Steps)
        {
            var result = await ExecuteStepAsync(step, config);
            results.Add(result);
        }

        return results;
    }

    public async Task<StepResult> ExecuteStepAsync(TestStep step, AgentConfig config)
    {
        await Task.CompletedTask;
        
        // TODO: Implement step execution with browser automation
        return new StepResult
        {
            StepId = step.Id,
            Status = "passed",
            Start = DateTime.UtcNow.AddSeconds(-1),
            End = DateTime.UtcNow,
            Notes = "Step executed successfully (stub implementation)",
            Evidence = new Evidence
            {
                Console = new List<string>(),
                Network = new List<NetworkRequest>()
            }
        };
    }

    public async Task CancelRunAsync(string runId)
    {
        await Task.CompletedTask;
        // TODO: Implement run cancellation
    }
}

public class AssertionService : IAssertionService
{
    public async Task<bool> EvaluateAssertionAsync(Assertion assertion, object context)
    {
        await Task.CompletedTask;
        // TODO: Implement assertion evaluation
        return true;
    }

    public async Task<List<string>> RunHeuristicsAsync(object pageContext)
    {
        await Task.CompletedTask;
        // TODO: Implement heuristic checks
        return new List<string>();
    }

    public async Task<string> PerformSoftOracleAsync(string subGoal, object uiContext)
    {
        await Task.CompletedTask;
        // TODO: Implement AI soft oracle
        return "Soft oracle evaluation (stub implementation)";
    }
}

public class HealingService : IHealingService
{
    public async Task<List<Locator>> GetFallbackLocatorsAsync(Locator failedLocator, object pageContext)
    {
        await Task.CompletedTask;
        // TODO: Implement fallback locator generation
        return new List<Locator>();
    }

    public async Task<HealingSuggestion> GenerateHealingSuggestionAsync(Locator failedLocator, object pageContext)
    {
        await Task.CompletedTask;
        // TODO: Implement healing suggestion generation
        return new HealingSuggestion
        {
            SuggestedLocator = failedLocator,
            Confidence = 0.5
        };
    }

    public async Task<string> GenerateHealingReportAsync(List<HealingSuggestion> suggestions)
    {
        await Task.CompletedTask;
        // TODO: Implement healing report generation
        return "{}";
    }
}

// Helper class for site discovery
public class DiscoveredElements
{
    public List<string> Links { get; set; } = new();
    public List<string> Forms { get; set; } = new();
    public List<string> Buttons { get; set; } = new();
    public List<string> InputFields { get; set; } = new();
}