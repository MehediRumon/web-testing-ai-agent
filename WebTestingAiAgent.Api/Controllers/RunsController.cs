using Microsoft.AspNetCore.Mvc;
using WebTestingAiAgent.Core.Interfaces;
using WebTestingAiAgent.Core.Models;

namespace WebTestingAiAgent.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RunsController : ControllerBase
{
    private readonly IRunManager _runManager;
    private readonly IValidationService _validationService;

    public RunsController(IRunManager runManager, IValidationService validationService)
    {
        _runManager = runManager;
        _validationService = validationService;
    }

    /// <summary>
    /// Create a new test run
    /// POST /api/runs
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<CreateRunResponse>> CreateRun([FromBody] CreateRunRequest request)
    {
        // Validate request (FR-INPUT-06)
        var validationErrors = await _validationService.ValidateCreateRunRequestAsync(request);
        if (validationErrors.Any())
        {
            return BadRequest(new ApiErrorResponse
            {
                Message = "Validation failed",
                Errors = validationErrors
            });
        }

        try
        {
            var runId = await _runManager.CreateRunAsync(request);
            return Ok(new CreateRunResponse { RunId = runId });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiErrorResponse
            {
                Message = $"Failed to create run: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Get run status and partial results
    /// GET /api/runs/{runId}
    /// </summary>
    [HttpGet("{runId}")]
    public async Task<ActionResult<RunStatus>> GetRunStatus(string runId)
    {
        try
        {
            var status = await _runManager.GetRunStatusAsync(runId);
            if (status == null)
            {
                return NotFound(new ApiErrorResponse
                {
                    Message = $"Run {runId} not found"
                });
            }
            return Ok(status);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiErrorResponse
            {
                Message = $"Failed to get run status: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Cancel a running test
    /// POST /api/runs/{runId}/cancel
    /// </summary>
    [HttpPost("{runId}/cancel")]
    public async Task<ActionResult> CancelRun(string runId)
    {
        try
        {
            await _runManager.CancelRunAsync(runId);
            return Ok();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiErrorResponse
            {
                Message = $"Failed to cancel run: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Get list of active runs
    /// GET /api/runs
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<RunStatus>>> GetActiveRuns()
    {
        try
        {
            var runs = await _runManager.GetActiveRunsAsync();
            return Ok(runs);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiErrorResponse
            {
                Message = $"Failed to get active runs: {ex.Message}"
            });
        }
    }
}