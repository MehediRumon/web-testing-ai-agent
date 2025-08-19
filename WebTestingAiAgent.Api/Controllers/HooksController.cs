using Microsoft.AspNetCore.Mvc;
using WebTestingAiAgent.Core.Interfaces;
using WebTestingAiAgent.Core.Models;

namespace WebTestingAiAgent.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HooksController : ControllerBase
{
    private readonly IIntegrationService _integrationService;
    private readonly IRunManager _runManager;

    public HooksController(IIntegrationService integrationService, IRunManager runManager)
    {
        _integrationService = integrationService;
        _runManager = runManager;
    }

    /// <summary>
    /// Send Slack notification for a run
    /// POST /api/hooks/slack
    /// </summary>
    [HttpPost("slack")]
    public async Task<ActionResult> SendSlackNotification([FromBody] SlackWebhookRequest request)
    {
        try
        {
            var report = await _runManager.GetRunReportAsync(request.RunId);
            if (report == null)
            {
                return NotFound(new ApiErrorResponse
                {
                    Message = $"Run {request.RunId} not found"
                });
            }

            await _integrationService.SendSlackNotificationAsync(request.RunId, report);
            return Ok();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiErrorResponse
            {
                Message = $"Failed to send Slack notification: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Create Jira issue for a failed run
    /// POST /api/hooks/jira
    /// </summary>
    [HttpPost("jira")]
    public async Task<ActionResult> CreateJiraIssue([FromBody] JiraWebhookRequest request)
    {
        try
        {
            var report = await _runManager.GetRunReportAsync(request.RunId);
            if (report == null)
            {
                return NotFound(new ApiErrorResponse
                {
                    Message = $"Run {request.RunId} not found"
                });
            }

            await _integrationService.CreateJiraIssueAsync(request.RunId, report);
            return Ok();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiErrorResponse
            {
                Message = $"Failed to create Jira issue: {ex.Message}"
            });
        }
    }
}

public class SlackWebhookRequest
{
    public string RunId { get; set; } = string.Empty;
}

public class JiraWebhookRequest
{
    public string RunId { get; set; } = string.Empty;
}