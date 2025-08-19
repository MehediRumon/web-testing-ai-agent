using System.Text.Json;
using WebTestingAiAgent.Core.Interfaces;
using WebTestingAiAgent.Core.Models;

namespace WebTestingAiAgent.Api.Services;

public class ReportingService : IReportingService
{
    public async Task<string> GenerateHtmlReportAsync(RunReport report)
    {
        await Task.CompletedTask;
        
        var html = $@"<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Test Report - {report.RunId}</title>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 20px; }}
        .header {{ background: #f5f5f5; padding: 20px; border-radius: 5px; }}
        .summary {{ margin: 20px 0; }}
        .pass {{ color: green; }}
        .fail {{ color: red; }}
        .step {{ margin: 10px 0; padding: 10px; border: 1px solid #ddd; border-radius: 3px; }}
    </style>
</head>
<body>
    <div class='header'>
        <h1>Web Testing AI Agent Report</h1>
        <p><strong>Run ID:</strong> {report.RunId}</p>
        <p><strong>Objective:</strong> {report.Objective}</p>
        <p><strong>Environment:</strong> {report.Env.Browser} ({(report.Env.Headless ? "Headless" : "Headed")})</p>
        <p><strong>Base URL:</strong> {report.Env.BaseUrl}</p>
    </div>
    
    <div class='summary'>
        <h2>Summary</h2>
        <p><span class='pass'>Passed: {report.Summary.Passed}</span></p>
        <p><span class='fail'>Failed: {report.Summary.Failed}</span></p>
        <p>Skipped: {report.Summary.Skipped}</p>
        <p>Duration: {report.Summary.DurationSec} seconds</p>
    </div>
    
    <div class='results'>
        <h2>Step Results</h2>
        {string.Join("", report.Results.Select(r => $@"
        <div class='step'>
            <h3>{r.StepId} - {r.Status}</h3>
            <p><strong>Duration:</strong> {(r.End - r.Start).TotalSeconds:F2}s</p>
            {(string.IsNullOrEmpty(r.Notes) ? "" : $"<p><strong>Notes:</strong> {r.Notes}</p>")}
            {(r.Error != null ? $"<p class='fail'><strong>Error:</strong> {r.Error.Message}</p>" : "")}
        </div>"))}
    </div>
</body>
</html>";

        return html;
    }

    public async Task<string> GenerateJsonReportAsync(RunReport report)
    {
        await Task.CompletedTask;
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        return JsonSerializer.Serialize(report, options);
    }

    public async Task<string> GenerateJUnitXmlAsync(RunReport report)
    {
        await Task.CompletedTask;
        
        var xml = $@"<?xml version='1.0' encoding='UTF-8'?>
<testsuite name='WebTestingAiAgent' 
           tests='{report.Summary.Passed + report.Summary.Failed + report.Summary.Skipped}' 
           failures='{report.Summary.Failed}' 
           skipped='{report.Summary.Skipped}' 
           time='{report.Summary.DurationSec}'>
{string.Join("", report.Results.Select(r => $@"
    <testcase classname='WebTestingAiAgent' name='{r.StepId}' time='{(r.End - r.Start).TotalSeconds:F2}'>
        {(r.Status == "failed" && r.Error != null ? $"<failure message='{r.Error.Message}'>{r.Error.Message}</failure>" : "")}
        {(r.Status == "skipped" ? "<skipped/>" : "")}
    </testcase>"))}
</testsuite>";

        return xml;
    }

    public async Task<string> CreateEvidencePackAsync(string runId)
    {
        await Task.CompletedTask;
        // TODO: Implement evidence pack creation
        return Path.Combine(Path.GetTempPath(), $"evidence-pack-{runId}.zip");
    }
}

public class IntegrationService : IIntegrationService
{
    public async Task SendSlackNotificationAsync(string runId, RunReport report)
    {
        await Task.CompletedTask;
        // TODO: Implement Slack webhook integration
        Console.WriteLine($"Slack notification sent for run {runId}");
    }

    public async Task CreateJiraIssueAsync(string runId, RunReport report)
    {
        await Task.CompletedTask;
        // TODO: Implement Jira issue creation
        Console.WriteLine($"Jira issue created for run {runId}");
    }
}

public class StorageService : IStorageService
{
    private readonly string _basePath;

    public StorageService()
    {
        _basePath = Path.Combine(Directory.GetCurrentDirectory(), "artifacts");
        Directory.CreateDirectory(_basePath);
    }

    public async Task<string> SaveArtifactAsync(string runId, string fileName, byte[] content)
    {
        var runPath = Path.Combine(_basePath, runId);
        Directory.CreateDirectory(runPath);
        
        var filePath = Path.Combine(runPath, fileName);
        await File.WriteAllBytesAsync(filePath, content);
        
        return filePath;
    }

    public async Task<string> SaveArtifactAsync(string runId, string fileName, string content)
    {
        var runPath = Path.Combine(_basePath, runId);
        Directory.CreateDirectory(runPath);
        
        var filePath = Path.Combine(runPath, fileName);
        await File.WriteAllTextAsync(filePath, content);
        
        return filePath;
    }

    public async Task<byte[]> GetArtifactAsync(string runId, string fileName)
    {
        var filePath = Path.Combine(_basePath, runId, fileName);
        if (!File.Exists(filePath))
        {
            return Array.Empty<byte>();
        }
        
        return await File.ReadAllBytesAsync(filePath);
    }

    public async Task<List<string>> ListArtifactsAsync(string runId)
    {
        await Task.CompletedTask;
        var runPath = Path.Combine(_basePath, runId);
        if (!Directory.Exists(runPath))
        {
            return new List<string>();
        }
        
        return Directory.GetFiles(runPath)
            .Select(Path.GetFileName)
            .ToList();
    }

    public async Task DeleteRunArtifactsAsync(string runId)
    {
        await Task.CompletedTask;
        var runPath = Path.Combine(_basePath, runId);
        if (Directory.Exists(runPath))
        {
            Directory.Delete(runPath, true);
        }
    }
}