using System.Collections.Concurrent;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using WebTestingAiAgent.Core.Interfaces;
using WebTestingAiAgent.Core.Models;

namespace WebTestingAiAgent.Api.Services;

public class RecordingService : IRecordingService
{
    private readonly ConcurrentDictionary<string, RecordingSession> _sessions = new();
    private readonly ConcurrentDictionary<string, Timer> _recordingTimers = new();
    private readonly IBrowserAutomationService _browserService;
    private readonly IServiceProvider _serviceProvider; // For getting TestCaseService when needed

    public RecordingService(IBrowserAutomationService browserService, IServiceProvider serviceProvider)
    {
        _browserService = browserService;
        _serviceProvider = serviceProvider;
    }

    public async Task<RecordingSession> StartRecordingAsync(StartRecordingRequest request)
    {
        var session = new RecordingSession
        {
            Id = Guid.NewGuid().ToString(),
            Name = request.Name,
            BaseUrl = request.BaseUrl,
            Status = RecordingStatus.Recording,
            StartedAt = DateTime.UtcNow,
            Settings = request.Settings ?? new RecordingSettings()
        };

        _sessions[session.Id] = session;

        // Start browser session for recording
        var browserSessionId = await _browserService.StartBrowserSessionAsync(
            request.BaseUrl, 
            new ExecutionSettings 
            { 
                Browser = "chrome", 
                Headless = session.Settings.Headless, // Use headless setting from recording settings
                TimeoutMs = session.Settings.TimeoutMs,
                BrowserInitTimeoutMs = 30000 // Extended timeout for recording sessions
            },
            session.Settings.ForceVisible); // Pass force visible flag

        // Store browser session ID in metadata
        session.Steps.Add(new RecordedStep
        {
            Order = 0,
            Action = "session_start",
            Metadata = new Dictionary<string, object> { ["browserSessionId"] = browserSessionId }
        });

        // Start interaction collection timer
        StartInteractionCollectionTimer(session.Id, browserSessionId);

        // Start recording duration timer
        StartRecordingDurationTimer(session.Id);

        return session;
    }

