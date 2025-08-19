namespace WebTestingAiAgent.Core.Models;

public class PlanJson
{
    public string RunId { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string Objective { get; set; } = string.Empty;
    public int TimeBudgetSeconds { get; set; } = 600;
    public int ExplorationDepth { get; set; } = 1;
    public List<TestStep> Steps { get; set; } = new();
}