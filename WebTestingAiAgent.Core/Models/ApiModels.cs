namespace WebTestingAiAgent.Core.Models;

public class CreateRunRequest
{
    public string Objective { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public AgentConfig? Config { get; set; }
}

public class CreateRunResponse
{
    public string RunId { get; set; } = string.Empty;
}

public class RunStatus
{
    public string RunId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // queued|running|completed|failed|cancelled
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int Progress { get; set; } // 0-100
    public List<StepResult> PartialResults { get; set; } = new();
}

public class ValidationError
{
    public string Field { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class ApiErrorResponse
{
    public string Message { get; set; } = string.Empty;
    public List<ValidationError> Errors { get; set; } = new();
}