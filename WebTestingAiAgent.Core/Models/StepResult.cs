namespace WebTestingAiAgent.Core.Models;

public class Evidence
{
    public string? ScreenshotPath { get; set; }
    public string? DomSnapshotPath { get; set; }
    public List<string> Console { get; set; } = new();
    public List<NetworkRequest> Network { get; set; } = new();
}

public class NetworkRequest
{
    public string Url { get; set; } = string.Empty;
    public int Status { get; set; }
}

public class StepError
{
    public string Message { get; set; } = string.Empty;
}

public class HealingSuggestion
{
    public Locator SuggestedLocator { get; set; } = new();
    public double Confidence { get; set; }
}

public class StepResult
{
    public string StepId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // passed|failed|retried|skipped
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public string? Notes { get; set; }
    public Evidence Evidence { get; set; } = new();
    public StepError? Error { get; set; }
    public HealingSuggestion? Healing { get; set; }
}