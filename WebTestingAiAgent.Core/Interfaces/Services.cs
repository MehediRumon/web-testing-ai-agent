using WebTestingAiAgent.Core.Models;

namespace WebTestingAiAgent.Core.Interfaces;

/// <summary>
/// AI Planner service interface (FR-PLAN-01 to FR-PLAN-05)
/// </summary>
public interface IPlannerService
{
    Task<PlanJson> CreatePlanAsync(string objective, string baseUrl, AgentConfig config);
    Task<PlanJson> ReplanAsync(PlanJson originalPlan, string feedback, AgentConfig config);
    Task<bool> ValidatePlanAsync(PlanJson plan);
}

/// <summary>
/// Execution engine interface (FR-EXEC-01 to FR-EXEC-06)
/// </summary>
public interface IExecutorService
{
    Task<List<StepResult>> ExecutePlanAsync(PlanJson plan, AgentConfig config);
    Task<StepResult> ExecuteStepAsync(TestStep step, AgentConfig config);
    Task CancelRunAsync(string runId);
}

/// <summary>
/// Assertion and heuristics engine (FR-ASSERT-01 to FR-ASSERT-04)
/// </summary>
public interface IAssertionService
{
    Task<bool> EvaluateAssertionAsync(Assertion assertion, object context);
    Task<List<string>> RunHeuristicsAsync(object pageContext);
    Task<string> PerformSoftOracleAsync(string subGoal, object uiContext);
}

/// <summary>
/// Self-healing and suggestions service (FR-HEAL-01 to FR-HEAL-03)
/// </summary>
public interface IHealingService
{
    Task<List<Locator>> GetFallbackLocatorsAsync(Locator failedLocator, object pageContext);
    Task<HealingSuggestion> GenerateHealingSuggestionAsync(Locator failedLocator, object pageContext);
    Task<string> GenerateHealingReportAsync(List<HealingSuggestion> suggestions);
}

/// <summary>
/// Reporting service (FR-REPORT-01 to FR-REPORT-05)
/// </summary>
public interface IReportingService
{
    Task<string> GenerateHtmlReportAsync(RunReport report);
    Task<string> GenerateJsonReportAsync(RunReport report);
    Task<string> GenerateJUnitXmlAsync(RunReport report);
    Task<string> CreateEvidencePackAsync(string runId);
}