    public async Task<RecordingSession?> GetRecordingSessionAsync(string sessionId)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return await Task.FromResult(session);
    }

    public async Task<List<RecordingSessionResponse>> GetActiveRecordingSessionsAsync()
    {
        var activeSessions = _sessions.Values
            .Where(s => s.Status == RecordingStatus.Recording || s.Status == RecordingStatus.Paused)
            .Select(s => new RecordingSessionResponse
            {
                Id = s.Id,
                Name = s.Name,
                BaseUrl = s.BaseUrl,
                Status = s.Status,
                StepCount = s.Steps.Count,
                StartedAt = s.StartedAt,
                EndedAt = s.EndedAt,
                RecordingDuration = CalculateCurrentDuration(s)
            })
            .ToList();

        return await Task.FromResult(activeSessions);
    }

    public async Task<RecordingSession> StopRecordingAsync(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            throw new ArgumentException($"Recording session {sessionId} not found");

        session.Status = RecordingStatus.Stopped;
        session.EndedAt = DateTime.UtcNow;
        
        // Update final recording duration
        UpdateRecordingDuration(session);

        // Stop timers
        if (_recordingTimers.TryRemove(sessionId, out var timer))
        {
            timer.Dispose();
        }

        // Collect any remaining captured interactions
        var browserSessionId = GetBrowserSessionId(session);
        if (!string.IsNullOrEmpty(browserSessionId))
        {
            try
            {
                var capturedSteps = await _browserService.CollectCapturedInteractionsAsync(browserSessionId);
                foreach (var step in capturedSteps)
                {
                    step.Order = session.Steps.Count;
                    session.Steps.Add(step);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error collecting final interactions: {ex.Message}");
            }

            // Stop browser session
            await _browserService.StopBrowserSessionAsync(browserSessionId);
        }

        // Auto-execute if enabled
        if (session.Settings.AutoExecuteAfterRecording && session.Steps.Count > 1) // More than just session_start
        {
            _ = Task.Run(async () => await AutoExecuteRecordingAsync(session));
        }

        return session;
    }

    public async Task<RecordingSession> PauseRecordingAsync(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            throw new ArgumentException($"Recording session {sessionId} not found");

        if (session.Status != RecordingStatus.Recording)
            throw new InvalidOperationException($"Cannot pause session in status {session.Status}");

        session.Status = RecordingStatus.Paused;
        session.PausedAt = DateTime.UtcNow;
        
        // Update recording duration
        UpdateRecordingDuration(session);

        // Pause browser interaction capture
        var browserSessionId = GetBrowserSessionId(session);
        if (!string.IsNullOrEmpty(browserSessionId))
        {
            await _browserService.SetCaptureStateAsync(browserSessionId, false);
        }

        return await Task.FromResult(session);
    }

    public async Task<RecordingSession> ResumeRecordingAsync(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            throw new ArgumentException($"Recording session {sessionId} not found");

        if (session.Status != RecordingStatus.Paused)
            throw new InvalidOperationException($"Cannot resume session in status {session.Status}");

        session.Status = RecordingStatus.Recording;
        session.PausedAt = null;

        // Resume browser interaction capture
        var browserSessionId = GetBrowserSessionId(session);
        if (!string.IsNullOrEmpty(browserSessionId))
        {
            await _browserService.SetCaptureStateAsync(browserSessionId, true);
        }

        return await Task.FromResult(session);
    }

    public async Task<RecordedStep> AddStepAsync(string sessionId, RecordedStep step)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            throw new ArgumentException($"Recording session {sessionId} not found");

        if (session.Status != RecordingStatus.Recording)
            throw new InvalidOperationException($"Cannot add steps to session in status {session.Status}");

        // Check step limit
        if (session.Steps.Count >= session.Settings.MaxSteps)
            throw new InvalidOperationException($"Maximum steps limit ({session.Settings.MaxSteps}) reached");

        // Avoid duplicate steps based on action, element, and timestamp proximity (within 500ms)
        var recentSteps = session.Steps.Where(s => 
            DateTime.UtcNow - s.Timestamp < TimeSpan.FromMilliseconds(500) &&
            s.Action == step.Action &&
            s.ElementSelector == step.ElementSelector
        ).ToList();
        
        if (recentSteps.Any())
        {
            // For input events, update the value instead of creating a new step
            if (step.Action == "input" && recentSteps.Any())
            {
                var lastInputStep = recentSteps.Last();
                lastInputStep.Value = step.Value;
                lastInputStep.Timestamp = DateTime.UtcNow;
                return lastInputStep;
            }
            // Skip duplicate non-input actions
            else if (step.Action != "input")
            {
                return recentSteps.Last();
            }
        }

        step.Order = session.Steps.Count;
        step.Timestamp = DateTime.UtcNow;
        step.Id = Guid.NewGuid().ToString();

        session.Steps.Add(step);
        Console.WriteLine($"Recording session {sessionId}: Added {step.Action} step ({session.Steps.Count} total steps)");
        return await Task.FromResult(step);
    }

    public async Task<TestCase> SaveAsTestCaseAsync(string sessionId, string testCaseName, string description = "")
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            throw new ArgumentException($"Recording session {sessionId} not found");

        // Collect any remaining captured interactions before saving
        var browserSessionId = GetBrowserSessionId(session);
        if (!string.IsNullOrEmpty(browserSessionId))
        {
            try
            {
                var capturedSteps = await _browserService.CollectCapturedInteractionsAsync(browserSessionId);
                foreach (var step in capturedSteps)
                {
                    // Set the correct order for the step
                    step.Order = session.Steps.Count;
                    session.Steps.Add(step);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to collect remaining interactions during save: {ex.Message}");
            }
        }

        var testCase = new TestCase
        {
            Name = testCaseName,
            Description = description,
            BaseUrl = session.BaseUrl,
            Steps = session.Steps.Where(s => s.Action != "session_start").ToList(),
            Tags = new List<string> { "recorded" },
            Format = TestCaseFormat.Json
        };

        var createRequest = new CreateTestCaseRequest
        {
            Name = testCaseName,
            Description = description,
            BaseUrl = session.BaseUrl,
            Tags = testCase.Tags,
            Format = testCase.Format
        };

        // Get TestCaseService from service provider to avoid singleton dependency issues
        using var scope = _serviceProvider.CreateScope();
        var testCaseService = scope.ServiceProvider.GetRequiredService<ITestCaseService>();
        
        var createdTestCase = await testCaseService.CreateTestCaseAsync(createRequest);
        
        // Update the created test case with the recorded steps
        createdTestCase.Steps = testCase.Steps;
        
        // Mark session as completed
        session.Status = RecordingStatus.Completed;
        session.EndedAt = DateTime.UtcNow;

        Console.WriteLine($"Recording saved as test case '{testCaseName}' with {testCase.Steps.Count} steps");

        return createdTestCase;
    }

    public async Task<bool> DeleteRecordingSessionAsync(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            // Stop browser session if still active
            var browserSessionId = GetBrowserSessionId(session);
            if (!string.IsNullOrEmpty(browserSessionId))
            {
                try
                {
                    await _browserService.StopBrowserSessionAsync(browserSessionId);
                }
                catch
                {
                    // Ignore errors when stopping browser session during cleanup
                }
            }
        }

        var removed = _sessions.TryRemove(sessionId, out _);
        return await Task.FromResult(removed);
    }

    private string? GetBrowserSessionId(RecordingSession session)
    {
        var sessionStartStep = session.Steps.FirstOrDefault(s => s.Action == "session_start");
        return sessionStartStep?.Metadata.TryGetValue("browserSessionId", out var sessionId) == true 
            ? sessionId?.ToString() 
            : null;
    }

    private void StartInteractionCollectionTimer(string sessionId, string browserSessionId)
    {
        var timer = new Timer(async _ =>
        {
            if (_sessions.TryGetValue(sessionId, out var session) && session.Status == RecordingStatus.Recording)
            {
                try
                {
                    var capturedSteps = await _browserService.CollectCapturedInteractionsAsync(browserSessionId);
                    foreach (var step in capturedSteps)
                    {
                        await AddStepAsync(sessionId, step);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error collecting interactions for session {sessionId}: {ex.Message}");
                }
            }
        }, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)); // Collect every second

        _recordingTimers[sessionId] = timer;
    }

    private void StartRecordingDurationTimer(string sessionId)
    {
        var durationTimer = new Timer(_ =>
        {
            if (_sessions.TryGetValue(sessionId, out var session))
            {
                UpdateRecordingDuration(session);
                
                // Auto-stop if max duration reached
                if (session.RecordingDuration.TotalMinutes >= session.Settings.MaxRecordingMinutes)
                {
                    _ = Task.Run(async () => await StopRecordingAsync(sessionId));
                }
            }
        }, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)); // Update every second
    }

    private void UpdateRecordingDuration(RecordingSession session)
    {
        var endTime = session.PausedAt ?? session.EndedAt ?? DateTime.UtcNow;
        session.RecordingDuration = endTime - session.StartedAt;
    }

    private TimeSpan CalculateCurrentDuration(RecordingSession session)
    {
        var endTime = session.PausedAt ?? session.EndedAt ?? DateTime.UtcNow;
        return endTime - session.StartedAt;
    }

    private async Task AutoExecuteRecordingAsync(RecordingSession session)
    {
        try
        {
            // Convert recording to test case
            var testCase = new TestCase
            {
                Name = $"{session.Name}_AutoExecute",
                Description = $"Auto-execution of recording: {session.Name}",
                BaseUrl = session.BaseUrl,
                Steps = session.Steps.Where(s => s.Action != "session_start").ToList(),
                Tags = new List<string> { "recorded", "auto-execute" },
                Format = TestCaseFormat.Json
            };

            // TODO: Execute the test case using the test execution service
            // This would require injecting ITestExecutionService
            Console.WriteLine($"Auto-executing recorded test case: {testCase.Name}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error auto-executing recording {session.Id}: {ex.Message}");
        }
    }
}

