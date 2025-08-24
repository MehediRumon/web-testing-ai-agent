using Microsoft.AspNetCore.Mvc;
using WebTestingAiAgent.Core.Interfaces;
using WebTestingAiAgent.Core.Models;

namespace WebTestingAiAgent.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestCasesController : ControllerBase
{
    private readonly ITestCaseService _testCaseService;

    public TestCasesController(ITestCaseService testCaseService)
    {
        _testCaseService = testCaseService;
    }

    /// <summary>
    /// Get all test cases with optional filtering
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<TestCaseResponse>>> GetTestCases([FromQuery] TestCaseListRequest request)
    {
        try
        {
            var testCases = await _testCaseService.GetTestCasesAsync(request);
            return Ok(testCases);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error retrieving test cases", error = ex.Message });
        }
    }

    /// <summary>
    /// Get a specific test case by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<TestCase>> GetTestCase(string id)
    {
        try
        {
            var testCase = await _testCaseService.GetTestCaseAsync(id);
            if (testCase == null)
                return NotFound(new { message = $"Test case {id} not found" });

            return Ok(testCase);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error retrieving test case", error = ex.Message });
        }
    }

    /// <summary>
    /// Create a new test case
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<TestCase>> CreateTestCase([FromBody] CreateTestCaseRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var testCase = await _testCaseService.CreateTestCaseAsync(request);
            return CreatedAtAction(nameof(GetTestCase), new { id = testCase.Id }, testCase);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error creating test case", error = ex.Message });
        }
    }

    /// <summary>
    /// Update an existing test case
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<TestCase>> UpdateTestCase(string id, [FromBody] UpdateTestCaseRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var testCase = await _testCaseService.UpdateTestCaseAsync(id, request);
            return Ok(testCase);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error updating test case", error = ex.Message });
        }
    }

    /// <summary>
    /// Delete a test case
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteTestCase(string id)
    {
        try
        {
            var deleted = await _testCaseService.DeleteTestCaseAsync(id);
            if (!deleted)
                return NotFound(new { message = $"Test case {id} not found" });

            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error deleting test case", error = ex.Message });
        }
    }

    /// <summary>
    /// Export a test case in the specified format
    /// </summary>
    [HttpGet("{id}/export")]
    public async Task<ActionResult> ExportTestCase(string id, [FromQuery] TestCaseFormat format = TestCaseFormat.Json)
    {
        try
        {
            var exportedContent = await _testCaseService.ExportTestCaseAsync(id, format);
            
            var contentType = format switch
            {
                TestCaseFormat.Json => "application/json",
                TestCaseFormat.Yaml => "application/x-yaml",
                TestCaseFormat.Gherkin => "text/plain",
                _ => "text/plain"
            };

            var fileName = format switch
            {
                TestCaseFormat.Json => $"testcase-{id}.json",
                TestCaseFormat.Yaml => $"testcase-{id}.yaml",
                TestCaseFormat.Gherkin => $"testcase-{id}.feature",
                _ => $"testcase-{id}.txt"
            };

            return File(System.Text.Encoding.UTF8.GetBytes(exportedContent), contentType, fileName);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error exporting test case", error = ex.Message });
        }
    }

    /// <summary>
    /// Import a test case from file content
    /// </summary>
    [HttpPost("import")]
    public async Task<ActionResult<TestCase>> ImportTestCase([FromBody] ImportTestCaseRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var testCase = await _testCaseService.ImportTestCaseAsync(request.Content, request.Format);
            return CreatedAtAction(nameof(GetTestCase), new { id = testCase.Id }, testCase);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error importing test case", error = ex.Message });
        }
    }
}

public class ImportTestCaseRequest
{
    public string Content { get; set; } = string.Empty;
    public TestCaseFormat Format { get; set; } = TestCaseFormat.Json;
}