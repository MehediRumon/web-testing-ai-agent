using WebTestingAiAgent.Core.Models;

namespace WebTestingAiAgent.Core.Interfaces;

/// <summary>
/// Test Case management service interface
/// </summary>
public interface ITestCaseService
{
    Task<TestCase> CreateTestCaseAsync(CreateTestCaseRequest request);
    Task<TestCase?> GetTestCaseAsync(string id);
    Task<List<TestCaseResponse>> GetTestCasesAsync(TestCaseListRequest request);
    Task<TestCase> UpdateTestCaseAsync(string id, UpdateTestCaseRequest request);
    Task<bool> DeleteTestCaseAsync(string id);
    Task<string> ExportTestCaseAsync(string id, TestCaseFormat format);
    Task<TestCase> ImportTestCaseAsync(string content, TestCaseFormat format);
}

/// <summary>
/// Recording service interface for capturing user interactions
/// </summary>
public interface IRecordingService
{
    Task<RecordingSession> StartRecordingAsync(StartRecordingRequest request);
    Task<RecordingSession?> GetRecordingSessionAsync(string sessionId);
    Task<List<RecordingSessionResponse>> GetActiveRecordingSessionsAsync();
    Task<RecordingSession> StopRecordingAsync(string sessionId);
    Task<RecordingSession> PauseRecordingAsync(string sessionId);
    Task<RecordingSession> ResumeRecordingAsync(string sessionId);
    Task<RecordedStep> AddStepAsync(string sessionId, RecordedStep step);
    Task<TestCase> SaveAsTestCaseAsync(string sessionId, string testCaseName, string description = "");
    Task<bool> DeleteRecordingSessionAsync(string sessionId);
}

/// <summary>
/// Test execution service interface for replaying test cases
/// </summary>
public interface ITestExecutionService
{
    Task<TestExecution> ExecuteTestCaseAsync(ExecuteTestCaseRequest request);
    Task<TestExecution?> GetExecutionAsync(string executionId);
    Task<List<TestExecutionResponse>> GetActiveExecutionsAsync();
    Task<List<TestExecutionResponse>> GetExecutionHistoryAsync(string? testCaseId = null, int page = 1, int pageSize = 20);
    Task<TestExecution> StopExecutionAsync(string executionId);
    Task<TestExecution> PauseExecutionAsync(string executionId);
    Task<TestExecution> ResumeExecutionAsync(string executionId);
}

/// <summary>
/// Browser automation service for recording and playback
/// </summary>
public interface IBrowserAutomationService
{
    Task<string> StartBrowserSessionAsync(string baseUrl, ExecutionSettings settings, bool forceVisible = false, bool useVirtualDisplay = false);
    Task StopBrowserSessionAsync(string sessionId);
    Task<RecordedStep> CaptureInteractionAsync(string sessionId, string eventType, Dictionary<string, object> eventData);
    Task<StepResult> ExecuteStepAsync(string sessionId, RecordedStep step);
    Task<byte[]> TakeScreenshotAsync(string sessionId);
    Task<string> GetPageSourceAsync(string sessionId);
    Task<Dictionary<string, object>> GetElementInfoAsync(string sessionId, string selector);
    Task<List<RecordedStep>> CollectCapturedInteractionsAsync(string sessionId);
    Task SetCaptureStateAsync(string sessionId, bool isCapturing);
}

/// <summary>
/// Test case export/import service for different formats
/// </summary>
public interface ITestCaseFormatService
{
    Task<string> ConvertToJsonAsync(TestCase testCase);
    Task<string> ConvertToYamlAsync(TestCase testCase);
    Task<string> ConvertToGherkinAsync(TestCase testCase);
    Task<TestCase> ParseFromJsonAsync(string content);
    Task<TestCase> ParseFromYamlAsync(string content);
    Task<TestCase> ParseFromGherkinAsync(string content);
    Task<bool> ValidateFormatAsync(string content, TestCaseFormat format);
}