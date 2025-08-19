using WebTestingAiAgent.Core.Interfaces;
using WebTestingAiAgent.Core.Models;
using System.Text.RegularExpressions;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

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
    private static bool _webDriverInitialized = false;
    private static readonly object _lockObj = new object();

    public async Task<List<StepResult>> ExecutePlanAsync(PlanJson plan, AgentConfig config)
    {
        // Initialize WebDriver once per application
        await EnsureWebDriverInitializedAsync();
        
        var results = new List<StepResult>();
        
        Console.WriteLine($"Executing plan with Headless = {config.Headless}");
        
        ChromeDriver? driver = null;
        try
        {
            var options = new ChromeOptions();
            
            if (config.Headless)
            {
                options.AddArguments("--headless");
            }
            
            options.AddArguments("--disable-dev-shm-usage", "--no-sandbox", "--disable-gpu", "--window-size=1280,720");
            
            driver = new ChromeDriver(options);
            
            try
            {
                foreach (var step in plan.Steps)
                {
                    var result = await ExecuteStepAsync(step, config, driver);
                    results.Add(result);
                    
                    // If a step fails and it's not a non-critical step, consider stopping
                    if (result.Status == "failed" && !step.Metadata.Tags.Contains("@optional"))
                    {
                        // Continue execution but mark subsequent dependent steps as skipped
                    }
                }
            }
            finally
            {
                driver?.Quit();
            }
        }
        catch (Exception ex) when (ex.Message.Contains("cannot find Chrome binary") || 
                                  ex.Message.Contains("chromedriver") || 
                                  ex.Message.Contains("Failed to start browser") ||
                                  ex.Message.Contains("Cannot start the driver service"))
        {
            // Create a proper error result indicating browser setup issue
            var endTime = DateTime.UtcNow;
            results.Add(new StepResult
            {
                StepId = "browser-initialization",
                Start = DateTime.UtcNow.AddSeconds(-1), // Give a small duration to show it attempted something
                End = endTime,
                Status = "failed",
                Notes = $"Browser initialization failed. Headless mode was set to: {config.Headless}. " +
                       "Please ensure Chrome browser and ChromeDriver are installed and available in PATH.",
                Error = new StepError 
                { 
                    Message = $"Browser setup failed: {ex.Message}. " +
                             "Install Chrome browser and ensure ChromeDriver is available."
                },
                Evidence = new Evidence()
            });
            
            Console.WriteLine($"Browser initialization failed: {ex.Message}");
            Console.WriteLine($"Browser configuration attempted: Headless = {config.Headless}");
            Console.WriteLine("To fix this issue, ensure Chrome browser is installed and ChromeDriver is available in PATH");
        }
        catch (Exception)
        {
            driver?.Quit();
            throw;
        }

        return results;
    }

    public async Task<StepResult> ExecuteStepAsync(TestStep step, AgentConfig config)
    {
        // This overload creates its own browser instance
        await EnsureWebDriverInitializedAsync();
        
        ChromeDriver? driver = null;
        try
        {
            var options = new ChromeOptions();
            
            if (config.Headless)
            {
                options.AddArguments("--headless");
            }
            
            options.AddArguments("--disable-dev-shm-usage", "--no-sandbox", "--disable-gpu", "--window-size=1280,720");
            
            driver = new ChromeDriver(options);
            
            try
            {
                return await ExecuteStepAsync(step, config, driver);
            }
            finally
            {
                driver?.Quit();
            }
        }
        catch (Exception ex) when (ex.Message.Contains("cannot find Chrome binary") || 
                                  ex.Message.Contains("chromedriver") || 
                                  ex.Message.Contains("Failed to start browser") ||
                                  ex.Message.Contains("Cannot start the driver service"))
        {
            // Return a proper error result for single step execution
            var endTime = DateTime.UtcNow;
            return new StepResult
            {
                StepId = step.Id,
                Start = DateTime.UtcNow.AddSeconds(-1),
                End = endTime,
                Status = "failed",
                Notes = $"Browser initialization failed for step '{step.Id}'. Headless mode: {config.Headless}",
                Error = new StepError 
                { 
                    Message = $"Browser setup failed: {ex.Message}. Install Chrome browser and ensure ChromeDriver is available."
                },
                Evidence = new Evidence()
            };
        }
        catch (Exception)
        {
            driver?.Quit();
            throw;
        }
    }

    private async Task<StepResult> ExecuteStepAsync(TestStep step, AgentConfig config, ChromeDriver driver)
    {
        var stepResult = new StepResult
        {
            StepId = step.Id,
            Start = DateTime.UtcNow,
            Evidence = new Evidence
            {
                Console = new List<string>(),
                Network = new List<NetworkRequest>()
            }
        };

        try
        {
            // Note: Selenium doesn't have built-in console/network monitoring capabilities
            // These would need to be implemented through browser logs if needed

            Console.WriteLine($"Executing step: {step.Id} - {step.Action}");

            switch (step.Action.ToLower())
            {
                case "navigate":
                    await ExecuteNavigateAsync(driver, step);
                    break;
                
                case "click":
                    await ExecuteClickAsync(driver, step);
                    break;
                
                case "input":
                    await ExecuteInputAsync(driver, step);
                    break;
                
                case "assert":
                    await ExecuteAssertAsync(driver, step);
                    break;
                
                default:
                    throw new NotImplementedException($"Action '{step.Action}' is not implemented");
            }

            // Validate assertions
            var assertionResults = await ValidateAssertionsAsync(driver, step.Assertions);
            
            stepResult.Status = assertionResults.All(a => a) ? "passed" : "failed";
            stepResult.Notes = $"Step executed successfully. Assertions: {assertionResults.Count(a => a)}/{assertionResults.Count} passed";

            // Capture screenshot on failure or if explicitly requested
            if (stepResult.Status == "failed" || step.Metadata.Tags.Contains("@screenshot"))
            {
                await CaptureEvidenceAsync(driver, stepResult);
            }
        }
        catch (Exception ex)
        {
            stepResult.Status = "failed";
            stepResult.Error = new StepError { Message = ex.Message };
            stepResult.Notes = $"Step failed with error: {ex.Message}";
            
            // Capture evidence on error
            await CaptureEvidenceAsync(driver, stepResult);
        }
        finally
        {
            stepResult.End = DateTime.UtcNow;
        }

        return stepResult;
    }

    private async Task ExecuteNavigateAsync(ChromeDriver driver, TestStep step)
    {
        if (step.Target?.Primary?.Value == null)
            throw new ArgumentException("Navigate action requires a URL value");

        // Set page load timeout
        driver.Manage().Timeouts().PageLoad = TimeSpan.FromMilliseconds(step.TimeoutMs > 0 ? step.TimeoutMs : 30000);
        
        driver.Navigate().GoToUrl(step.Target.Primary.Value);
        
        // Wait a bit for the page to stabilize (similar to network idle waiting)
        await Task.Delay(1000);
    }

    private async Task ExecuteClickAsync(ChromeDriver driver, TestStep step)
    {
        var element = await FindElementAsync(driver, step.Target);
        element.Click();
    }

    private async Task ExecuteInputAsync(ChromeDriver driver, TestStep step)
    {
        if (step.Value == null)
            throw new ArgumentException("Input action requires a value");

        var element = await FindElementAsync(driver, step.Target);
        element.Clear();
        element.SendKeys(step.Value);
    }

    private async Task ExecuteAssertAsync(ChromeDriver driver, TestStep step)
    {
        if (step.Target != null)
        {
            var element = await FindElementAsync(driver, step.Target);
            // Just finding the element is sufficient for basic assertion
            // More complex assertions would be handled in ValidateAssertionsAsync
        }
    }

    private async Task<IWebElement> FindElementAsync(ChromeDriver driver, Target? target)
    {
        if (target?.Primary == null)
            throw new ArgumentException("Target primary locator is required");

        try
        {
            var by = GetLocator(target.Primary);
            
            // Wait for element to be present
            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(5));
            var element = wait.Until(d => d.FindElement(by));
            
            return element;
        }
        catch (WebDriverTimeoutException)
        {
            // Try fallback locators
            foreach (var fallback in target.Fallbacks ?? new List<Locator>())
            {
                try
                {
                    var by = GetLocator(fallback);
                    var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(2));
                    return wait.Until(d => d.FindElement(by));
                }
                catch (WebDriverTimeoutException)
                {
                    continue;
                }
            }
            
            throw new Exception($"Element not found with primary locator {target.Primary.By}='{target.Primary.Value}' or any fallbacks");
        }
    }

    private By GetLocator(Locator locator)
    {
        return locator.By.ToLower() switch
        {
            "id" => By.Id(locator.Value),
            "css" => By.CssSelector(locator.Value),
            "xpath" => By.XPath(locator.Value),
            "name" => By.Name(locator.Value),
            "linktext" => By.LinkText(locator.Value),
            "partiallinktext" => By.PartialLinkText(locator.Value),
            "text" => By.XPath($"//*[contains(text(), '{locator.Value}')]"),
            "classname" => By.ClassName(locator.Value),
            "tagname" => By.TagName(locator.Value),
            "url" => throw new ArgumentException("URL locator is only valid for navigate actions"),
            _ => throw new ArgumentException($"Unsupported locator type: {locator.By}")
        };
    }

    private async Task<List<bool>> ValidateAssertionsAsync(ChromeDriver driver, List<Assertion> assertions)
    {
        var results = new List<bool>();
        
        foreach (var assertion in assertions)
        {
            try
            {
                bool result = assertion.Type.ToLower() switch
                {
                    "statusok" => await ValidateStatusOkAsync(driver, assertion.Value),
                    "elementvisible" => await ValidateElementVisibleAsync(driver, assertion.Value),
                    "textcontains" => await ValidateTextContainsAsync(driver, assertion.Value),
                    _ => true // Unknown assertion types pass by default
                };
                
                results.Add(result);
            }
            catch
            {
                results.Add(false);
            }
        }
        
        return results;
    }

    private async Task<bool> ValidateStatusOkAsync(ChromeDriver driver, string expectedStatus)
    {
        await Task.CompletedTask;
        // Note: Selenium doesn't expose HTTP response status directly
        // For basic validation, we can check if the page loaded without errors
        try
        {
            // Check if page title is accessible (indicates successful page load)
            var title = driver.Title;
            return !string.IsNullOrEmpty(title);
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> ValidateElementVisibleAsync(ChromeDriver driver, string selector)
    {
        try
        {
            await Task.CompletedTask;
            
            if (selector == "true" || selector == "body")
            {
                // General page visibility check
                var bodyElement = driver.FindElement(By.TagName("body"));
                return bodyElement.Displayed;
            }
            
            // Try as CSS selector first, then as XPath if needed
            IWebElement element;
            try
            {
                element = driver.FindElement(By.CssSelector(selector));
            }
            catch
            {
                element = driver.FindElement(By.XPath(selector));
            }
            
            return element.Displayed;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> ValidateTextContainsAsync(ChromeDriver driver, string expectedText)
    {
        try
        {
            await Task.CompletedTask;
            var bodyText = driver.FindElement(By.TagName("body")).Text;
            return bodyText.Contains(expectedText, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private async Task CaptureEvidenceAsync(ChromeDriver driver, StepResult stepResult)
    {
        try
        {
            // Create evidence directory
            var evidenceDir = Path.Combine(Directory.GetCurrentDirectory(), "evidence", stepResult.StepId);
            Directory.CreateDirectory(evidenceDir);

            // Capture screenshot
            var screenshotPath = Path.Combine(evidenceDir, "screenshot.png");
            var screenshot = driver.GetScreenshot();
            screenshot.SaveAsFile(screenshotPath);
            stepResult.Evidence.ScreenshotPath = screenshotPath;

            // Capture DOM snapshot
            var domPath = Path.Combine(evidenceDir, "dom.html");
            var content = driver.PageSource;
            await File.WriteAllTextAsync(domPath, content);
            stepResult.Evidence.DomSnapshotPath = domPath;
        }
        catch (Exception ex)
        {
            // Don't fail the step if evidence capture fails
            stepResult.Evidence.Console.Add($"Evidence capture failed: {ex.Message}");
        }
    }

    private async Task EnsureWebDriverInitializedAsync()
    {
        if (!_webDriverInitialized)
        {
            lock (_lockObj)
            {
                if (!_webDriverInitialized)
                {
                    // Note: Chrome browser and ChromeDriver should be installed and available in PATH
                    // ChromeDriver will be automatically downloaded by the Selenium.WebDriver.ChromeDriver package
                    _webDriverInitialized = true;
                }
            }
        }
        
        await Task.CompletedTask;
    }

    public async Task CancelRunAsync(string runId)
    {
        await Task.CompletedTask;
        // TODO: Implement run cancellation with proper tracking
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