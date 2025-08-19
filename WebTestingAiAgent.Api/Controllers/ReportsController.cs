using Microsoft.AspNetCore.Mvc;
using WebTestingAiAgent.Core.Interfaces;
using WebTestingAiAgent.Core.Models;

namespace WebTestingAiAgent.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReportsController : ControllerBase
{
    private readonly IRunManager _runManager;
    private readonly IReportingService _reportingService;
    private readonly IStorageService _storageService;

    public ReportsController(IRunManager runManager, IReportingService reportingService, IStorageService storageService)
    {
        _runManager = runManager;
        _reportingService = reportingService;
        _storageService = storageService;
    }

    /// <summary>
    /// Get run report in various formats
    /// GET /api/reports/{runId}?format=html|json|junit
    /// </summary>
    [HttpGet("{runId}")]
    public async Task<ActionResult> GetReport(string runId, [FromQuery] string format = "html")
    {
        try
        {
            var report = await _runManager.GetRunReportAsync(runId);
            if (report == null)
            {
                return NotFound(new ApiErrorResponse
                {
                    Message = $"Report for run {runId} not found"
                });
            }

            return format.ToLower() switch
            {
                "json" => await GetJsonReport(report),
                "junit" => await GetJUnitReport(report),
                "html" or _ => await GetHtmlReport(report)
            };
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiErrorResponse
            {
                Message = $"Failed to get report: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Get artifacts for a run
    /// GET /api/reports/{runId}/artifacts
    /// </summary>
    [HttpGet("{runId}/artifacts")]
    public async Task<ActionResult<List<string>>> GetArtifacts(string runId)
    {
        try
        {
            var artifacts = await _storageService.ListArtifactsAsync(runId);
            return Ok(artifacts);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiErrorResponse
            {
                Message = $"Failed to list artifacts: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Download specific artifact
    /// GET /api/reports/{runId}/artifacts/{fileName}
    /// </summary>
    [HttpGet("{runId}/artifacts/{fileName}")]
    public async Task<ActionResult> GetArtifact(string runId, string fileName)
    {
        try
        {
            var content = await _storageService.GetArtifactAsync(runId, fileName);
            if (content == null || content.Length == 0)
            {
                return NotFound();
            }

            var contentType = GetContentType(fileName);
            return File(content, contentType, fileName);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiErrorResponse
            {
                Message = $"Failed to get artifact: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Download complete evidence pack as ZIP
    /// GET /api/reports/{runId}/evidence-pack
    /// </summary>
    [HttpGet("{runId}/evidence-pack")]
    public async Task<ActionResult> GetEvidencePack(string runId)
    {
        try
        {
            var zipPath = await _reportingService.CreateEvidencePackAsync(runId);
            var content = await System.IO.File.ReadAllBytesAsync(zipPath);
            return File(content, "application/zip", $"evidence-pack-{runId}.zip");
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiErrorResponse
            {
                Message = $"Failed to create evidence pack: {ex.Message}"
            });
        }
    }

    private async Task<ActionResult> GetHtmlReport(RunReport report)
    {
        var html = await _reportingService.GenerateHtmlReportAsync(report);
        return Content(html, "text/html");
    }

    private async Task<ActionResult> GetJsonReport(RunReport report)
    {
        var json = await _reportingService.GenerateJsonReportAsync(report);
        return Content(json, "application/json");
    }

    private async Task<ActionResult> GetJUnitReport(RunReport report)
    {
        var xml = await _reportingService.GenerateJUnitXmlAsync(report);
        return Content(xml, "application/xml");
    }

    private static string GetContentType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".html" => "text/html",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".txt" => "text/plain",
            ".log" => "text/plain",
            _ => "application/octet-stream"
        };
    }
}