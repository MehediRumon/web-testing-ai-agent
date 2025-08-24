using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using YamlDotNet.Serialization;
using WebTestingAiAgent.Core.Interfaces;
using WebTestingAiAgent.Core.Models;
using WebTestingAiAgent.Api.Data;

namespace WebTestingAiAgent.Api.Services;

public class TestCaseService : ITestCaseService
{
    private readonly WebTestingDbContext _context;

    public TestCaseService(WebTestingDbContext context)
    {
        _context = context;
    }

    public async Task<TestCase> CreateTestCaseAsync(CreateTestCaseRequest request)
    {
        var testCase = new TestCase
        {
            Id = Guid.NewGuid().ToString(),
            Name = request.Name,
            Description = request.Description,
            BaseUrl = request.BaseUrl,
            Tags = request.Tags,
            Format = request.Format,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.TestCases.Add(testCase);
        await _context.SaveChangesAsync();
        return testCase;
    }

    public async Task<TestCase?> GetTestCaseAsync(string id)
    {
        return await _context.TestCases
            .Include(tc => tc.Steps)
            .FirstOrDefaultAsync(tc => tc.Id == id);
    }

    public async Task<List<TestCaseResponse>> GetTestCasesAsync(TestCaseListRequest request)
    {
        var query = _context.TestCases.Include(tc => tc.Steps).AsQueryable();

        // Apply filters
        if (!string.IsNullOrEmpty(request.SearchTerm))
        {
            query = query.Where(tc => 
                tc.Name.Contains(request.SearchTerm) ||
                tc.Description.Contains(request.SearchTerm));
        }

        if (request.Tags?.Any() == true)
        {
            // For SQLite, we need to search within the JSON tags field
            foreach (var tag in request.Tags)
            {
                query = query.Where(tc => tc.Tags.Contains(tag));
            }
        }

        if (request.Format.HasValue)
        {
            query = query.Where(tc => tc.Format == request.Format.Value);
        }

        // Apply sorting
        query = request.SortBy?.ToLower() switch
        {
            "name" => request.SortDescending ? query.OrderByDescending(tc => tc.Name) : query.OrderBy(tc => tc.Name),
            "createdat" => request.SortDescending ? query.OrderByDescending(tc => tc.CreatedAt) : query.OrderBy(tc => tc.CreatedAt),
            _ => request.SortDescending ? query.OrderByDescending(tc => tc.UpdatedAt) : query.OrderBy(tc => tc.UpdatedAt)
        };

        // Apply pagination and convert to response
        var pagedTestCases = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(tc => new TestCaseResponse
            {
                Id = tc.Id,
                Name = tc.Name,
                Description = tc.Description,
                BaseUrl = tc.BaseUrl,
                StepCount = tc.Steps.Count,
                CreatedAt = tc.CreatedAt,
                UpdatedAt = tc.UpdatedAt,
                Tags = tc.Tags,
                Format = tc.Format
            })
            .ToListAsync();

        return pagedTestCases;
    }

    public async Task<TestCase> UpdateTestCaseAsync(string id, UpdateTestCaseRequest request)
    {
        var testCase = await _context.TestCases
            .Include(tc => tc.Steps)
            .FirstOrDefaultAsync(tc => tc.Id == id);
            
        if (testCase == null)
            throw new ArgumentException($"Test case with ID {id} not found");

        if (!string.IsNullOrEmpty(request.Name))
            testCase.Name = request.Name;
        
        if (request.Description != null)
            testCase.Description = request.Description;
        
        if (!string.IsNullOrEmpty(request.BaseUrl))
            testCase.BaseUrl = request.BaseUrl;
        
        if (request.Tags != null)
            testCase.Tags = request.Tags;
        
        if (request.Format.HasValue)
            testCase.Format = request.Format.Value;

        testCase.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return testCase;
    }

    public async Task<bool> DeleteTestCaseAsync(string id)
    {
        var testCase = await _context.TestCases.FindAsync(id);
        if (testCase == null)
            return false;

        _context.TestCases.Remove(testCase);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<string> ExportTestCaseAsync(string id, TestCaseFormat format)
    {
        var testCase = await _context.TestCases
            .Include(tc => tc.Steps)
            .FirstOrDefaultAsync(tc => tc.Id == id);
            
        if (testCase == null)
            throw new ArgumentException($"Test case with ID {id} not found");

        return format switch
        {
            TestCaseFormat.Json => await ConvertToJsonAsync(testCase),
            TestCaseFormat.Yaml => await ConvertToYamlAsync(testCase),
            TestCaseFormat.Gherkin => await ConvertToGherkinAsync(testCase),
            _ => throw new ArgumentException($"Unsupported format: {format}")
        };
    }

    public async Task<TestCase> ImportTestCaseAsync(string content, TestCaseFormat format)
    {
        var testCase = format switch
        {
            TestCaseFormat.Json => await ParseFromJsonAsync(content),
            TestCaseFormat.Yaml => await ParseFromYamlAsync(content),
            TestCaseFormat.Gherkin => await ParseFromGherkinAsync(content),
            _ => throw new ArgumentException($"Unsupported format: {format}")
        };

        testCase.Id = Guid.NewGuid().ToString();
        testCase.CreatedAt = DateTime.UtcNow;
        testCase.UpdatedAt = DateTime.UtcNow;

        _context.TestCases.Add(testCase);
        await _context.SaveChangesAsync();
        return testCase;
    }

    private async Task<string> ConvertToJsonAsync(TestCase testCase)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        return await Task.FromResult(JsonSerializer.Serialize(testCase, options));
    }

    private async Task<string> ConvertToYamlAsync(TestCase testCase)
    {
        var serializer = new SerializerBuilder().Build();
        return await Task.FromResult(serializer.Serialize(testCase));
    }

    private async Task<string> ConvertToGherkinAsync(TestCase testCase)
    {
        var gherkin = $"Feature: {testCase.Name}\n";
        if (!string.IsNullOrEmpty(testCase.Description))
            gherkin += $"  {testCase.Description}\n\n";

        gherkin += $"  Background:\n";
        gherkin += $"    Given I navigate to \"{testCase.BaseUrl}\"\n\n";

        gherkin += $"  Scenario: Execute {testCase.Name}\n";
        
        foreach (var step in testCase.Steps.OrderBy(s => s.Order))
        {
            gherkin += ConvertStepToGherkin(step);
        }

        return await Task.FromResult(gherkin);
    }

    private string ConvertStepToGherkin(RecordedStep step)
    {
        return step.Action.ToLower() switch
        {
            "click" => $"    When I click on element \"{step.ElementSelector}\"\n",
            "input" => $"    When I enter \"{step.Value}\" into element \"{step.ElementSelector}\"\n",
            "select" => $"    When I select \"{step.Value}\" from element \"{step.ElementSelector}\"\n",
            "navigate" => $"    When I navigate to \"{step.Url}\"\n",
            "wait" => $"    Then I wait for {step.Value} milliseconds\n",
            _ => $"    # Unknown action: {step.Action}\n"
        };
    }

    private async Task<TestCase> ParseFromJsonAsync(string content)
    {
        var testCase = JsonSerializer.Deserialize<TestCase>(content);
        return await Task.FromResult(testCase ?? throw new ArgumentException("Invalid JSON content"));
    }

    private async Task<TestCase> ParseFromYamlAsync(string content)
    {
        var deserializer = new DeserializerBuilder().Build();
        var testCase = deserializer.Deserialize<TestCase>(content);
        return await Task.FromResult(testCase ?? throw new ArgumentException("Invalid YAML content"));
    }

    private async Task<TestCase> ParseFromGherkinAsync(string content)
    {
        // Basic Gherkin parsing - in a real implementation, you'd use a proper Gherkin parser
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var testCase = new TestCase();
        var steps = new List<RecordedStep>();
        var stepOrder = 0;

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            
            if (trimmedLine.StartsWith("Feature:"))
            {
                testCase.Name = trimmedLine.Substring(8).Trim();
            }
            else if (trimmedLine.StartsWith("Given I navigate to"))
            {
                var url = ExtractQuotedValue(trimmedLine);
                testCase.BaseUrl = url;
            }
            else if (trimmedLine.StartsWith("When I click on"))
            {
                var selector = ExtractQuotedValue(trimmedLine);
                steps.Add(new RecordedStep
                {
                    Order = stepOrder++,
                    Action = "click",
                    ElementSelector = selector
                });
            }
            else if (trimmedLine.StartsWith("When I enter"))
            {
                var parts = trimmedLine.Split("into element");
                if (parts.Length == 2)
                {
                    var value = ExtractQuotedValue(parts[0]);
                    var selector = ExtractQuotedValue(parts[1]);
                    steps.Add(new RecordedStep
                    {
                        Order = stepOrder++,
                        Action = "input",
                        ElementSelector = selector,
                        Value = value
                    });
                }
            }
        }

        testCase.Steps = steps;
        return await Task.FromResult(testCase);
    }

    private string ExtractQuotedValue(string text)
    {
        var startIndex = text.IndexOf('"') + 1;
        var endIndex = text.LastIndexOf('"');
        return startIndex > 0 && endIndex > startIndex 
            ? text.Substring(startIndex, endIndex - startIndex) 
            : string.Empty;
    }
}