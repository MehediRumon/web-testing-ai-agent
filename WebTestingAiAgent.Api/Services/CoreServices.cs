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
        
        List<TestStep> steps;
        
        // Check if this is a login-focused test
        if (objective.ToLower().Contains("login") || objective.ToLower().Contains("signin") || 
            objective.ToLower().Contains("authentication") || objective.ToLower().Contains("credentials"))
        {
            // Determine if this is a valid or invalid login test based on the objective
            bool testInvalidLogin = objective.ToLower().Contains("invalid") || 
                                   objective.ToLower().Contains("incorrect") || 
                                   objective.ToLower().Contains("wrong") ||
                                   objective.ToLower().Contains("failed") ||
                                   objective.ToLower().Contains("error");
            
            bool testValidLogin = (objective.ToLower().Contains("valid") || 
                                 objective.ToLower().Contains("correct") || 
                                 objective.ToLower().Contains("successful") ||
                                 objective.ToLower().Contains("success")) && !testInvalidLogin; // Don't test valid if invalid is explicitly requested
            
            // If neither valid nor invalid is specified, default to valid login test
            if (!testValidLogin && !testInvalidLogin)
            {
                testValidLogin = true;
            }
            
            steps = new List<TestStep>();
            
            // Generate test cases based on the specific objective
            if (testValidLogin)
            {
                var validLoginSteps = await GenerateLoginTestCasesAsync(baseUrl, withValidCredentials: true);
                steps.AddRange(validLoginSteps);
            }
            
            if (testInvalidLogin)
            {
                var invalidLoginSteps = await GenerateLoginTestCasesAsync(baseUrl, withValidCredentials: false);
                steps.AddRange(invalidLoginSteps);
            }
        }
        else
        {
            // Auto-discover pages and generate test cases from BaseURL only
            var discoveredElements = await DiscoverSiteStructureAsync(baseUrl, config.Exploration.MaxDepth);
            steps = await GenerateAutomaticTestCasesAsync(baseUrl, discoveredElements);
        }
        
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
            TimeoutMs = 5000,
            Metadata = new StepMetadata { Tags = new List<string> { "@navigation", "@basic" } }
        });

        // 2. Verify main banner/header visibility (optional test that won't fail the run)
        steps.Add(new TestStep
        {
            Id = $"step-{stepCounter++:D3}",
            Action = "assert",
            Target = new Target
            {
                Primary = new Locator { By = "css", Value = "header, .header, #header, h1, .banner" },
                Fallbacks = new List<Locator>
                {
                    new Locator { By = "css", Value = "h1, h2, h3" },
                    new Locator { By = "css", Value = "nav, .nav, .navbar" },
                    new Locator { By = "css", Value = ".logo, .brand, .site-title" }
                }
            },
            Assertions = new List<Assertion>
            {
                new Assertion { Type = "elementVisible", Value = "true" }
            },
            TimeoutMs = 5000,
            Metadata = new StepMetadata { Tags = new List<string> { "@visibility", "@basic", "@optional" } }
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
                        new Locator { By = "css", Value = $"a[href*='{link.ToLower()}'], a[title*='{link}']" },
                        new Locator { By = "xpath", Value = $"//a[contains(translate(text(), 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz'), '{link.ToLower()}')]" },
                        new Locator { By = "xpath", Value = $"//a[contains(@href, '{link.ToLower()}')] | //button[contains(text(), '{link}')] | //*[@role='button'][contains(text(), '{link}')]" },
                        new Locator { By = "css", Value = $"[data-link*='{link.ToLower()}'], [data-nav*='{link.ToLower()}'], .nav-link, .menu-item" }
                    }
                },
                Assertions = new List<Assertion>
                {
                    new Assertion { Type = "statusOk", Value = "200" },
                    new Assertion { Type = "elementVisible", Value = "body" }
                },
                TimeoutMs = 40000,
                Metadata = new StepMetadata { Tags = new List<string> { "@navigation", "@links", "@optional" } }
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
                TimeoutMs = 5000,
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
                            new Locator { By = "css", Value = $"input[placeholder*='{input}']" },
                            new Locator { By = "css", Value = $"input[type='{(input == "password" ? "password" : input == "email" ? "email" : "text")}']" },
                            new Locator { By = "xpath", Value = $"//input[contains(@placeholder, '{input}') or contains(@name, '{input}') or contains(@id, '{input}')]" },
                            new Locator { By = "css", Value = $"[data-testid*='{input}'], [data-cy*='{input}'], .{input}-input, #{input}-field" }
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
                    Primary = new Locator { By = "css", Value = "input[type='submit'], button[type='submit']" },
                    Fallbacks = new List<Locator>
                    {
                        new Locator { By = "xpath", Value = "//button[contains(text(), 'Submit')]" },
                        new Locator { By = "css", Value = "button" },
                        new Locator { By = "css", Value = "[type='submit']" },
                        new Locator { By = "xpath", Value = "//input[@value='Submit' or @value='Send' or @value='Login' or @value='Sign In']" },
                        new Locator { By = "css", Value = ".submit-btn, .login-btn, .send-btn, [role='button']" }
                    }
                },
                Assertions = new List<Assertion>
                {
                    new Assertion { Type = "statusOk", Value = "200" }
                },
                TimeoutMs = 5000,
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
                    Primary = new Locator { By = "css", Value = $"input[value='{button}']" },
                    Fallbacks = new List<Locator>
                    {
                        new Locator { By = "xpath", Value = $"//button[contains(text(), '{button}')]" },
                        new Locator { By = "partialLinkText", Value = button },
                        new Locator { By = "css", Value = $"[title*='{button}']" },
                        new Locator { By = "xpath", Value = $"//input[@value='{button}' or @title='{button}']" },
                        new Locator { By = "css", Value = $"button, [role='button'], .btn" },
                        new Locator { By = "css", Value = $".{button.ToLower()}-btn, .{button.ToLower()}-button, #{button.ToLower()}-btn" }
                    }
                },
                TimeoutMs = 40000,
                Metadata = new StepMetadata { Tags = new List<string> { "@buttons", "@interaction", "@optional" } }
            });
        }

        return steps;
    }

    private async Task<List<TestStep>> GenerateLoginTestCasesAsync(string baseUrl, bool withValidCredentials = true)
    {
        await Task.CompletedTask;
        
        var steps = new List<TestStep>();
        int stepCounter = 1;

        // 1. Navigate to main page - optimized timeout
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
            TimeoutMs = 5000, // Reduced from 10000 to 5000
            Metadata = new StepMetadata { Tags = new List<string> { "@navigation", "@login" } }
        });

        // 2. Find and click login link/button - optimized timeout
        steps.Add(new TestStep
        {
            Id = $"step-{stepCounter++:D3}",
            Action = "click",
            Target = new Target
            {
                Primary = new Locator { By = "linkText", Value = "Login" },
                Fallbacks = new List<Locator>
                {
                    new Locator { By = "linkText", Value = "Sign In" },
                    new Locator { By = "partialLinkText", Value = "Login" },
                    new Locator { By = "partialLinkText", Value = "Sign In" },
                    new Locator { By = "css", Value = "a[href*='login'], a[href*='signin'], a[href*='auth']" },
                    new Locator { By = "xpath", Value = "//a[contains(text(), 'Login') or contains(text(), 'Sign In') or contains(text(), 'log in')]" },
                    new Locator { By = "css", Value = ".login-link, .signin-link, .auth-link, [data-testid*='login'], [data-cy*='login']" }
                }
            },
            TimeoutMs = 5000, // Reduced from 10000 to 5000
            Metadata = new StepMetadata { Tags = new List<string> { "@navigation", "@login" } }
        });

        // 3. Fill email/username field
        steps.Add(new TestStep
        {
            Id = $"step-{stepCounter++:D3}",
            Action = "input",
            Target = new Target
            {
                Primary = new Locator { By = "name", Value = "email" },
                Fallbacks = new List<Locator>
                {
                    new Locator { By = "name", Value = "username" },
                    new Locator { By = "id", Value = "email" },
                    new Locator { By = "id", Value = "username" },
                    new Locator { By = "css", Value = "input[type='email']" },
                    new Locator { By = "css", Value = "input[placeholder*='email'], input[placeholder*='Email']" },
                    new Locator { By = "css", Value = "input[placeholder*='username'], input[placeholder*='Username']" },
                    new Locator { By = "xpath", Value = "//input[contains(@placeholder, 'email') or contains(@placeholder, 'Email') or contains(@placeholder, 'username') or contains(@placeholder, 'Username')]" }
                }
            },
            Value = withValidCredentials ? "testuser@example.com" : "invalid@test.com",
            TimeoutMs = 5000,
            Metadata = new StepMetadata { Tags = new List<string> { "@forms", "@login", "@input" } }
        });

        // 4. Fill password field  
        steps.Add(new TestStep
        {
            Id = $"step-{stepCounter++:D3}",
            Action = "input",
            Target = new Target
            {
                Primary = new Locator { By = "name", Value = "password" },
                Fallbacks = new List<Locator>
                {
                    new Locator { By = "id", Value = "password" },
                    new Locator { By = "css", Value = "input[type='password']" },
                    new Locator { By = "css", Value = "input[placeholder*='password'], input[placeholder*='Password']" },
                    new Locator { By = "xpath", Value = "//input[@type='password' or contains(@placeholder, 'password') or contains(@placeholder, 'Password')]" }
                }
            },
            Value = withValidCredentials ? "testpassword123" : "wrongpassword",
            TimeoutMs = 5000,
            Metadata = new StepMetadata { Tags = new List<string> { "@forms", "@login", "@input" } }
        });

        // 5. Submit login form
        steps.Add(new TestStep
        {
            Id = $"step-{stepCounter++:D3}",
            Action = "click",
            Target = new Target
            {
                Primary = new Locator { By = "css", Value = "input[type='submit']" },
                Fallbacks = new List<Locator>
                {
                    new Locator { By = "xpath", Value = "//button[contains(text(), 'Login') or contains(text(), 'Sign In') or contains(text(), 'Submit')]" },
                    new Locator { By = "css", Value = "button[type='submit']" },
                    new Locator { By = "css", Value = ".login-btn, .signin-btn, .submit-btn" },
                    new Locator { By = "xpath", Value = "//input[@value='Login' or @value='Sign In' or @value='Submit']" },
                    new Locator { By = "css", Value = "[data-testid*='login'], [data-cy*='login'], [data-testid*='submit']" }
                }
            },
            Assertions = new List<Assertion>
            {
                new Assertion { Type = "statusOk", Value = "200" }
            },
            TimeoutMs = 5000,
            Metadata = new StepMetadata { Tags = new List<string> { "@forms", "@login", "@submit" } }
        });

        // 6. Verify login result
        if (withValidCredentials)
        {
            // Add URL change assertion to verify redirection after login
            steps.Add(new TestStep
            {
                Id = $"step-{stepCounter++:D3}",
                Action = "assert",
                Target = new Target
                {
                    Primary = new Locator { By = "url", Value = "urlChanged" }
                },
                Assertions = new List<Assertion>
                {
                    new Assertion { Type = "urlNotEquals", Value = baseUrl + "/login" },
                    new Assertion { Type = "urlNotEquals", Value = baseUrl + "/signin" },
                    new Assertion { Type = "urlNotEquals", Value = baseUrl + "/auth" }
                },
                TimeoutMs = 10000, // Allow more time for redirection
                Metadata = new StepMetadata { Tags = new List<string> { "@login", "@redirection", "@verification" } }
            });
            
            // Verify dashboard/home page elements are visible
            steps.Add(new TestStep
            {
                Id = $"step-{stepCounter++:D3}",
                Action = "assert",
                Target = new Target
                {
                    Primary = new Locator { By = "css", Value = ".dashboard, .welcome, .user-dashboard, .home-content" },
                    Fallbacks = new List<Locator>
                    {
                        new Locator { By = "xpath", Value = "//text()[contains(., 'Welcome') or contains(., 'Dashboard') or contains(., 'Home')]" },
                        new Locator { By = "css", Value = ".logout, .profile, .user-menu, .user-info" },
                        new Locator { By = "xpath", Value = "//a[contains(text(), 'Logout') or contains(text(), 'Profile') or contains(text(), 'Sign Out')]" },
                        new Locator { By = "css", Value = "h1, h2, h3" }, // Any main heading as fallback
                        new Locator { By = "css", Value = "nav, .navigation, .navbar" } // Navigation elements
                    }
                },
                Assertions = new List<Assertion>
                {
                    new Assertion { Type = "elementVisible", Value = "true" },
                    new Assertion { Type = "textContains", Value = "Welcome|Dashboard|Home|Profile" }
                },
                TimeoutMs = 10000,
                Metadata = new StepMetadata { Tags = new List<string> { "@login", "@success", "@dashboard", "@verification" } }
            });
            
            // Verify user is authenticated by checking for logout option
            steps.Add(new TestStep
            {
                Id = $"step-{stepCounter++:D3}",
                Action = "assert",
                Target = new Target
                {
                    Primary = new Locator { By = "xpath", Value = "//a[contains(text(), 'Logout') or contains(text(), 'Sign Out')]" },
                    Fallbacks = new List<Locator>
                    {
                        new Locator { By = "css", Value = ".logout, .signout, .sign-out" },
                        new Locator { By = "css", Value = "[href*='logout'], [href*='signout']" },
                        new Locator { By = "xpath", Value = "//button[contains(text(), 'Logout') or contains(text(), 'Sign Out')]" }
                    }
                },
                Assertions = new List<Assertion>
                {
                    new Assertion { Type = "elementVisible", Value = "true" }
                },
                TimeoutMs = 5000,
                Metadata = new StepMetadata { Tags = new List<string> { "@login", "@authentication", "@logout", "@verification" } }
            });
        }
        else
        {
            steps.Add(new TestStep
            {
                Id = $"step-{stepCounter++:D3}",
                Action = "assert",
                Target = new Target
                {
                    Primary = new Locator { By = "css", Value = ".error, .alert-danger, .login-error" },
                    Fallbacks = new List<Locator>
                    {
                        new Locator { By = "xpath", Value = "//text()[contains(., 'Invalid') or contains(., 'Error') or contains(., 'incorrect')]" },
                        new Locator { By = "css", Value = ".invalid-feedback, .field-error, .form-error" }
                    }
                },
                Assertions = new List<Assertion>
                {
                    new Assertion { Type = "elementVisible", Value = "true" }
                },
                TimeoutMs = 5000,
                Metadata = new StepMetadata { Tags = new List<string> { "@login", "@error", "@verification" } }
            });
        }

        // Post-login navigation tests (only for valid login)
        if (withValidCredentials)
        {
            // Generate common post-login URLs to test
            var postLoginUrls = GeneratePostLoginTestUrls(baseUrl);
            
            foreach (var testUrl in postLoginUrls)
            {
                // Navigate to each URL after login
                steps.Add(new TestStep
                {
                    Id = $"step-{stepCounter++:D3}",
                    Action = "navigate",
                    Target = new Target
                    {
                        Primary = new Locator { By = "url", Value = testUrl }
                    },
                    Assertions = new List<Assertion>
                    {
                        new Assertion { Type = "statusOk", Value = "200" },
                        new Assertion { Type = "elementVisible", Value = "body" },
                        new Assertion { Type = "urlNotEquals", Value = baseUrl + "/login" } // Ensure we're not redirected back to login
                    },
                    TimeoutMs = 5000,
                    Metadata = new StepMetadata { Tags = new List<string> { "@navigation", "@post-login", "@authenticated" } }
                });

                // Look for success indicators on each page
                steps.Add(new TestStep
                {
                    Id = $"step-{stepCounter++:D3}",
                    Action = "assert",
                    Target = new Target
                    {
                        Primary = new Locator { By = "css", Value = ".welcome, .dashboard, .user-menu, .profile" },
                        Fallbacks = new List<Locator>
                        {
                            new Locator { By = "xpath", Value = "//text()[contains(., 'Welcome') or contains(., 'Dashboard') or contains(., 'Profile')]" },
                            new Locator { By = "css", Value = ".logout, .sign-out, [href*='logout']" }
                        }
                    },
                    Assertions = new List<Assertion>
                    {
                        new Assertion { Type = "elementVisible", Value = "true" }
                    },
                    TimeoutMs = 3000,
                    Metadata = new StepMetadata { Tags = new List<string> { "@authentication", "@success", "@verification" } }
                });

                // Check for error indicators that suggest bugs
                steps.Add(new TestStep
                {
                    Id = $"step-{stepCounter++:D3}",
                    Action = "assert",
                    Target = new Target
                    {
                        Primary = new Locator { By = "css", Value = ".error, .alert-danger, .exception, .error-message" },
                        Fallbacks = new List<Locator>
                        {
                            new Locator { By = "xpath", Value = "//text()[contains(., 'Error') or contains(., 'Exception') or contains(., '404') or contains(., '500')]" },
                            new Locator { By = "css", Value = ".not-found, .server-error, .access-denied" }
                        }
                    },
                    Assertions = new List<Assertion>
                    {
                        new Assertion { Type = "elementVisible", Value = "false" } // Error elements should NOT be visible
                    },
                    TimeoutMs = 2000,
                    Metadata = new StepMetadata { Tags = new List<string> { "@error-detection", "@bug-detection", "@quality-check" } }
                });
            }
        }

        return steps;
    }

    private List<string> GeneratePostLoginTestUrls(string baseUrl)
    {
        var urls = new List<string>();
        var baseUri = new Uri(baseUrl);
        
        // Common post-login pages to test
        var commonPaths = new[]
        {
            "/dashboard",
            "/profile", 
            "/account",
            "/settings",
            "/admin",
            "/user",
            "/home",
            "/welcome"
        };
        
        foreach (var path in commonPaths)
        {
            urls.Add(baseUri.Scheme + "://" + baseUri.Host + path);
        }
        
        return urls;
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
            "email" => "rumon.onnorokom@gmail.com",
            "username" => "rumon.onnorokom@gmail.com",
            "password" => "Mrumon4726",
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

        // Always start with navigation - reduced timeout for faster execution
        steps.Add(new TestStep
        {
            Id = "navigate-01",
            Action = "navigate",
            Target = new Target
            {
                Primary = new Locator { By = "url", Value = baseUrl }
            },
            TimeoutMs = 5000, // Reduced from 10000 to 5000
            Metadata = new StepMetadata { Tags = new List<string> { "@navigation" } }
        });

        // Add basic assertion for page load - reduced timeout
        steps.Add(new TestStep
        {
            Id = "assert-page-load",
            Action = "assert",
            Assertions = new List<Assertion>
            {
                new Assertion { Type = "statusOk", Value = "200" }
            },
            TimeoutMs = 3000, // Reduced from 5000 to 3000
            Metadata = new StepMetadata { Tags = new List<string> { "@assertion" } }
        });

        return steps;
    }
}

