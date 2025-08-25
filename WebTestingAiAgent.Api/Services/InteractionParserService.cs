using System.Globalization;
using System.Text.RegularExpressions;
using WebTestingAiAgent.Core.Interfaces;
using WebTestingAiAgent.Core.Models;

namespace WebTestingAiAgent.Api.Services;

/// <summary>
/// Service for parsing recorded user interactions from text format into RecordedStep objects
/// </summary>
public class InteractionParserService : IInteractionParserService
{
    /// <summary>
    /// Parse interaction lines in the format:
    /// #<step_number> <ACTION> <selector> ["value"] <url> <timestamp>
    /// </summary>
    public async Task<List<RecordedStep>> ParseInteractionsAsync(string interactionText)
    {
        if (string.IsNullOrWhiteSpace(interactionText))
            throw new ArgumentException("Interaction text cannot be empty", nameof(interactionText));

        var lines = interactionText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var steps = new List<RecordedStep>();

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(trimmedLine) || !trimmedLine.StartsWith("#"))
                continue;

            try
            {
                var step = await ParseInteractionLineAsync(trimmedLine);
                if (step != null)
                    steps.Add(step);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to parse line: '{trimmedLine}'. Error: {ex.Message}", ex);
            }
        }

        // Consolidate multiple INPUT actions on the same element, keeping only the final value
        steps = ConsolidateInputActions(steps);

        return steps;
    }

    /// <summary>
    /// Parse a single interaction line
    /// </summary>
    private async Task<RecordedStep?> ParseInteractionLineAsync(string line)
    {
        // First, try the compact format: #<number> <ACTION> <selector> ["value"] <url> <timestamp>
        var compactPattern = @"^#(\d+)\s+(CLICK|INPUT|SELECT|NAVIGATE|WAIT)\s+(.+?)\s+""(.+?)""\s+(/.+?)\s+(\d{2}:\d{2}:\d{2})$";
        var match = Regex.Match(line, compactPattern);

        if (!match.Success)
        {
            // Try compact format without quoted value (for CLICK actions)
            compactPattern = @"^#(\d+)\s+(CLICK|SELECT|NAVIGATE|WAIT)\s+(.+?)\s+(/.+?)\s+(\d{2}:\d{2}:\d{2})$";
            match = Regex.Match(line, compactPattern);
        }

        if (match.Success)
        {
            var stepNumber = int.Parse(match.Groups[1].Value);
            var action = match.Groups[2].Value.ToLower();
            var selector = match.Groups[3].Value.Trim();
            
            string? value = null;
            string url;
            string timestamp;
            
            if (match.Groups.Count == 7) // INPUT with quoted value
            {
                value = match.Groups[4].Value;
                url = match.Groups[5].Value;
                timestamp = match.Groups[6].Value;
            }
            else // CLICK or other action without quoted value
            {
                url = match.Groups[4].Value;
                timestamp = match.Groups[5].Value;
            }

            return await CreateRecordedStepAsync(stepNumber, action, selector, value, url, timestamp, line);
        }

        // If compact format fails, skip the line (might be from multi-line format that we don't support yet)
        return await Task.FromResult<RecordedStep?>(null);
    }

    /// <summary>
    /// Create a RecordedStep from parsed components
    /// </summary>
    private async Task<RecordedStep> CreateRecordedStepAsync(int stepNumber, string action, string selector, 
        string? value, string url, string timestamp, string originalLine)
    {
        // Parse timestamp - assuming it's in HH:mm:ss format for today
        var today = DateTime.Today;
        if (TimeSpan.TryParseExact(timestamp, @"hh\:mm\:ss", CultureInfo.InvariantCulture, out var timeSpan))
        {
            var fullTimestamp = today.Add(timeSpan);
            
            var recordedStep = new RecordedStep
            {
                Id = Guid.NewGuid().ToString(),
                Order = stepNumber,
                Action = action,
                ElementSelector = selector,
                Value = value,
                Url = url,
                Timestamp = fullTimestamp,
                Metadata = new Dictionary<string, object>
                {
                    { "originalLine", originalLine },
                    { "parsedTimestamp", timestamp },
                    { "importedAt", DateTime.UtcNow }
                }
            };

            return await Task.FromResult(recordedStep);
        }

        throw new FormatException($"Invalid timestamp format: {timestamp}");
    }

    /// <summary>
    /// Validate that an interaction sequence is properly formatted
    /// </summary>
    public async Task<bool> ValidateInteractionsAsync(string interactionText)
    {
        try
        {
            var steps = await ParseInteractionsAsync(interactionText);
            return steps.Any();
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Create a recording session from parsed interactions
    /// </summary>
    public async Task<RecordingSession> CreateRecordingSessionFromInteractionsAsync(
        string interactionText, 
        string sessionName, 
        string baseUrl)
    {
        var steps = await ParseInteractionsAsync(interactionText);
        
        if (!steps.Any())
            throw new InvalidOperationException("No valid interactions found to create recording session");

        // Determine base URL from steps if not provided
        if (string.IsNullOrEmpty(baseUrl) && steps.Any(s => !string.IsNullOrEmpty(s.Url)))
        {
            var firstUrl = steps.First(s => !string.IsNullOrEmpty(s.Url))?.Url;
            if (firstUrl != null)
            {
                // Extract base URL from the first URL path
                baseUrl = DetermineBaseUrlFromPath(firstUrl);
            }
        }

        var session = new RecordingSession
        {
            Id = Guid.NewGuid().ToString(),
            Name = sessionName,
            BaseUrl = baseUrl ?? "https://example.com",
            Steps = steps,
            Status = RecordingStatus.Completed,
            StartedAt = steps.Min(s => s.Timestamp),
            EndedAt = steps.Max(s => s.Timestamp),
            RecordingDuration = steps.Max(s => s.Timestamp) - steps.Min(s => s.Timestamp),
            Settings = new RecordingSettings
            {
                CaptureScreenshots = false,
                MaxSteps = steps.Count + 10,
                TimeoutMs = 30000
            }
        };

        return await Task.FromResult(session);
    }

    /// <summary>
    /// Consolidate multiple INPUT actions on the same element, keeping only the final value
    /// </summary>
    private List<RecordedStep> ConsolidateInputActions(List<RecordedStep> steps)
    {
        var consolidatedSteps = new List<RecordedStep>();
        var inputGroups = new Dictionary<string, List<RecordedStep>>();

        // Group INPUT actions by element selector
        foreach (var step in steps)
        {
            if (step.Action.ToLower() == "input" && !string.IsNullOrEmpty(step.ElementSelector))
            {
                var key = step.ElementSelector.ToLower();
                if (!inputGroups.ContainsKey(key))
                    inputGroups[key] = new List<RecordedStep>();
                
                inputGroups[key].Add(step);
            }
            else
            {
                // Non-input actions are always included
                consolidatedSteps.Add(step);
            }
        }

        // For each input group, keep only the final (last) input action
        foreach (var group in inputGroups.Values)
        {
            if (group.Any())
            {
                // Sort by order (step number) and take the last one
                var finalInput = group.OrderBy(s => s.Order).Last();
                consolidatedSteps.Add(finalInput);
            }
        }

        // Sort the consolidated steps by their original order
        return consolidatedSteps.OrderBy(s => s.Order).ToList();
    }

    /// <summary>
    /// Attempt to determine base URL from a URL path
    /// </summary>
    private string DetermineBaseUrlFromPath(string urlPath)
    {
        // This is a simple implementation - in a real scenario, 
        // you'd want more sophisticated URL parsing
        if (urlPath.StartsWith("/"))
        {
            // Assume HTTPS and a common domain structure
            return "https://example.com";
        }
        
        return urlPath;
    }
}