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
            session.Settings.ForceVisible, // Pass force visible flag
            session.Settings.UseVirtualDisplay); // Pass virtual display setting

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
                // Use GetReadyCapturedEvents for regular collection, then get remaining events when stopping
                var capturedSteps = await _browserService.CollectCapturedInteractionsAsync(browserSessionId);
                
                // Also get any remaining events that might not be "ready" yet when stopping recording
                if (_browserService is BrowserAutomationService browserAutomation && 
                    browserAutomation._interactionCaptures.TryGetValue(browserSessionId, out var capture))
                {
                    var remainingSteps = capture.GetCapturedEvents(); // Get ALL remaining events
                    capturedSteps.AddRange(remainingSteps);
                }
                
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

        // Improved duplicate detection algorithm
        var isDuplicate = false;
        RecordedStep? existingStep = null;

        // Handle different types of events with specific duplicate detection logic
        switch (step.Action.ToLower())
        {
            case "input":
                // For input events, look for recent input on the same element within 5 seconds for live steps
                // This longer window helps consolidate input during extended typing sessions
                existingStep = session.Steps
                    .Where(s => s.Action == "input" && 
                               s.ElementSelector == step.ElementSelector &&
                               DateTime.UtcNow - s.Timestamp < TimeSpan.FromSeconds(5))
                    .LastOrDefault();
                
                if (existingStep != null)
                {
                    // Update the existing input step with the new value and timestamp
                    existingStep.Value = step.Value;
                    existingStep.Timestamp = DateTime.UtcNow;
                    Console.WriteLine($"Recording session {sessionId}: Updated input step value to '{step.Value}' for {step.ElementSelector}");
                    return existingStep;
                }
                break;

            case "click":
                // For click events, check for exact duplicate clicks within 300ms
                isDuplicate = session.Steps.Any(s => 
                    s.Action == "click" &&
                    s.ElementSelector == step.ElementSelector &&
                    DateTime.UtcNow - s.Timestamp < TimeSpan.FromMilliseconds(300));
                break;

            case "navigate":
                // For navigation events, check for same URL within 2 seconds to avoid duplicates from multiple triggers
                isDuplicate = session.Steps.Any(s => 
                    s.Action == "navigate" &&
                    s.Url == step.Url &&
                    DateTime.UtcNow - s.Timestamp < TimeSpan.FromSeconds(2));
                break;

            case "select":
            case "submit":
                // For form-related events, check for duplicates within 500ms
                isDuplicate = session.Steps.Any(s => 
                    s.Action == step.Action &&
                    s.ElementSelector == step.ElementSelector &&
                    DateTime.UtcNow - s.Timestamp < TimeSpan.FromMilliseconds(500));
                break;
        }

        // Skip this step if it's identified as a duplicate
        if (isDuplicate)
        {
            Console.WriteLine($"Recording session {sessionId}: Skipped duplicate {step.Action} step for {step.ElementSelector}");
            return session.Steps.LastOrDefault(s => 
                s.Action == step.Action && 
                s.ElementSelector == step.ElementSelector) ?? step;
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
                // Collect ready events first, then any remaining events when saving
                var capturedSteps = await _browserService.CollectCapturedInteractionsAsync(browserSessionId);
                
                // Also get any remaining events that might not be "ready" yet when saving
                if (_browserService is BrowserAutomationService browserAutomation && 
                    browserAutomation._interactionCaptures.TryGetValue(browserSessionId, out var capture))
                {
                    var remainingSteps = capture.GetCapturedEvents(); // Get ALL remaining events
                    capturedSteps.AddRange(remainingSteps);
                }
                
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

    public async Task<List<RecordedStep>> GetLiveStepsAsync(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            throw new ArgumentException($"Recording session {sessionId} not found");

        // Start with existing recorded steps (excluding session_start)
        var allSteps = session.Steps.Where(s => s.Action != "session_start").ToList();

        // Get browser session ID to collect any pending captured interactions
        var browserSessionId = GetBrowserSessionId(session);
        if (!string.IsNullOrEmpty(browserSessionId))
        {
            try
            {
                // Collect ready captured interactions that haven't been added to session yet
                var capturedSteps = await _browserService.CollectCapturedInteractionsAsync(browserSessionId);
                
                // Filter out any steps that are already in the session to avoid duplicates
                // For input events, be more aggressive about filtering to show only the latest value per element
                var newSteps = new List<RecordedStep>();
                
                foreach (var captured in capturedSteps)
                {
                    if (captured.Action.ToLower() == "input")
                    {
                        // For input events, check if there's already a recent input for the same element
                        var hasRecentInput = allSteps.Any(existing => 
                            existing.Action.ToLower() == "input" &&
                            existing.ElementSelector == captured.ElementSelector &&
                            Math.Abs((existing.Timestamp - captured.Timestamp).TotalMilliseconds) < 5000); // 5 second window for input consolidation
                        
                        if (!hasRecentInput)
                        {
                            // Also remove any older input events for the same element from allSteps to ensure only latest is shown
                            allSteps.RemoveAll(existing =>
                                existing.Action.ToLower() == "input" &&
                                existing.ElementSelector == captured.ElementSelector &&
                                existing.Timestamp < captured.Timestamp);
                            
                            newSteps.Add(captured);
                        }
                    }
                    else
                    {
                        // For non-input events, use the original duplicate detection logic
                        var isDuplicate = allSteps.Any(existing => 
                            existing.Action == captured.Action &&
                            existing.ElementSelector == captured.ElementSelector &&
                            existing.Value == captured.Value &&
                            Math.Abs((existing.Timestamp - captured.Timestamp).TotalMilliseconds) < 1000);
                        
                        if (!isDuplicate)
                        {
                            newSteps.Add(captured);
                        }
                    }
                }

                // Add the new steps to our result
                allSteps.AddRange(newSteps);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not collect captured interactions for session {sessionId}: {ex.Message}");
            }
        }

        // Sort by timestamp to maintain chronological order
        return allSteps.OrderBy(s => s.Timestamp).ThenBy(s => s.Order).ToList();
    }
}