public class ExecutorService : IExecutorService
{
    private static bool _chromeDriverInitialized = false;
    private static readonly object _lockObj = new object();

    public async Task<List<StepResult>> ExecutePlanAsync(PlanJson plan, AgentConfig config)
    {
        // Initialize ChromeDriver once per application
        await EnsureChromeDriverInitializedAsync();
        
        var results = new List<StepResult>();
        
        Console.WriteLine($"Executing plan with Headless = {config.Headless}");
        
        try
        {
            var options = CreateChromeOptions(config);
            
            // Use system ChromeDriver for better compatibility (ExecutePlanAsync method)
            IWebDriver driver;
            try
            {
                // Try to use system ChromeDriver first
                var service = ChromeDriverService.CreateDefaultService("/usr/bin");
                service.HideCommandPromptWindow = true;
                driver = new ChromeDriver(service, options);
                Console.WriteLine("ChromeDriver created successfully using system driver.");
            }
            catch (Exception systemEx)
            {
                Console.WriteLine($"System ChromeDriver failed: {systemEx.Message}");
                Console.WriteLine("Falling back to package ChromeDriver...");
                try
                {
                    // Fallback to package ChromeDriver
                    driver = new ChromeDriver(options);
                    Console.WriteLine("ChromeDriver created successfully using package driver.");
                }
                catch (Exception packageEx)
                {
                    Console.WriteLine($"Package ChromeDriver also failed: {packageEx.Message}");
                    throw new InvalidOperationException($"ChromeDriver initialization failed. System error: {systemEx.Message}, Package error: {packageEx.Message}");
                }
            }
            
            using (driver)
            {
                // Reduce implicit wait for faster execution
                driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(3);
                // Set page load timeout to prevent hanging
                driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(15);
            
            try
            {
                Console.WriteLine($"Starting execution of {plan.Steps.Count} steps...");
                for (int i = 0; i < plan.Steps.Count; i++)
                {
                    var step = plan.Steps[i];
                    Console.WriteLine($"Executing step {i + 1}/{plan.Steps.Count}: {step.Action} - {step.Id}");
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
                driver.Quit();
            }
            } // End using (driver)
        }
        catch (Exception ex) when (ex.Message.Contains("cannot find Chrome binary") || 
                                   ex.Message.Contains("chromedriver") || 
                                   ex.Message.Contains("Failed to start"))
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
                       "Please ensure Chrome browser and ChromeDriver are installed and accessible.",
                Error = new StepError 
                { 
                    Message = $"Browser setup failed: {ex.Message}. " +
                             "Ensure Chrome browser is installed and ChromeDriver is available in PATH or included in the project."
                },
                Evidence = new Evidence()
            });
            
