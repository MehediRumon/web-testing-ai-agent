using WebTestingAiAgent.Core.Models;

namespace WebTestingAiAgent.Core.Interfaces;

/// <summary>
/// Run management and orchestration interface (FR-GOV-01 to FR-GOV-03)
/// </summary>
public interface IRunManager
{
    Task<string> CreateRunAsync(CreateRunRequest request);
    Task<RunStatus> GetRunStatusAsync(string runId);
    Task CancelRunAsync(string runId);
    Task<RunReport> GetRunReportAsync(string runId);
    Task<List<RunStatus>> GetActiveRunsAsync();
}

/// <summary>
/// Integration services interface (FR-INTEG-01 to FR-INTEG-04)
/// </summary>
public interface IIntegrationService
{
    Task SendSlackNotificationAsync(string runId, RunReport report);
    Task CreateJiraIssueAsync(string runId, RunReport report);
}

/// <summary>
/// Validation service interface (FR-INPUT-01 to FR-INPUT-06)
/// </summary>
public interface IValidationService
{
    Task<List<ValidationError>> ValidateCreateRunRequestAsync(CreateRunRequest request);
    Task<List<ValidationError>> ValidateConfigAsync(AgentConfig config);
    Task<List<ValidationError>> ValidateObjectiveAsync(string objective);
    Task<List<ValidationError>> ValidateBaseUrlAsync(string baseUrl);
}

/// <summary>
/// Storage service for artifacts and reports
/// </summary>
public interface IStorageService
{
    Task<string> SaveArtifactAsync(string runId, string fileName, byte[] content);
    Task<string> SaveArtifactAsync(string runId, string fileName, string content);
    Task<byte[]> GetArtifactAsync(string runId, string fileName);
    Task<List<string>> ListArtifactsAsync(string runId);
    Task DeleteRunArtifactsAsync(string runId);
}