public class BrowserAutomationService : IBrowserAutomationService
{
    private readonly ConcurrentDictionary<string, IWebDriver> _browserSessions = new();
    private readonly ConcurrentDictionary<string, BrowserInteractionCapture> _interactionCaptures = new();

    public async Task<string> StartBrowserSessionAsync(string baseUrl, ExecutionSettings settings, bool forceVisible = false)
    {
        var sessionId = Guid.NewGuid().ToString();
        
        var options = new ChromeOptions();
        bool useHeadless = settings.Headless;
        
        // Check if display is available for non-headless mode
        if (!useHeadless)
        {
            var display = Environment.GetEnvironmentVariable("DISPLAY");
            if (string.IsNullOrEmpty(display))
            {
                if (forceVisible)
                {
                    Console.WriteLine("⚠️  Warning: No DISPLAY environment variable found, but forceVisible=true for recording.");
                    Console.WriteLine("   Recording requires a visible browser for user interaction capture.");
                    
                    // Try to set up a virtual display for recording
                    if (TrySetupVirtualDisplay())
                    {
                        Console.WriteLine("✅ Virtual display setup successful - browser will be visible for recording");
                        useHeadless = false; // Use visible mode with virtual display
                    }
                    else
                    {
                        Console.WriteLine("   🔧 Solutions:");
                        Console.WriteLine("   - Run on a desktop environment with GUI (recommended)");
                        Console.WriteLine("   - Use X11 forwarding: ssh -X user@server");
                        Console.WriteLine("   - Use VNC or remote desktop for headless servers");
                        Console.WriteLine("   - Install Xvfb: apt-get install xvfb");
                        Console.WriteLine("   ⚠️  Falling back to headless mode - recorded interactions may be limited.");
                        useHeadless = true;
                    }
                }
                else
                {
                    Console.WriteLine("No DISPLAY environment variable found. Falling back to headless mode.");
                    useHeadless = true;
                }
            }
        }
        
        if (useHeadless)
        {
            options.AddArgument("--headless");
            Console.WriteLine("Running browser in headless mode");
        }
        else
        {
            Console.WriteLine("Running browser in visible mode for interaction recording");
            // Add options to improve the visible browser experience for recording
            options.AddArgument("--window-size=1280,720");
            // Remove --start-maximized as it can cause issues in virtual display environments
            options.AddArgument("--window-position=0,0");
        }
        
        // Standard Chrome options for automation
        options.AddArgument("--no-sandbox");
        options.AddArgument("--disable-dev-shm-usage");
        
        // Essential Chrome options for automation and virtual display compatibility
        options.AddArgument("--no-sandbox");
        options.AddArgument("--disable-dev-shm-usage");
        
        // Additional options for virtual display and container environments
        options.AddArgument("--disable-dbus");  // Fix D-Bus permission errors
        options.AddArgument("--disable-gpu"); // Disable GPU for stability in virtual environments
        options.AddArgument("--disable-software-rasterizer");
        options.AddArgument("--disable-extensions");
        options.AddArgument("--disable-plugins");
        options.AddArgument("--disable-default-apps");
        options.AddArgument("--no-first-run");
        options.AddArgument("--no-default-browser-check");
        options.AddArgument("--disable-background-timer-throttling");
        options.AddArgument("--disable-backgrounding-occluded-windows");
        options.AddArgument("--disable-renderer-backgrounding");
        
        // Use a unique remote debugging port to avoid conflicts
        options.AddArgument($"--remote-debugging-port={9222 + new Random().Next(100, 999)}");

        try
        {
            Console.WriteLine("Starting Chrome driver...");
            
            // Ensure DISPLAY environment variable is properly set for ChromeDriver
            var display = Environment.GetEnvironmentVariable("DISPLAY");
            if (!string.IsNullOrEmpty(display))
            {
                Console.WriteLine($"Using DISPLAY: {display}");
            }
            
            // Simplified ChromeDriver initialization with timeout
            // Simplified ChromeDriver initialization without Task.Run to get better error info
            var driverTask = Task.Run(() => {
                try
                {
                    Console.WriteLine("Creating ChromeDriver with auto-detected driver path...");
                    Console.WriteLine($"Chrome options: {string.Join(", ", options.Arguments)}");
                    
                    // Use default ChromeDriver constructor - this will automatically find chromedriver
                    // The Selenium.WebDriver.ChromeDriver package handles driver location and platform-specific executables
                    var driver = new ChromeDriver(options);
                    Console.WriteLine("ChromeDriver created successfully.");
                    
                    // Set timeouts
                    driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromMilliseconds(settings.TimeoutMs);
                    driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(10); // Shorter page load timeout
                    
                    return driver;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ChromeDriver creation failed: {ex.Message}");
                    Console.WriteLine($"Exception type: {ex.GetType().Name}");
                    if (ex.InnerException != null)
                    {
                        Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                    }
                    Console.WriteLine($"Stack trace: {ex.StackTrace}");
                    Console.WriteLine($"Common solutions:");
                    Console.WriteLine($"  1. Ensure Google Chrome is installed");
                    Console.WriteLine($"  2. ChromeDriver version matches Chrome version");
                    Console.WriteLine($"  3. ChromeDriver is in PATH or use Selenium.WebDriver.ChromeDriver package");
                    Console.WriteLine($"  4. Check DISPLAY environment and Xvfb setup");
                    throw;
                }
            });
            
            // Wait for driver creation with configurable timeout, extended for virtual display environments
            var baseTimeoutMs = settings.BrowserInitTimeoutMs;
            // Add extra time for virtual display environments which need more startup time
            var isVirtualDisplay = Environment.GetEnvironmentVariable("DISPLAY") == ":99";
            var initTimeoutMs = isVirtualDisplay ? Math.Max(baseTimeoutMs, 30000) : baseTimeoutMs;
            
            Console.WriteLine($"Using browser initialization timeout: {initTimeoutMs/1000} seconds" + 
                            (isVirtualDisplay ? " (extended for virtual display)" : ""));
            
            var timeoutTask = Task.Delay(initTimeoutMs);
            var completedTask = await Task.WhenAny(driverTask, timeoutTask);
            
            if (completedTask == timeoutTask)
            {
                throw new TimeoutException($"ChromeDriver initialization timed out after {initTimeoutMs/1000} seconds");
            }
            
            var driver = await driverTask;
            Console.WriteLine("Chrome driver started successfully.");
            
            _browserSessions[sessionId] = driver;
            
            Console.WriteLine($"Navigating to: {baseUrl}");
            // Navigate to base URL - handle network errors gracefully for testing
            try
            {
                driver.Navigate().GoToUrl(baseUrl);
                Console.WriteLine("Navigation completed successfully.");
            }
            catch (WebDriverException navEx) when (navEx.Message.Contains("ERR_NAME_NOT_RESOLVED") || 
                                                  navEx.Message.Contains("ERR_CONNECTION_REFUSED") ||
                                                  navEx.Message.Contains("ERR_NETWORK_ACCESS_DENIED"))
            {
                Console.WriteLine($"Navigation failed due to network restrictions: {navEx.Message}");
                Console.WriteLine("Continuing with session creation - browser is ready for interaction capture.");
                // Continue with session creation even if navigation fails due to network restrictions
            }
            
            // Set up interaction capture
            var interactionCapture = new BrowserInteractionCapture();
            _interactionCaptures[sessionId] = interactionCapture;
            
            // Inject the capturing script after navigation - handle errors gracefully
            await Task.Delay(1000); // Wait for page to load
            Console.WriteLine("Injecting capture script...");
            try
            {
                interactionCapture.InjectCapturingScript(driver);
                interactionCapture.StartCapturing();
                Console.WriteLine("Capture script injected successfully.");
            }
            catch (Exception jsEx)
            {
                Console.WriteLine($"Warning: Failed to inject capture script: {jsEx.Message}");
                Console.WriteLine("Session created but interaction capture may not work until a valid page is loaded.");
            }
            Console.WriteLine("Recording session started successfully.");
            
            return sessionId;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to start browser session: {ex.Message}");
            throw new InvalidOperationException($"Failed to start browser session: {ex.Message}", ex);
        }
    }

