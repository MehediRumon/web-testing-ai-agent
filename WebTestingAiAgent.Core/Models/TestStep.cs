namespace WebTestingAiAgent.Core.Models;

public class Assertion
{
    public string Type { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class StepMetadata
{
    public List<string> Tags { get; set; } = new();
}

public class TestStep
{
    public string Id { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public Target? Target { get; set; }
    public string? Value { get; set; }
    public List<Assertion> Assertions { get; set; } = new();
    public int TimeoutMs { get; set; } = 10000;
    public StepMetadata Metadata { get; set; } = new();
}