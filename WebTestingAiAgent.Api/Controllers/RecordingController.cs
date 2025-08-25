using Microsoft.AspNetCore.Mvc;
using WebTestingAiAgent.Core.Interfaces;
using WebTestingAiAgent.Core.Models;

namespace WebTestingAiAgent.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RecordingController : ControllerBase
{
    private readonly IRecordingService _recordingService;

    public RecordingController(IRecordingService recordingService)
    {
        _recordingService = recordingService;
    }

    /// <summary>
    /// Start a new recording session
    /// </summary>
    [HttpPost("start")]
    public async Task<ActionResult<RecordingSession>> StartRecording([FromBody] StartRecordingRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var session = await _recordingService.StartRecordingAsync(request);
            return Ok(session);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error starting recording session", error = ex.Message });
        }
    }

    /// <summary>
    /// Get a specific recording session
    /// </summary>
    [HttpGet("{sessionId}")]
    public async Task<ActionResult<RecordingSession>> GetRecordingSession(string sessionId)
    {
        try
        {
            var session = await _recordingService.GetRecordingSessionAsync(sessionId);
            if (session == null)
                return NotFound(new { message = $"Recording session {sessionId} not found" });

            return Ok(session);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error retrieving recording session", error = ex.Message });
        }
    }

    /// <summary>
    /// Get all active recording sessions
    /// </summary>
    [HttpGet("active")]
    public async Task<ActionResult<List<RecordingSessionResponse>>> GetActiveRecordingSessions()
    {
        try
        {
            var sessions = await _recordingService.GetActiveRecordingSessionsAsync();
            return Ok(sessions);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error retrieving active recording sessions", error = ex.Message });
        }
    }

    /// <summary>
    /// Stop a recording session
    /// </summary>
    [HttpPost("{sessionId}/stop")]
    public async Task<ActionResult<RecordingSession>> StopRecording(string sessionId)
    {
        try
        {
            var session = await _recordingService.StopRecordingAsync(sessionId);
            return Ok(session);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error stopping recording session", error = ex.Message });
        }
    }

    /// <summary>
    /// Pause a recording session
    /// </summary>
    [HttpPost("{sessionId}/pause")]
    public async Task<ActionResult<RecordingSession>> PauseRecording(string sessionId)
    {
        try
        {
            var session = await _recordingService.PauseRecordingAsync(sessionId);
            return Ok(session);
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
            return StatusCode(500, new { message = "Error pausing recording session", error = ex.Message });
        }
    }

    /// <summary>
    /// Resume a recording session
    /// </summary>
    [HttpPost("{sessionId}/resume")]
    public async Task<ActionResult<RecordingSession>> ResumeRecording(string sessionId)
    {
        try
        {
            var session = await _recordingService.ResumeRecordingAsync(sessionId);
            return Ok(session);
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
            return StatusCode(500, new { message = "Error resuming recording session", error = ex.Message });
        }
    }

    /// <summary>
    /// Add a step to a recording session
    /// </summary>
    [HttpPost("{sessionId}/steps")]
    public async Task<ActionResult<RecordedStep>> AddStep(string sessionId, [FromBody] RecordedStep step)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var addedStep = await _recordingService.AddStepAsync(sessionId, step);
            return Ok(addedStep);
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
            return StatusCode(500, new { message = "Error adding step to recording session", error = ex.Message });
        }
    }

    /// <summary>
    /// Save a recording session as a test case
    /// </summary>
    [HttpPost("{sessionId}/save")]
    public async Task<ActionResult<TestCase>> SaveAsTestCase(string sessionId, [FromBody] SaveAsTestCaseRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var testCase = await _recordingService.SaveAsTestCaseAsync(sessionId, request.TestCaseName, request.Description);
            return Ok(testCase);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error saving recording as test case", error = ex.Message });
        }
    }

    /// <summary>
    /// Get live steps from a recording session
    /// </summary>
    [HttpGet("{sessionId}/steps")]
    public async Task<ActionResult<List<RecordedStep>>> GetRecordingSteps(string sessionId)
    {
        try
        {
            var session = await _recordingService.GetRecordingSessionAsync(sessionId);
            if (session == null)
                return NotFound(new { message = $"Recording session {sessionId} not found" });

            // Return only the actual recorded steps (excluding session_start)
            var steps = session.Steps.Where(s => s.Action != "session_start").ToList();
            return Ok(steps);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error retrieving recording steps", error = ex.Message });
        }
    }

    /// <summary>
    /// Execute a recording session directly
    /// </summary>
    [HttpPost("{sessionId}/execute")]
    public async Task<ActionResult<TestExecution>> ExecuteRecording(string sessionId, [FromBody] ExecutionSettings? settings = null)
    {
        try
        {
            var session = await _recordingService.GetRecordingSessionAsync(sessionId);
            if (session == null)
                return NotFound(new { message = $"Recording session {sessionId} not found" });

            if (session.Steps.Count == 0)
                return BadRequest(new { message = "Recording session has no steps to execute" });

            // First save the recording as a temporary test case
            var tempTestCaseName = $"TempExec_{sessionId}_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
            var tempTestCase = await _recordingService.SaveAsTestCaseAsync(sessionId, tempTestCaseName, 
                $"Temporary test case for executing recording session '{session.Name}'");

            // Execute the test case
            var execRequest = new ExecuteTestCaseRequest
            {
                TestCaseId = tempTestCase.Id,
                Settings = settings ?? new ExecutionSettings()
            };

            var executionService = HttpContext.RequestServices.GetRequiredService<ITestExecutionService>();
            var execution = await executionService.ExecuteTestCaseAsync(execRequest);
            return Ok(execution);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error executing recording session", error = ex.Message });
        }
    }

    /// <summary>
    /// Delete a recording session
    /// </summary>
    [HttpDelete("{sessionId}")]
    public async Task<ActionResult> DeleteRecordingSession(string sessionId)
    {
        try
        {
            var deleted = await _recordingService.DeleteRecordingSessionAsync(sessionId);
            if (!deleted)
                return NotFound(new { message = $"Recording session {sessionId} not found" });

            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error deleting recording session", error = ex.Message });
        }
    }
}

public class SaveAsTestCaseRequest
{
    public string TestCaseName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}