    public async Task StopBrowserSessionAsync(string sessionId)
    {
        // Stop interaction capture first
        if (_interactionCaptures.TryRemove(sessionId, out var capture))
        {
            capture.StopCapturing();
        }
        
        if (_browserSessions.TryRemove(sessionId, out var driver))
        {
            driver.Quit();
            driver.Dispose();
        }
        await Task.CompletedTask;
    }

    public async Task<RecordedStep> CaptureInteractionAsync(string sessionId, string eventType, Dictionary<string, object> eventData)
    {
        var step = new RecordedStep
        {
            Action = eventType,
            Timestamp = DateTime.UtcNow,
            Metadata = eventData
        };

        // Extract common properties based on event type
        if (eventData.TryGetValue("selector", out var selector))
            step.ElementSelector = selector.ToString();
        
        if (eventData.TryGetValue("value", out var value))
            step.Value = value.ToString();
        
        if (eventData.TryGetValue("url", out var url))
            step.Url = url.ToString();

        return await Task.FromResult(step);
    }

    public async Task<StepResult> ExecuteStepAsync(string sessionId, RecordedStep step)
    {
        if (!_browserSessions.TryGetValue(sessionId, out var driver))
            throw new ArgumentException($"Browser session {sessionId} not found");

        var stepResult = new StepResult
        {
            StepId = step.Id,
            Start = DateTime.UtcNow,
            Status = "running"
        };

        try
        {
            switch (step.Action.ToLower())
            {
                case "click":
                    if (!string.IsNullOrEmpty(step.ElementSelector))
                    {
                        var element = driver.FindElement(By.CssSelector(step.ElementSelector));
                        element.Click();
                    }
                    break;

                case "input":
                    if (!string.IsNullOrEmpty(step.ElementSelector) && !string.IsNullOrEmpty(step.Value))
                    {
                        var element = driver.FindElement(By.CssSelector(step.ElementSelector));
                        element.Clear();
                        element.SendKeys(step.Value);
                    }
                    break;

                case "select":
                    if (!string.IsNullOrEmpty(step.ElementSelector) && !string.IsNullOrEmpty(step.Value))
                    {
                        var element = driver.FindElement(By.CssSelector(step.ElementSelector));
                        var select = new SelectElement(element);
                        select.SelectByText(step.Value);
                    }
                    break;

                case "navigate":
                    if (!string.IsNullOrEmpty(step.Url))
                    {
                        driver.Navigate().GoToUrl(step.Url);
                    }
                    break;

                case "wait":
                    if (int.TryParse(step.Value, out var waitMs))
                    {
                        await Task.Delay(waitMs);
                    }
                    break;
            }

            stepResult.Status = "passed";
            stepResult.End = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            stepResult.Status = "failed";
            stepResult.End = DateTime.UtcNow;
            stepResult.Error = new StepError { Message = ex.Message };
        }

        return stepResult;
    }

