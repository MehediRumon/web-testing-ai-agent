namespace WebTestingAiAgent.Core.Models;

public class RunSummary
{
    public int Passed { get; set; }
    public int Failed { get; set; }
    public int Skipped { get; set; }
    public int DurationSec { get; set; }
}

public class LocatorHealth
{
    public string Selector { get; set; } = string.Empty;
    public int Failures { get; set; }
    public string Suggest { get; set; } = string.Empty;
}

public class RunAnalytics
{
    public double FlakeRate { get; set; }
    public List<LocatorHealth> LocatorHealth { get; set; } = new();
}

public class RunEnvironment
{
    public string Browser { get; set; } = string.Empty;
    public bool Headless { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
}

public class RunReport
{
    public string RunId { get; set; } = string.Empty;
    public string Objective { get; set; } = string.Empty;
    public RunEnvironment Env { get; set; } = new();
    public RunSummary Summary { get; set; } = new();
    public List<StepResult> Results { get; set; } = new();
    public RunAnalytics Analytics { get; set; } = new();
}