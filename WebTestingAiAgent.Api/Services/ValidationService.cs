using WebTestingAiAgent.Core.Interfaces;
using WebTestingAiAgent.Core.Models;

namespace WebTestingAiAgent.Api.Services;

public class ValidationService : IValidationService
{
    public async Task<List<ValidationError>> ValidateCreateRunRequestAsync(CreateRunRequest request)
    {
        await Task.CompletedTask;
        var errors = new List<ValidationError>();

        // FR-INPUT-01: Objective validation (min 5 chars, max 4000)
        var objectiveErrors = await ValidateObjectiveAsync(request.Objective);
        errors.AddRange(objectiveErrors);

        // FR-INPUT-02: BaseUrl validation
        var baseUrlErrors = await ValidateBaseUrlAsync(request.BaseUrl);
        errors.AddRange(baseUrlErrors);

        // FR-INPUT-06: Missing required fields
        if (string.IsNullOrWhiteSpace(request.Objective))
        {
            errors.Add(new ValidationError
            {
                Field = nameof(request.Objective),
                Message = "Objective is required"
            });
        }

        if (string.IsNullOrWhiteSpace(request.BaseUrl))
        {
            errors.Add(new ValidationError
            {
                Field = nameof(request.BaseUrl),
                Message = "BaseUrl is required"
            });
        }

        // Validate config if provided
        if (request.Config != null)
        {
            var configErrors = await ValidateConfigAsync(request.Config);
            errors.AddRange(configErrors);
        }

        return errors;
    }

    public async Task<List<ValidationError>> ValidateConfigAsync(AgentConfig config)
    {
        await Task.CompletedTask;
        var errors = new List<ValidationError>();

        // FR-INPUT-03: TimeBudgetSeconds (30–3600)
        if (config.Exploration.TimeBudgetSec < 30 || config.Exploration.TimeBudgetSec > 3600)
        {
            errors.Add(new ValidationError
            {
                Field = "config.exploration.timeBudgetSec",
                Message = "TimeBudgetSeconds must be between 30 and 3600"
            });
        }

        // FR-INPUT-04: ExplorationDepth (0–5)
        if (config.Exploration.MaxDepth < 0 || config.Exploration.MaxDepth > 5)
        {
            errors.Add(new ValidationError
            {
                Field = "config.exploration.maxDepth",
                Message = "ExplorationDepth must be between 0 and 5"
            });
        }

        // FR-INPUT-05: Parallelism (1–20)
        if (config.Parallel < 1 || config.Parallel > 20)
        {
            errors.Add(new ValidationError
            {
                Field = "config.parallel",
                Message = "Parallelism must be between 1 and 20"
            });
        }

        return errors;
    }

    public async Task<List<ValidationError>> ValidateObjectiveAsync(string objective)
    {
        await Task.CompletedTask;
        var errors = new List<ValidationError>();

        if (!string.IsNullOrWhiteSpace(objective))
        {
            if (objective.Length < 5)
            {
                errors.Add(new ValidationError
                {
                    Field = "objective",
                    Message = "Objective must be at least 5 characters long"
                });
            }

            if (objective.Length > 4000)
            {
                errors.Add(new ValidationError
                {
                    Field = "objective",
                    Message = "Objective must not exceed 4000 characters"
                });
            }
        }

        return errors;
    }

    public async Task<List<ValidationError>> ValidateBaseUrlAsync(string baseUrl)
    {
        await Task.CompletedTask;
        var errors = new List<ValidationError>();

        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
            {
                errors.Add(new ValidationError
                {
                    Field = "baseUrl",
                    Message = "BaseUrl must be a valid absolute URL"
                });
            }
            else
            {
                // FR-INPUT-02: HTTPS required for non-local
                if (!uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) &&
                    !uri.Host.Equals("127.0.0.1") &&
                    uri.Scheme != "https")
                {
                    errors.Add(new ValidationError
                    {
                        Field = "baseUrl",
                        Message = "HTTPS is required for non-local URLs"
                    });
                }
            }
        }

        return errors;
    }
}