    public async Task<byte[]> TakeScreenshotAsync(string sessionId)
    {
        if (!_browserSessions.TryGetValue(sessionId, out var driver))
            throw new ArgumentException($"Browser session {sessionId} not found");

        var screenshot = ((ITakesScreenshot)driver).GetScreenshot();
        return await Task.FromResult(screenshot.AsByteArray);
    }

    public async Task<string> GetPageSourceAsync(string sessionId)
    {
        if (!_browserSessions.TryGetValue(sessionId, out var driver))
            throw new ArgumentException($"Browser session {sessionId} not found");

        return await Task.FromResult(driver.PageSource);
    }

    public async Task<Dictionary<string, object>> GetElementInfoAsync(string sessionId, string selector)
    {
        if (!_browserSessions.TryGetValue(sessionId, out var driver))
            throw new ArgumentException($"Browser session {sessionId} not found");

        try
        {
            var element = driver.FindElement(By.CssSelector(selector));
            var info = new Dictionary<string, object>
            {
                ["tagName"] = element.TagName,
                ["text"] = element.Text,
                ["displayed"] = element.Displayed,
                ["enabled"] = element.Enabled,
                ["selected"] = element.Selected,
                ["location"] = new { x = element.Location.X, y = element.Location.Y },
                ["size"] = new { width = element.Size.Width, height = element.Size.Height }
            };

            return await Task.FromResult(info);
        }
        catch (Exception ex)
        {
            return await Task.FromResult(new Dictionary<string, object> { ["error"] = ex.Message });
        }
    }

