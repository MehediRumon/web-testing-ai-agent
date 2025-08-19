namespace WebTestingAiAgent.Core.Models;

public class Locator
{
    public string By { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class Target
{
    public Locator Primary { get; set; } = new();
    public List<Locator> Fallbacks { get; set; } = new();
    public Fingerprint? Fingerprint { get; set; }
}

public class Fingerprint
{
    public string Tag { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public Dictionary<string, string> Attrs { get; set; } = new();
}