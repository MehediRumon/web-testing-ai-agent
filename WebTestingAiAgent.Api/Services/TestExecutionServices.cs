using System.Collections.Concurrent;
using WebTestingAiAgent.Core.Interfaces;
using WebTestingAiAgent.Core.Models;

namespace WebTestingAiAgent.Api.Services;

public class TestExecutionService : ITestExecutionService
{
    private readonly ConcurrentDictionary<string, TestExecution> _executions = new();
    private readonly IBrowserAutomationService _browserService;
    private readonly ITestCaseService _testCaseService;

    public TestExecutionService(IBrowserAutomationService browserService, ITestCaseService testCaseService)
    {
        _browserService = browserService;
        _testCaseService = testCaseService;
    }

    public async Task<TestExecution> ExecuteTestCaseAsync(ExecuteTestCaseRequest request)
    {
        var testCase = await _testCaseService.GetTestCaseAsync(request.TestCaseId);
        if (testCase == null)
            throw new ArgumentException($"Test case {request.TestCaseId} not found");

        var execution = new TestExecution
        {
            Id = Guid.NewGuid().ToString(),
            TestCaseId = request.TestCaseId,
            TestCaseName = testCase.Name,
            Status = ExecutionStatus.Running,
            StartedAt = DateTime.UtcNow,
            Settings = request.Settings ?? new ExecutionSettings()
        };

        _executions[execution.Id] = execution;

        // Start execution in background
        _ = Task.Run(async () => await ExecuteTestCaseInternalAsync(execution, testCase));

        return execution;
    }

    public async Task<TestExecution?> GetExecutionAsync(string executionId)
    {
        _executions.TryGetValue(executionId, out var execution);
        return await Task.FromResult(execution);
    }

    public async Task<List<TestExecutionResponse>> GetActiveExecutionsAsync()
    {
        var activeExecutions = _executions.Values
            .Where(e => e.Status == ExecutionStatus.Running || e.Status == ExecutionStatus.Paused)
            .Select(e => new TestExecutionResponse
            {
                Id = e.Id,
                TestCaseId = e.TestCaseId,
                TestCaseName = e.TestCaseName,
                Status = e.Status,
                StartedAt = e.StartedAt,
                EndedAt = e.EndedAt,
                TotalSteps = e.StepResults.Count,
                CompletedSteps = e.StepResults.Count(sr => sr.Status == "passed" || sr.Status == "failed"),
                FailedSteps = e.StepResults.Count(sr => sr.Status == "failed"),
                ErrorMessage = e.ErrorMessage
            })
            .ToList();

        return await Task.FromResult(activeExecutions);
    }

    public async Task<List<TestExecutionResponse>> GetExecutionHistoryAsync(string? testCaseId = null, int page = 1, int pageSize = 20)
    {
        var executions = _executions.Values.AsEnumerable();

        if (!string.IsNullOrEmpty(testCaseId))
        {
            executions = executions.Where(e => e.TestCaseId == testCaseId);
        }

        var historyExecutions = executions
            .OrderByDescending(e => e.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new TestExecutionResponse
            {
                Id = e.Id,
                TestCaseId = e.TestCaseId,
                TestCaseName = e.TestCaseName,
                Status = e.Status,
                StartedAt = e.StartedAt,
                EndedAt = e.EndedAt,
                TotalSteps = e.StepResults.Count,
                CompletedSteps = e.StepResults.Count(sr => sr.Status == "passed" || sr.Status == "failed"),
                FailedSteps = e.StepResults.Count(sr => sr.Status == "failed"),
                ErrorMessage = e.ErrorMessage
            })
            .ToList();

        return await Task.FromResult(historyExecutions);
    }

    public async Task<TestExecution> StopExecutionAsync(string executionId)
    {
        if (!_executions.TryGetValue(executionId, out var execution))
            throw new ArgumentException($"Execution {executionId} not found");

        execution.Status = ExecutionStatus.Cancelled;
        execution.EndedAt = DateTime.UtcNow;
        execution.ErrorMessage = "Execution stopped by user";

        return await Task.FromResult(execution);
    }