public class BrowserAutomationService : IBrowserAutomationService
{
    private readonly ConcurrentDictionary<string, IWebDriver> _browserSessions = new();
    public readonly ConcurrentDictionary<string, BrowserInteractionCapture> _interactionCaptures = new();

    public async Task<string> StartBrowserSessionAsync(string baseUrl, ExecutionSettings settings, bool forceVisible = false, bool useVirtualDisplay = false)
    {
        var sessionId = Guid.NewGuid().ToString();
        
        // Always start browser in visible mode
        Console.WriteLine("🎬 Starting browser in visible mode for recording...");
        var driver = await TryStartBrowserAsync(sessionId, baseUrl, settings, false, true, false);
        if (driver != null)
        {
            Console.WriteLine("✅ Browser started successfully in visible mode");
            return sessionId;
        }
        
        throw new InvalidOperationException("Failed to start browser in visible mode");
    }
    
    private async Task<IWebDriver?> TryStartBrowserAsync(string sessionId, string baseUrl, ExecutionSettings settings, bool forceHeadless, bool forceVisible, bool useVirtualDisplay = false)
    {
        var options = new ChromeOptions();
        
        // Always run in visible mode as per requirements
        Console.WriteLine("Running browser in visible mode for interaction recording");
        // Add options to improve the visible browser experience for recording
        options.AddArgument("--window-size=1280,720");
        options.AddArgument("--window-position=0,0");
        
        // Minimal Chrome options for maximum compatibility
        options.AddArgument("--no-sandbox");
        options.AddArgument("--disable-dev-shm-usage");
        options.AddArgument("--disable-gpu"); // Always disable GPU for consistency
        
        // Essential options for container environments
        options.AddArgument("--disable-dbus");  // Fix D-Bus permission errors
        options.AddArgument("--no-first-run");
        options.AddArgument("--no-default-browser-check");
        
        // Use a unique remote debugging port to avoid conflicts
        options.AddArgument($"--remote-debugging-port={9222 + new Random().Next(100, 999)}");

        try
        {
            Console.WriteLine($"Starting Chrome driver... (timeout: {settings.BrowserInitTimeoutMs/1000}s)");
            
            // Ensure DISPLAY environment variable is properly set for ChromeDriver
            var display = Environment.GetEnvironmentVariable("DISPLAY");
            if (!string.IsNullOrEmpty(display))
            {
                Console.WriteLine($"Using DISPLAY: {display}");
            }
            
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
            // Reduce timeout for fallback attempts to fail faster
            var initTimeoutMs = forceHeadless ? Math.Min(baseTimeoutMs, 10000) : baseTimeoutMs;
            
            Console.WriteLine($"Using browser initialization timeout: {initTimeoutMs/1000} seconds (visible)");
            
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
            try
            {
                await driver.Navigate().GoToUrlAsync(baseUrl);
                Console.WriteLine("Navigation successful.");
            }
            catch (WebDriverException navEx) when (navEx.Message.Contains("ERR_INTERNET_DISCONNECTED") ||
                                                  navEx.Message.Contains("ERR_NAME_NOT_RESOLVED") ||
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
            catch (Exception captureEx)
            {
                Console.WriteLine($"Warning: Failed to inject capture script: {captureEx.Message}");
                Console.WriteLine("Recording session will continue but interaction capture may be limited.");
            }
            
            return driver;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to start browser session: {ex.Message}");
            return null;
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

        Console.WriteLine($"Executing step: {step.Action} on {step.ElementSelector} with value '{step.Value}'");

        try
        {
            // Validate step before execution
            if (string.IsNullOrEmpty(step.Action))
            {
                throw new ArgumentException("Step action cannot be null or empty");
            }

            switch (step.Action.ToLower())
            {
                case "click":
                    if (string.IsNullOrEmpty(step.ElementSelector))
                    {
                        throw new ArgumentException("ElementSelector is required for click action");
                    }
                    
                    var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(5));
                    var element = wait.Until(driver => driver.FindElement(By.CssSelector(step.ElementSelector)));
                    
                    // Ensure element is clickable
                    wait.Until(driver => element.Enabled && element.Displayed);
                    element.Click();
                    
                    // Small delay after click to allow any navigation or updates
                    await Task.Delay(200);
                    Console.WriteLine($"Successfully clicked element: {step.ElementSelector}");
                    break;

                case "input":
                    if (string.IsNullOrEmpty(step.ElementSelector))
                    {
                        throw new ArgumentException("ElementSelector is required for input action");
                    }
                    if (string.IsNullOrEmpty(step.Value))
                    {
                        Console.WriteLine($"Warning: Input value is empty for element {step.ElementSelector}");
                    }
                    
                    var inputWait = new WebDriverWait(driver, TimeSpan.FromSeconds(5));
                    var inputElement = inputWait.Until(driver => driver.FindElement(By.CssSelector(step.ElementSelector)));
                    
                    // Ensure element is interactable
                    inputWait.Until(driver => inputElement.Enabled && inputElement.Displayed);
                    inputElement.Clear();
                    if (!string.IsNullOrEmpty(step.Value))
                    {
                        inputElement.SendKeys(step.Value);
                    }
                    
                    // Small delay after input
                    await Task.Delay(200);
                    Console.WriteLine($"Successfully input value '{step.Value}' to element: {step.ElementSelector}");
                    break;

                case "select":
                    if (!string.IsNullOrEmpty(step.ElementSelector) && !string.IsNullOrEmpty(step.Value))
                    {
                        var selectWait = new WebDriverWait(driver, TimeSpan.FromSeconds(5));
                        var selectElement = selectWait.Until(driver => driver.FindElement(By.CssSelector(step.ElementSelector)));
                        
                        // Ensure element is interactable
                        selectWait.Until(driver => selectElement.Enabled && selectElement.Displayed);
                        var select = new SelectElement(selectElement);
                        
                        try
                        {
                            // Try selecting by visible text first
                            select.SelectByText(step.Value);
                        }
                        catch (NoSuchElementException)
                        {
                            // If that fails, try by value
                            try
                            {
                                select.SelectByValue(step.Value);
                            }
                            catch (NoSuchElementException)
                            {
                                // If that also fails, try partial text match
                                var options = select.Options;
                                var matchingOption = options.FirstOrDefault(o => 
                                    o.Text.Contains(step.Value, StringComparison.OrdinalIgnoreCase) ||
                                    o.GetAttribute("value").Contains(step.Value, StringComparison.OrdinalIgnoreCase));
                                
                                if (matchingOption != null)
                                {
                                    matchingOption.Click();
                                }
                                else
                                {
                                    throw new NoSuchElementException($"Could not find option with text or value '{step.Value}' in select element {step.ElementSelector}");
                                }
                            }
                        }
                        
                        // Small delay after selection
                        await Task.Delay(200);
                        Console.WriteLine($"Successfully selected '{step.Value}' in element: {step.ElementSelector}");
                    }
                    break;

                case "navigate":
                    if (string.IsNullOrEmpty(step.Url))
                    {
                        throw new ArgumentException("URL is required for navigate action");
                    }
                    
                    Console.WriteLine($"Navigating to: {step.Url}");
                    driver.Navigate().GoToUrl(step.Url);
                    
                    // Wait for page to load completely
                    var navWait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
                    navWait.Until(driver => ((IJavaScriptExecutor)driver).ExecuteScript("return document.readyState").Equals("complete"));
                    
                    // Additional wait for any dynamic content
                    await Task.Delay(1000);
                    Console.WriteLine($"Successfully navigated to: {step.Url}");
                    break;

                case "wait":
                    if (int.TryParse(step.Value, out var waitMs))
                    {
                        Console.WriteLine($"Waiting for {waitMs}ms");
                        await Task.Delay(waitMs);
                    }
                    else
                    {
                        Console.WriteLine($"Warning: Invalid wait time '{step.Value}', skipping wait step");
                    }
                    break;

                default:
                    Console.WriteLine($"Warning: Unknown action '{step.Action}', skipping step");
                    break;
            }

            stepResult.Status = "passed";
            stepResult.End = DateTime.UtcNow;
            Console.WriteLine($"Step completed successfully: {step.Action}");
        }
        catch (Exception ex)
        {
            stepResult.Status = "failed";
            stepResult.End = DateTime.UtcNow;
            stepResult.Error = new StepError { Message = ex.Message };
            Console.WriteLine($"Step failed: {step.Action} - {ex.Message}");
            
            // Log additional details for debugging
            if (ex is WebDriverTimeoutException)
            {
                Console.WriteLine($"Timeout occurred waiting for element: {step.ElementSelector}");
            }
            else if (ex is NoSuchElementException)
            {
                Console.WriteLine($"Element not found: {step.ElementSelector}");
            }
            else if (ex is ElementNotInteractableException)
            {
                Console.WriteLine($"Element not interactable: {step.ElementSelector}");
            }
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
        
        // Return only ready captured events (respects debouncing)
        return capture.GetReadyCapturedEvents();
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


}