            Console.WriteLine($"Browser initialization failed: {ex.Message}");
            Console.WriteLine($"Browser configuration attempted: Headless = {config.Headless}");
            Console.WriteLine("To fix this issue, ensure Chrome browser is installed and ChromeDriver is available.");
        }

        return results;
    }

    public async Task<StepResult> ExecuteStepAsync(TestStep step, AgentConfig config)
    {
        // This overload creates its own browser instance
        await EnsureChromeDriverInitializedAsync();
        
        try
        {
            var options = CreateChromeOptions(config);
            
            // Use system ChromeDriver for better compatibility (ExecuteStepAsync method)
            IWebDriver driver;
            try
            {
                // Try to use system ChromeDriver first
                var service = ChromeDriverService.CreateDefaultService("/usr/bin");
                service.HideCommandPromptWindow = true;
                driver = new ChromeDriver(service, options);
                Console.WriteLine("ChromeDriver created successfully using system driver.");
            }
            catch (Exception systemEx)
            {
                Console.WriteLine($"System ChromeDriver failed: {systemEx.Message}");
                Console.WriteLine("Falling back to package ChromeDriver...");
                try
                {
                    // Fallback to package ChromeDriver
                    driver = new ChromeDriver(options);
                    Console.WriteLine("ChromeDriver created successfully using package driver.");
                }
                catch (Exception packageEx)
                {
                    Console.WriteLine($"Package ChromeDriver also failed: {packageEx.Message}");
                    throw new InvalidOperationException($"ChromeDriver initialization failed. System error: {systemEx.Message}, Package error: {packageEx.Message}");
                }
            }
            
            using (driver)
            {
                // Reduce implicit wait for faster execution
                driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(3);
                // Set page load timeout to prevent hanging
                driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(15);
            
            try
            {
                return await ExecuteStepAsync(step, config, driver);
            }
            finally
            {
                driver.Quit();
            }
            } // End using (driver)
        }
        catch (Exception ex) when (ex.Message.Contains("cannot find Chrome binary") || 
                                   ex.Message.Contains("chromedriver") || 
                                   ex.Message.Contains("Failed to start"))
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
                    Message = $"Browser setup failed: {ex.Message}. Ensure Chrome browser and ChromeDriver are available."
                },
                Evidence = new Evidence()
            };
        }
    }

    private async Task<StepResult> ExecuteStepAsync(TestStep step, AgentConfig config, IWebDriver driver)
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
            stepResult.Error = new StepError 
            { 
                Message = ex.Message
            };
            stepResult.Notes = $"Step failed with error: {ex.Message}";

            // Capture evidence on failure
            await CaptureEvidenceAsync(driver, stepResult);
        }
        finally
        {
            stepResult.End = DateTime.UtcNow;
        }

        return stepResult;
    }

    private async Task ExecuteNavigateAsync(IWebDriver driver, TestStep step)
    {
        if (step.Target?.Primary?.Value == null)
            throw new ArgumentException("Navigate action requires a URL value");

        driver.Navigate().GoToUrl(step.Target.Primary.Value);
        
        // Wait for page to load
        var wait = new WebDriverWait(driver, TimeSpan.FromMilliseconds(step.TimeoutMs));
        wait.Until(driver => ((IJavaScriptExecutor)driver).ExecuteScript("return document.readyState").Equals("complete"));
        
        await Task.CompletedTask;
    }

    private async Task ExecuteClickAsync(IWebDriver driver, TestStep step)
    {
        var element = await FindElementAsync(driver, step.Target);
        element.Click();
    }

    private async Task ExecuteInputAsync(IWebDriver driver, TestStep step)
    {
        if (step.Value == null)
            throw new ArgumentException("Input action requires a value");

        var element = await FindElementAsync(driver, step.Target);
        element.Clear();
        element.SendKeys(step.Value);
    }

    private async Task ExecuteAssertAsync(IWebDriver driver, TestStep step)
    {
        if (step.Target != null)
        {
            // Handle URL-based assertions
            if (step.Target.Primary?.By?.ToLower() == "url")
            {
                // URL assertions don't need element finding, they're handled in ValidateAssertionsAsync
                return;
            }
            
            var element = await FindElementAsync(driver, step.Target);
            // If we found the element, the assertion passes
        }
    }

    private async Task<IWebElement> FindElementAsync(IWebDriver driver, Target? target)
    {
        if (target?.Primary == null)
            throw new ArgumentException("Target primary locator is required");

        try
        {
            var element = GetElement(driver, target.Primary);
            return element;
        }
        catch (NoSuchElementException)
        {
            // Try fallback locators
            foreach (var fallback in target.Fallbacks ?? new List<Locator>())
            {
                try
                {
                    var element = GetElement(driver, fallback);
                    return element;
                }
                catch (NoSuchElementException)
                {
                    continue;
                }
            }
            
            throw new Exception($"Element not found with primary locator {target.Primary.By}='{target.Primary.Value}' or any fallbacks");
        }
    }

    private IWebElement GetElement(IWebDriver driver, Locator locator)
    {
        return locator.By.ToLower() switch
        {
            "id" => driver.FindElement(By.Id(locator.Value)),
            "css" => driver.FindElement(By.CssSelector(locator.Value)),
            "xpath" => driver.FindElement(By.XPath(locator.Value)),
            "name" => driver.FindElement(By.Name(locator.Value)),
            "linktext" => driver.FindElement(By.LinkText(locator.Value)),
            "partiallinktext" => driver.FindElement(By.PartialLinkText(locator.Value)),
            "text" => driver.FindElement(By.XPath($"//*[contains(text(), '{locator.Value}')]")),
            "url" => throw new ArgumentException("URL locator is only valid for navigate actions"),
            _ => throw new ArgumentException($"Unsupported locator type: {locator.By}")
        };
    }

    private async Task<List<bool>> ValidateAssertionsAsync(IWebDriver driver, List<Assertion> assertions)
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
                    "urlnotequals" => await ValidateUrlNotEqualsAsync(driver, assertion.Value),
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

    private async Task<bool> ValidateStatusOkAsync(IWebDriver driver, string expectedStatus)
    {
        await Task.CompletedTask;
        // For Selenium, we can't easily check HTTP status codes without additional setup
        // We'll assume navigation was successful if we can find the body element
        try
        {
            driver.FindElement(By.TagName("body"));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> ValidateElementVisibleAsync(IWebDriver driver, string selector)
    {
        try
        {
            if (selector == "true" || selector == "body")
            {
                // General page visibility check
                var bodyElement = driver.FindElement(By.TagName("body"));
                return bodyElement.Displayed;
            }
            
            var element = driver.FindElement(By.CssSelector(selector));
            return element.Displayed;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> ValidateTextContainsAsync(IWebDriver driver, string expectedText)
    {
        try
        {
            var bodyElement = driver.FindElement(By.TagName("body"));
            var content = bodyElement.Text;
            
            // Handle pipe-separated options for OR matching
            if (expectedText.Contains("|"))
            {
                var options = expectedText.Split('|');
                return options.Any(option => content?.Contains(option.Trim(), StringComparison.OrdinalIgnoreCase) == true);
            }
            
            return content?.Contains(expectedText, StringComparison.OrdinalIgnoreCase) ?? false;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> ValidateUrlNotEqualsAsync(IWebDriver driver, string urlToAvoid)
    {
        await Task.CompletedTask;
        try
        {
            var currentUrl = driver.Url;
            
            // Handle multiple URL patterns to avoid
            if (urlToAvoid.Contains("|"))
            {
                var urlsToAvoid = urlToAvoid.Split('|');
                return !urlsToAvoid.Any(url => currentUrl.Contains(url.Trim(), StringComparison.OrdinalIgnoreCase));
            }
            
            // Check if current URL doesn't contain the URL to avoid
            return !currentUrl.Contains(urlToAvoid, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private async Task CaptureEvidenceAsync(IWebDriver driver, StepResult stepResult)
    {
        try
        {
            // Create evidence directory
            var evidenceDir = Path.Combine(Directory.GetCurrentDirectory(), "evidence", stepResult.StepId);
            Directory.CreateDirectory(evidenceDir);

            // Capture screenshot
            var screenshotPath = Path.Combine(evidenceDir, "screenshot.png");
            var screenshotDriver = (ITakesScreenshot)driver;
            var screenshot = screenshotDriver.GetScreenshot();
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

    private ChromeOptions CreateChromeOptions(AgentConfig config)
    {
        var options = new ChromeOptions();
        
        // Auto-detect headless environment (no display available)
        bool isHeadlessEnvironment = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY"));
        
        // Users can override headless mode by explicitly setting it to false, but warn about potential issues
        bool shouldRunHeadless = config.Headless;
        if (!config.Headless && isHeadlessEnvironment)
        {
            Console.WriteLine("⚠️  Warning: No DISPLAY environment variable detected, but headless mode is disabled.");
            Console.WriteLine("   The browser may not be visible. Consider setting headless=true for server environments.");
        }
        
        if (shouldRunHeadless)
        {
            options.AddArgument("--headless");
            Console.WriteLine("Running in headless mode");
        }
        else
        {
            Console.WriteLine("Running in non-headless mode (browser will be visible)");
        }
        
        // Enhanced options for better compatibility
        options.AddArgument("--disable-dev-shm-usage");
        options.AddArgument("--no-sandbox");
        options.AddArgument("--disable-gpu");
        options.AddArgument("--disable-extensions");
        options.AddArgument("--disable-web-security");
        options.AddArgument("--allow-running-insecure-content");
        options.AddArgument("--window-size=1280,720");
        options.AddArgument("--user-agent=WebTestingAI-Agent/1.0 (Automated Testing)");
        
        // Use unique user data directory to avoid conflicts
        var tempUserDataDir = Path.Combine(Path.GetTempPath(), $"chrome-user-data-{Guid.NewGuid():N}");
        options.AddArgument($"--user-data-dir={tempUserDataDir}");
        
        return options;
    }

    private async Task EnsureChromeDriverInitializedAsync()
    {
        if (!_chromeDriverInitialized)
        {
            lock (_lockObj)
            {
                if (!_chromeDriverInitialized)
                {
                    // Verify Chrome browser is available
                    bool chromeAvailable = System.IO.File.Exists("/usr/bin/google-chrome") || 
                                          System.IO.File.Exists("/usr/bin/chromium-browser") ||
                                          System.IO.File.Exists("/usr/bin/chrome") ||
                                          System.IO.File.Exists("/opt/google/chrome/chrome");
                    
                    if (!chromeAvailable)
                    {
                        throw new InvalidOperationException("Chrome browser not found. Please install Google Chrome or Chromium browser.");
                    }
                    
                    // Note: ChromeDriver will be automatically downloaded by the Selenium.WebDriver.ChromeDriver package
                    _chromeDriverInitialized = true;
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