    public async Task<List<RecordedStep>> CollectCapturedInteractionsAsync(string sessionId)
    {
        if (!_browserSessions.TryGetValue(sessionId, out var driver))
            throw new ArgumentException($"Browser session {sessionId} not found");

        if (!_interactionCaptures.TryGetValue(sessionId, out var capture))
            return new List<RecordedStep>();

        // Collect events from browser
        capture.CollectEventsFromBrowser(driver);
        
        // Return captured events
        return capture.GetCapturedEvents();
    }

    public async Task SetCaptureStateAsync(string sessionId, bool isCapturing)
    {
        if (!_browserSessions.TryGetValue(sessionId, out var driver))
            throw new ArgumentException($"Browser session {sessionId} not found");

        if (_interactionCaptures.TryGetValue(sessionId, out var capture))
        {
            capture.SetCapturingState(driver, isCapturing);
        }
        
        await Task.CompletedTask;
    }

    private bool TrySetupVirtualDisplay()
    {
        try
        {
            // Check if Xvfb is available
            var xvfbProcess = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "which",
                    Arguments = "Xvfb",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            
            xvfbProcess.Start();
            xvfbProcess.WaitForExit();
            
            if (xvfbProcess.ExitCode != 0)
            {
                Console.WriteLine("   Xvfb not available - install with: apt-get install xvfb");
                return false;
            }

            // Start virtual display
            var virtualDisplay = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "Xvfb",
                    Arguments = ":99 -screen 0 1280x720x24 -ac +extension GLX +render -noreset",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            virtualDisplay.Start();
            
            // Wait a moment for the display to start
            System.Threading.Thread.Sleep(2000);
            
            // Set the DISPLAY environment variable
            Environment.SetEnvironmentVariable("DISPLAY", ":99");
            
            Console.WriteLine("   Virtual display started on :99");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   Failed to setup virtual display: {ex.Message}");
            return false;
        }
    }
}