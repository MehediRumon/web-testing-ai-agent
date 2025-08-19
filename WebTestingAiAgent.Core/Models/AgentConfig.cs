namespace WebTestingAiAgent.Core.Models;

public class RetryPolicy
{
    public int MaxStepRetries { get; set; } = 1;
    public int RetryWaitMs { get; set; } = 500;
}

public class EvidenceConfig
{
    public bool Verbose { get; set; }
    public bool CaptureConsole { get; set; } = true;
    public bool CaptureNetwork { get; set; } = true;
}

public class ExplorationConfig
{
    public int MaxDepth { get; set; } = 2;
    public int TimeBudgetSec { get; set; } = 600;
}

public class JiraConfig
{
    public string Url { get; set; } = string.Empty;
    public string ProjectKey { get; set; } = string.Empty;
}

public class IntegrationsConfig
{
    public string SlackWebhook { get; set; } = string.Empty;
    public JiraConfig Jira { get; set; } = new();
}

public class SecurityConfig
{
    public List<string> MaskSelectors { get; set; } = new();
    public bool AllowCrossOrigin { get; set; }
}

public class AgentConfig
{
    public string Browser { get; set; } = "chrome";
    public bool Headless { get; set; } = false; // Changed to false so users can see browser by default
    public int ExplicitTimeoutMs { get; set; } = 10000;
    public RetryPolicy RetryPolicy { get; set; } = new();
    public int Parallel { get; set; } = 4;
    public string ArtifactsPath { get; set; } = "./artifacts";
    public EvidenceConfig Evidence { get; set; } = new();
    public ExplorationConfig Exploration { get; set; } = new();
    public IntegrationsConfig Integrations { get; set; } = new();
    public SecurityConfig Security { get; set; } = new();
}