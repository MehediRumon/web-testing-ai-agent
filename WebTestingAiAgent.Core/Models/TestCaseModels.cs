using System.ComponentModel.DataAnnotations;

namespace WebTestingAiAgent.Core.Models;

#region Core Models

public class TestCase
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public List<RecordedStep> Steps { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<string> Tags { get; set; } = new();
    public TestCaseFormat Format { get; set; } = TestCaseFormat.Json;
}

public class RecordedStep
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public int Order { get; set; }
    public string Action { get; set; } = string.Empty; // click, input, select, navigate, wait
    public string? ElementSelector { get; set; }
    public string? Value { get; set; }
    public string? Url { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class RecordingSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public List<RecordedStep> Steps { get; set; } = new();
    public RecordingStatus Status { get; set; } = RecordingStatus.NotStarted;
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public DateTime? PausedAt { get; set; }
    public TimeSpan RecordingDuration { get; set; } = TimeSpan.Zero;
    public RecordingSettings Settings { get; set; } = new();
}

public class RecordingSettings
{
    public bool CaptureScreenshots { get; set; } = true;
    public bool CaptureNetwork { get; set; } = false;
    public bool CaptureConsole { get; set; } = false;
    public int MaxSteps { get; set; } = 100;
    public int TimeoutMs { get; set; } = 30000;
    public int MaxRecordingMinutes { get; set; } = 60;
    public bool AutoExecuteAfterRecording { get; set; } = false;
}

public class TestExecution
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TestCaseId { get; set; } = string.Empty;
    public string TestCaseName { get; set; } = string.Empty;
    public ExecutionStatus Status { get; set; } = ExecutionStatus.NotStarted;
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public List<StepResult> StepResults { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public ExecutionSettings Settings { get; set; } = new();
}

public class ExecutionSettings
{
    public string Browser { get; set; } = "chrome";
    public bool Headless { get; set; } = true;
    public int TimeoutMs { get; set; } = 30000;
    public bool CaptureScreenshots { get; set; } = true;
    public bool StopOnError { get; set; } = true;
}

#endregion

#region Enumerations

public enum TestCaseFormat
{
    Json,
    Yaml,
    Gherkin
}

public enum RecordingStatus
{
    NotStarted,
    Recording,
    Paused,
    Stopped,
    Completed,
    Error
}

public enum ExecutionStatus
{
    NotStarted,
    Running,
    Paused,
    Completed,
    Failed,
    Cancelled
}

#endregion

#region API Models

public class CreateTestCaseRequest
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;
    
    [Required]
    [Url]
    public string BaseUrl { get; set; } = string.Empty;
    
    public List<string> Tags { get; set; } = new();
    
    public TestCaseFormat Format { get; set; } = TestCaseFormat.Json;
}

public class UpdateTestCaseRequest
{
    [StringLength(100, MinimumLength = 1)]
    public string? Name { get; set; }
    
    [StringLength(500)]
    public string? Description { get; set; }
    
    [Url]
    public string? BaseUrl { get; set; }
    
    public List<string>? Tags { get; set; }
    
    public TestCaseFormat? Format { get; set; }
}

public class StartRecordingRequest
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    [Url]
    public string BaseUrl { get; set; } = string.Empty;
    
    public RecordingSettings? Settings { get; set; }
}

public class ExecuteTestCaseRequest
{
    [Required]
    public string TestCaseId { get; set; } = string.Empty;
    
    public ExecutionSettings? Settings { get; set; }
}

public class TestCaseListRequest
{
    public string? SearchTerm { get; set; }
    public List<string>? Tags { get; set; }
    public TestCaseFormat? Format { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SortBy { get; set; } = "UpdatedAt";
    public bool SortDescending { get; set; } = true;
}

public class TestCaseResponse
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public int StepCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<string> Tags { get; set; } = new();
    public TestCaseFormat Format { get; set; }
}

public class RecordingSessionResponse
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public RecordingStatus Status { get; set; }
    public int StepCount { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public TimeSpan RecordingDuration { get; set; } = TimeSpan.Zero;
}

public class TestExecutionResponse
{
    public string Id { get; set; } = string.Empty;
    public string TestCaseId { get; set; } = string.Empty;
    public string TestCaseName { get; set; } = string.Empty;
    public ExecutionStatus Status { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public int TotalSteps { get; set; }
    public int CompletedSteps { get; set; }
    public int FailedSteps { get; set; }
    public string? ErrorMessage { get; set; }
}

#endregion