    public async Task<TestExecution> PauseExecutionAsync(string executionId)
    {
        if (!_executions.TryGetValue(executionId, out var execution))
            throw new ArgumentException($"Execution {executionId} not found");

        if (execution.Status != ExecutionStatus.Running)
            throw new InvalidOperationException($"Cannot pause execution in status {execution.Status}");

        execution.Status = ExecutionStatus.Paused;
        return await Task.FromResult(execution);
    }

    public async Task<TestExecution> ResumeExecutionAsync(string executionId)
    {
        if (!_executions.TryGetValue(executionId, out var execution))
            throw new ArgumentException($"Execution {executionId} not found");

        if (execution.Status != ExecutionStatus.Paused)
            throw new InvalidOperationException($"Cannot resume execution in status {execution.Status}");

        execution.Status = ExecutionStatus.Running;
        return await Task.FromResult(execution);
    }

    private async Task ExecuteTestCaseInternalAsync(TestExecution execution, TestCase testCase)
    {
        string? browserSessionId = null;
        
        try
        {
            // Start browser session
            browserSessionId = await _browserService.StartBrowserSessionAsync(testCase.BaseUrl, execution.Settings);

            // Execute each step
            foreach (var recordedStep in testCase.Steps.OrderBy(s => s.Order))
            {
                // Check if execution was cancelled or paused
                while (execution.Status == ExecutionStatus.Paused)
                {
                    await Task.Delay(1000); // Wait for resume
                }

                if (execution.Status == ExecutionStatus.Cancelled)
                {
                    break;
                }

                try
                {
                    var stepResult = await _browserService.ExecuteStepAsync(browserSessionId, recordedStep);
                    execution.StepResults.Add(stepResult);

                    // Take screenshot if configured and step failed
                    if (execution.Settings.CaptureScreenshots && stepResult.Status == "failed")
                    {
                        try
                        {
                            var screenshot = await _browserService.TakeScreenshotAsync(browserSessionId);
                            stepResult.Evidence.Screenshots.Add(new Screenshot
                            {
                                Data = Convert.ToBase64String(screenshot),
                                Timestamp = DateTime.UtcNow,
                                Description = $"Screenshot after failed step: {recordedStep.Action}"
                            });
                        }
                        catch
                        {
                            // Ignore screenshot errors
                        }
                    }

                    // Stop on error if configured
                    if (execution.Settings.StopOnError && stepResult.Status == "failed")
                    {
                        execution.Status = ExecutionStatus.Failed;
                        execution.ErrorMessage = stepResult.Error?.Message ?? "Step execution failed";
                        break;
                    }
                }
                catch (Exception ex)
                {
                    var stepResult = new StepResult
                    {
                        StepId = recordedStep.Id,
                        Start = DateTime.UtcNow,
                        End = DateTime.UtcNow,
                        Status = "failed",
                        Error = new StepError { Message = ex.Message }
                    };
                    
                    execution.StepResults.Add(stepResult);

                    if (execution.Settings.StopOnError)
                    {
                        execution.Status = ExecutionStatus.Failed;
                        execution.ErrorMessage = ex.Message;
                        break;
                    }
                }
            }

            // Set final status if not already set
            if (execution.Status == ExecutionStatus.Running)
            {
                var hasFailures = execution.StepResults.Any(sr => sr.Status == "failed");
                execution.Status = hasFailures ? ExecutionStatus.Failed : ExecutionStatus.Completed;
            }
        }
        catch (Exception ex)
        {
            execution.Status = ExecutionStatus.Failed;
            execution.ErrorMessage = ex.Message;
        }
        finally
        {
            execution.EndedAt = DateTime.UtcNow;
            
            // Clean up browser session
            if (browserSessionId != null)
            {
                try
                {
                    await _browserService.StopBrowserSessionAsync(browserSessionId);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }
}