using WebTestingAiAgent.Core.Interfaces;
using WebTestingAiAgent.Core.Models;

namespace WebTestingAiAgent.Api.Services;

public class RunManagerService : IRunManager
{
    private readonly Dictionary<string, RunStatus> _runs = new();
    private readonly Dictionary<string, RunReport> _reports = new();

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

        // TODO: Queue the run for execution
        // For now, simulate immediate completion
        await Task.Delay(100);
        
        return runId;
    }

    public async Task<RunStatus> GetRunStatusAsync(string runId)
    {
        await Task.CompletedTask;
        _runs.TryGetValue(runId, out var status);
        return status;
    }

    public async Task CancelRunAsync(string runId)
    {
        await Task.CompletedTask;
        if (_runs.TryGetValue(runId, out var status))
        {
            status.Status = "cancelled";
            status.CompletedAt = DateTime.UtcNow;
        }
    }

    public async Task<RunReport> GetRunReportAsync(string runId)
    {
        await Task.CompletedTask;
        _reports.TryGetValue(runId, out var report);
        return report;
    }

    public async Task<List<RunStatus>> GetActiveRunsAsync()
    {
        await Task.CompletedTask;
        return _runs.Values
            .Where(r => r.Status != "completed" && r.Status != "cancelled" && r.Status != "failed")
            .ToList();
    }
}