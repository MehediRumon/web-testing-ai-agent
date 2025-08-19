using WebTestingAiAgent.Core.Interfaces;
using WebTestingAiAgent.Core.Models;

namespace WebTestingAiAgent.Api.Services;

public class RunManagerService : IRunManager
{
    private readonly Dictionary<string, RunStatus> _runs = new();
    private readonly Dictionary<string, RunReport> _reports = new();
    private readonly IPlannerService _plannerService;
    private readonly IExecutorService _executorService;

    public RunManagerService(IPlannerService plannerService, IExecutorService executorService)
    {
        _plannerService = plannerService;
        _executorService = executorService;
    }

    public async Task<string> CreateRunAsync(CreateRunRequest request)
    {
        var runId = Guid.NewGuid().ToString();
        var runStatus = new RunStatus
        {
            RunId = runId,
            Status = "queued",
            CreatedAt = DateTime.UtcNow,
            Progress = 0,
            PartialResults = new List<StepResult>()
        };

        _runs[runId] = runStatus;

        // Start execution in background with better error handling
        _ = Task.Run(async () =>
        {
            try
            {
                await ExecuteRunAsync(runId, request);
            }
            catch (Exception ex)
            {
                // Ensure run remains in dictionary even if ExecuteRunAsync fails completely
                if (_runs.TryGetValue(runId, out var failedRunStatus))
                {
                    failedRunStatus.Status = "error";
                    failedRunStatus.CompletedAt = DateTime.UtcNow;
                    failedRunStatus.Progress = 100;
                    
                    // Create a simple error report
                    _reports[runId] = new RunReport
                    {
                        RunId = runId,
                        Objective = request.Objective ?? "Unknown",
                        Summary = new RunSummary
                        {
                            Passed = 0,
                            Failed = 1,
                            Skipped = 0,
                            DurationSec = (int)(DateTime.UtcNow - failedRunStatus.CreatedAt).TotalSeconds
                        },
                        Results = new List<StepResult>
                        {
                            new StepResult
                            {
                                StepId = "execution-error",
                                Status = "failed",
                                Start = failedRunStatus.CreatedAt,
                                End = DateTime.UtcNow,
                                Error = new StepError { Message = $"Execution failed: {ex.Message}" },
                                Evidence = new Evidence()
                            }
                        },
                        Env = new RunEnvironment(),
                        Analytics = new RunAnalytics()
                    };
                }
            }
        });
        
        return runId;
    }

    private async Task ExecuteRunAsync(string runId, CreateRunRequest request)
    {
        var runStatus = _runs[runId];
        
        try
        {
            // Update status to planning
            runStatus.Status = "planning";
            runStatus.StartedAt = DateTime.UtcNow;
            runStatus.Progress = 10;

            // Generate plan
            var config = request.Config ?? new AgentConfig();
            var plan = await _plannerService.CreatePlanAsync(request.Objective ?? "", request.BaseUrl, config);
            runStatus.Progress = 30;

            // Update status to executing
            runStatus.Status = "executing";
            runStatus.Progress = 40;

            // Execute plan
            var stepResults = await _executorService.ExecutePlanAsync(plan, config);
            
            // Update results as they complete
            runStatus.PartialResults = stepResults;
            runStatus.Progress = 90;

            // Generate report
            var report = new RunReport
            {
                RunId = runId,
                Objective = plan.Objective,
                Env = new RunEnvironment
                {
                    Browser = "chromium",
                    Headless = true,
                    BaseUrl = plan.BaseUrl
                },
                Results = stepResults,
                Summary = GenerateSummary(stepResults),
                Analytics = new RunAnalytics
                {
                    FlakeRate = 0.0,
                    LocatorHealth = new List<LocatorHealth>()
                }
            };
            
            _reports[runId] = report;
            
            // Complete the run
            runStatus.Status = stepResults.Any(r => r.Status == "failed") ? "completed_with_failures" : "completed";
            runStatus.CompletedAt = DateTime.UtcNow;
            runStatus.Progress = 100;
        }
        catch (Exception ex)
        {
            // Don't immediately mark as failed - keep it visible for troubleshooting
            runStatus.Status = "error";
            runStatus.CompletedAt = DateTime.UtcNow;
            runStatus.Progress = 100;
            
            // Create error report
            _reports[runId] = new RunReport
            {
                RunId = runId,
                Summary = new RunSummary
                {
                    Passed = 0,
                    Failed = 1,
                    Skipped = 0,
                    DurationSec = (int)(DateTime.UtcNow - runStatus.CreatedAt).TotalSeconds
                },
                Results = new List<StepResult>
                {
                    new StepResult
                    {
                        StepId = "error",
                        Status = "failed",
                        Start = runStatus.CreatedAt,
                        End = DateTime.UtcNow,
                        Error = new StepError { Message = ex.Message },
                        Evidence = new Evidence()
                    }
                }
            };
        }
    }

    private RunSummary GenerateSummary(List<StepResult> results)
    {
        return new RunSummary
        {
            Passed = results.Count(r => r.Status == "passed"),
            Failed = results.Count(r => r.Status == "failed"),
            Skipped = results.Count(r => r.Status == "skipped"),
            DurationSec = results.Any() ? 
                (int)(results.Max(r => r.End) - results.Min(r => r.Start)).TotalSeconds : 0
        };
    }

    public async Task<RunStatus> GetRunStatusAsync(string runId)
    {
        await Task.CompletedTask;
        if (_runs.TryGetValue(runId, out var status))
        {
            return status;
        }
        
        // Return a default "not found" status instead of null
        return new RunStatus
        {
            RunId = runId,
            Status = "not_found",
            CreatedAt = DateTime.UtcNow,
            Progress = 0,
            PartialResults = new List<StepResult>()
        };
    }

    public async Task CancelRunAsync(string runId)
    {
        await Task.CompletedTask;
        if (_runs.TryGetValue(runId, out var status))
        {
            status.Status = "cancelled";
            status.CompletedAt = DateTime.UtcNow;
            await _executorService.CancelRunAsync(runId);
        }
    }

    public async Task<RunReport> GetRunReportAsync(string runId)
    {
        await Task.CompletedTask;
        if (_reports.TryGetValue(runId, out var report))
        {
            return report;
        }
        
        // Return a default "not found" report instead of null
        return new RunReport
        {
            RunId = runId,
            Objective = "Run not found",
            Env = new RunEnvironment(),
            Summary = new RunSummary(),
            Results = new List<StepResult>(),
            Analytics = new RunAnalytics()
        };
    }

    public async Task<List<RunStatus>> GetActiveRunsAsync()
    {
        await Task.CompletedTask;
        return _runs.Values
            .Where(r => r.Status != "completed" && r.Status != "cancelled" && r.Status != "failed" && r.Status != "completed_with_failures")
            .OrderByDescending(r => r.CreatedAt)
            .ToList();
    }
}