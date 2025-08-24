using Microsoft.AspNetCore.Mvc;
using WebTestingAiAgent.Core.Interfaces;
using WebTestingAiAgent.Core.Models;

namespace WebTestingAiAgent.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExecutionController : ControllerBase
{
    private readonly ITestExecutionService _executionService;

    public ExecutionController(ITestExecutionService executionService)
    {
        _executionService = executionService;
    }

    /// <summary>
    /// Execute a test case
    /// </summary>
    [HttpPost("execute")]
    public async Task<ActionResult<TestExecution>> ExecuteTestCase([FromBody] ExecuteTestCaseRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var execution = await _executionService.ExecuteTestCaseAsync(request);
            return Ok(execution);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error executing test case", error = ex.Message });
        }
    }

    /// <summary>
    /// Get a specific test execution
    /// </summary>
    [HttpGet("{executionId}")]
    public async Task<ActionResult<TestExecution>> GetExecution(string executionId)
    {
        try
        {
            var execution = await _executionService.GetExecutionAsync(executionId);
            if (execution == null)
                return NotFound(new { message = $"Execution {executionId} not found" });

            return Ok(execution);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error retrieving execution", error = ex.Message });
        }
    }

    /// <summary>
    /// Get all active executions
    /// </summary>
    [HttpGet("active")]
    public async Task<ActionResult<List<TestExecutionResponse>>> GetActiveExecutions()
    {
        try
        {
            var executions = await _executionService.GetActiveExecutionsAsync();
            return Ok(executions);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error retrieving active executions", error = ex.Message });
        }
    }

    /// <summary>
    /// Get execution history
    /// </summary>
    [HttpGet("history")]
    public async Task<ActionResult<List<TestExecutionResponse>>> GetExecutionHistory(
        [FromQuery] string? testCaseId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var executions = await _executionService.GetExecutionHistoryAsync(testCaseId, page, pageSize);
            return Ok(executions);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error retrieving execution history", error = ex.Message });
        }
    }

    /// <summary>
    /// Stop a test execution
    /// </summary>
    [HttpPost("{executionId}/stop")]
    public async Task<ActionResult<TestExecution>> StopExecution(string executionId)
    {
        try
        {
            var execution = await _executionService.StopExecutionAsync(executionId);
            return Ok(execution);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error stopping execution", error = ex.Message });
        }
    }

    /// <summary>
    /// Pause a test execution
    /// </summary>
    [HttpPost("{executionId}/pause")]
    public async Task<ActionResult<TestExecution>> PauseExecution(string executionId)
    {
        try
        {
            var execution = await _executionService.PauseExecutionAsync(executionId);
            return Ok(execution);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error pausing execution", error = ex.Message });
        }
    }

    /// <summary>
    /// Resume a test execution
    /// </summary>
    [HttpPost("{executionId}/resume")]
    public async Task<ActionResult<TestExecution>> ResumeExecution(string executionId)
    {
        try
        {
            var execution = await _executionService.ResumeExecutionAsync(executionId);
            return Ok(execution);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error resuming execution", error = ex.Message });
        }
    }
}