using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using WebTestingAiAgent.Core.Interfaces;
using WebTestingAiAgent.Core.Models;

namespace WebTestingAiAgent.Api.Services;

public class BugValidationService : IBugValidationService
{
    private readonly IBugStorageService _storageService;
    private const int MaxImageSizeBytes = 10 * 1024 * 1024; // 10MB
    private readonly string[] _allowedImageTypes = { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp" };

    public BugValidationService(IBugStorageService storageService)
    {
        _storageService = storageService;
    }

    public async Task<List<ValidationError>> ValidateCreateBugRequestAsync(CreateBugRequest request)
    {
        var errors = new List<ValidationError>();

        // Validate required fields
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            errors.Add(new ValidationError { Field = nameof(request.Title), Message = "Title is required" });
        }
        else if (request.Title.Length > 500)
        {
            errors.Add(new ValidationError { Field = nameof(request.Title), Message = "Title must be 500 characters or less" });
        }

        if (string.IsNullOrWhiteSpace(request.UrlMenu))
        {
            errors.Add(new ValidationError { Field = nameof(request.UrlMenu), Message = "URL/Menu is required" });
        }
        else if (request.UrlMenu.Length > 1000)
        {
            errors.Add(new ValidationError { Field = nameof(request.UrlMenu), Message = "URL/Menu must be 1000 characters or less" });
        }

        if (request.Description.Length > 5000)
        {
            errors.Add(new ValidationError { Field = nameof(request.Description), Message = "Description must be 5000 characters or less" });
        }

        // Validate enum values
        if (!Enum.IsDefined(typeof(BugType), request.BugType))
        {
            errors.Add(new ValidationError { Field = nameof(request.BugType), Message = "Invalid bug type" });
        }

        if (!Enum.IsDefined(typeof(Priority), request.Priority))
        {
            errors.Add(new ValidationError { Field = nameof(request.Priority), Message = "Invalid priority" });
        }

        // Validate assignees exist and are developers
        if (!string.IsNullOrEmpty(request.PrimaryAssigneeId))
        {
            var primaryAssignee = await _storageService.GetUserAsync(request.PrimaryAssigneeId);
            if (primaryAssignee == null)
            {
                errors.Add(new ValidationError { Field = nameof(request.PrimaryAssigneeId), Message = "Primary assignee not found" });
            }
            else if (primaryAssignee.Role != UserRole.Developer && primaryAssignee.Role != UserRole.Admin)
            {
                errors.Add(new ValidationError { Field = nameof(request.PrimaryAssigneeId), Message = "Primary assignee must be a Developer or Admin" });
            }
            else if (!primaryAssignee.IsActive)
            {
                errors.Add(new ValidationError { Field = nameof(request.PrimaryAssigneeId), Message = "Primary assignee is not active" });
            }
        }

        if (!string.IsNullOrEmpty(request.SecondaryAssigneeId))
        {
            var secondaryAssignee = await _storageService.GetUserAsync(request.SecondaryAssigneeId);
            if (secondaryAssignee == null)
            {
                errors.Add(new ValidationError { Field = nameof(request.SecondaryAssigneeId), Message = "Secondary assignee not found" });
            }
            else if (secondaryAssignee.Role != UserRole.Developer && secondaryAssignee.Role != UserRole.Admin)
            {
                errors.Add(new ValidationError { Field = nameof(request.SecondaryAssigneeId), Message = "Secondary assignee must be a Developer or Admin" });
            }
            else if (!secondaryAssignee.IsActive)
            {
                errors.Add(new ValidationError { Field = nameof(request.SecondaryAssigneeId), Message = "Secondary assignee is not active" });
            }

            if (request.PrimaryAssigneeId == request.SecondaryAssigneeId)
            {
                errors.Add(new ValidationError { Field = nameof(request.SecondaryAssigneeId), Message = "Secondary assignee cannot be the same as primary assignee" });
            }
        }

        // Validate images
        foreach (var image in request.Images)
        {
            var imageErrors = await ValidateImageUploadAsync(image);
            errors.AddRange(imageErrors);
        }

        return errors;
    }

    public async Task<List<ValidationError>> ValidateUpdateBugRequestAsync(UpdateBugRequest request)
    {
        var errors = new List<ValidationError>();

        // Validate optional fields if provided
        if (!string.IsNullOrEmpty(request.Title) && request.Title.Length > 500)
        {
            errors.Add(new ValidationError { Field = nameof(request.Title), Message = "Title must be 500 characters or less" });
        }

        if (!string.IsNullOrEmpty(request.UrlMenu) && request.UrlMenu.Length > 1000)
        {
            errors.Add(new ValidationError { Field = nameof(request.UrlMenu), Message = "URL/Menu must be 1000 characters or less" });
        }

        if (!string.IsNullOrEmpty(request.Description) && request.Description.Length > 5000)
        {
            errors.Add(new ValidationError { Field = nameof(request.Description), Message = "Description must be 5000 characters or less" });
        }

        if (!string.IsNullOrEmpty(request.QaRemarks) && request.QaRemarks.Length > 2000)
        {
            errors.Add(new ValidationError { Field = nameof(request.QaRemarks), Message = "QA remarks must be 2000 characters or less" });
        }

        if (!string.IsNullOrEmpty(request.ProgrammerRemarks) && request.ProgrammerRemarks.Length > 2000)
        {
            errors.Add(new ValidationError { Field = nameof(request.ProgrammerRemarks), Message = "Programmer remarks must be 2000 characters or less" });
        }

        if (!string.IsNullOrEmpty(request.Comments) && request.Comments.Length > 1000)
        {
            errors.Add(new ValidationError { Field = nameof(request.Comments), Message = "Comments must be 1000 characters or less" });
        }

        // Validate enum values if provided
        if (request.BugType.HasValue && !Enum.IsDefined(typeof(BugType), request.BugType.Value))
        {
            errors.Add(new ValidationError { Field = nameof(request.BugType), Message = "Invalid bug type" });
        }

        if (request.Priority.HasValue && !Enum.IsDefined(typeof(Priority), request.Priority.Value))
        {
            errors.Add(new ValidationError { Field = nameof(request.Priority), Message = "Invalid priority" });
        }

        if (request.Status.HasValue && !Enum.IsDefined(typeof(DevStatus), request.Status.Value))
        {
            errors.Add(new ValidationError { Field = nameof(request.Status), Message = "Invalid status" });
        }

        // Validate assignees if provided
        if (!string.IsNullOrEmpty(request.PrimaryAssigneeId))
        {
            var primaryAssignee = await _storageService.GetUserAsync(request.PrimaryAssigneeId);
            if (primaryAssignee == null)
            {
                errors.Add(new ValidationError { Field = nameof(request.PrimaryAssigneeId), Message = "Primary assignee not found" });
            }
            else if (primaryAssignee.Role != UserRole.Developer && primaryAssignee.Role != UserRole.Admin)
            {
                errors.Add(new ValidationError { Field = nameof(request.PrimaryAssigneeId), Message = "Primary assignee must be a Developer or Admin" });
            }
        }

        if (!string.IsNullOrEmpty(request.SecondaryAssigneeId))
        {
            var secondaryAssignee = await _storageService.GetUserAsync(request.SecondaryAssigneeId);
            if (secondaryAssignee == null)
            {
                errors.Add(new ValidationError { Field = nameof(request.SecondaryAssigneeId), Message = "Secondary assignee not found" });
            }
            else if (secondaryAssignee.Role != UserRole.Developer && secondaryAssignee.Role != UserRole.Admin)
            {
                errors.Add(new ValidationError { Field = nameof(request.SecondaryAssigneeId), Message = "Secondary assignee must be a Developer or Admin" });
            }

            if (request.PrimaryAssigneeId == request.SecondaryAssigneeId)
            {
                errors.Add(new ValidationError { Field = nameof(request.SecondaryAssigneeId), Message = "Secondary assignee cannot be the same as primary assignee" });
            }
        }

        return errors;
    }

    public async Task<List<ValidationError>> ValidateCreateUserRequestAsync(CreateUserRequest request)
    {
        var errors = new List<ValidationError>();

        // Validate required fields
        if (string.IsNullOrWhiteSpace(request.Username))
        {
            errors.Add(new ValidationError { Field = nameof(request.Username), Message = "Username is required" });
        }
        else if (request.Username.Length < 3 || request.Username.Length > 50)
        {
            errors.Add(new ValidationError { Field = nameof(request.Username), Message = "Username must be between 3 and 50 characters" });
        }
        else if (!Regex.IsMatch(request.Username, "^[a-zA-Z0-9._-]+$"))
        {
            errors.Add(new ValidationError { Field = nameof(request.Username), Message = "Username can only contain letters, numbers, dots, underscores, and hyphens" });
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            errors.Add(new ValidationError { Field = nameof(request.Email), Message = "Email is required" });
        }
        else if (!new EmailAddressAttribute().IsValid(request.Email))
        {
            errors.Add(new ValidationError { Field = nameof(request.Email), Message = "Invalid email format" });
        }

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            errors.Add(new ValidationError { Field = nameof(request.FullName), Message = "Full name is required" });
        }
        else if (request.FullName.Length > 100)
        {
            errors.Add(new ValidationError { Field = nameof(request.FullName), Message = "Full name must be 100 characters or less" });
        }

        if (!Enum.IsDefined(typeof(UserRole), request.Role))
        {
            errors.Add(new ValidationError { Field = nameof(request.Role), Message = "Invalid user role" });
        }

        // Validate LeadId if provided
        if (!string.IsNullOrEmpty(request.LeadId))
        {
            var lead = await _storageService.GetUserAsync(request.LeadId);
            if (lead == null)
            {
                errors.Add(new ValidationError { Field = nameof(request.LeadId), Message = "Lead not found" });
            }
            else
            {
                // Validate appropriate lead role for user role
                var isValidLeadRole = (request.Role, lead.Role) switch
                {
                    (UserRole.Developer, UserRole.DeveloperLead) => true,
                    (UserRole.Tester, UserRole.QALead) => true,
                    _ => false
                };

                if (!isValidLeadRole)
                {
                    errors.Add(new ValidationError { Field = nameof(request.LeadId), Message = "Invalid lead role for this user role" });
                }
            }
        }
        else if (request.Role == UserRole.Developer || request.Role == UserRole.Tester)
        {
            errors.Add(new ValidationError { Field = nameof(request.LeadId), Message = "Lead is required for Developers and Testers" });
        }

        return errors;
    }

    public async Task<List<ValidationError>> ValidateUpdateUserRequestAsync(UpdateUserRequest request)
    {
        var errors = new List<ValidationError>();

        // Validate optional fields if provided
        if (!string.IsNullOrEmpty(request.FullName) && request.FullName.Length > 100)
        {
            errors.Add(new ValidationError { Field = nameof(request.FullName), Message = "Full name must be 100 characters or less" });
        }

        if (request.Role.HasValue && !Enum.IsDefined(typeof(UserRole), request.Role.Value))
        {
            errors.Add(new ValidationError { Field = nameof(request.Role), Message = "Invalid user role" });
        }

        // Validate LeadId if provided
        if (request.LeadId != null && !string.IsNullOrEmpty(request.LeadId))
        {
            var lead = await _storageService.GetUserAsync(request.LeadId);
            if (lead == null)
            {
                errors.Add(new ValidationError { Field = nameof(request.LeadId), Message = "Lead not found" });
            }
            else if (request.Role.HasValue)
            {
                // Validate appropriate lead role for user role
                var isValidLeadRole = (request.Role.Value, lead.Role) switch
                {
                    (UserRole.Developer, UserRole.DeveloperLead) => true,
                    (UserRole.Tester, UserRole.QALead) => true,
                    _ => false
                };

                if (!isValidLeadRole)
                {
                    errors.Add(new ValidationError { Field = nameof(request.LeadId), Message = "Invalid lead role for this user role" });
                }
            }
        }

        return errors;
    }

    public async Task<List<ValidationError>> ValidateStatusTransitionAsync(DevStatus currentStatus, DevStatus newStatus, UserRole userRole)
    {
        await Task.CompletedTask; // Placeholder for async signature
        var errors = new List<ValidationError>();

        // Define valid transitions based on workflow
        var validTransitions = GetValidStatusTransitions();

        if (!validTransitions.ContainsKey(currentStatus) || !validTransitions[currentStatus].Contains(newStatus))
        {
            errors.Add(new ValidationError { Field = "Status", Message = $"Invalid status transition from {currentStatus} to {newStatus}" });
        }

        return errors;
    }

    public async Task<List<ValidationError>> ValidateImageUploadAsync(BugImageUpload imageUpload)
    {
        await Task.CompletedTask; // Placeholder for async signature
        var errors = new List<ValidationError>();

        if (string.IsNullOrWhiteSpace(imageUpload.FileName))
        {
            errors.Add(new ValidationError { Field = "FileName", Message = "File name is required" });
        }

        if (imageUpload.Content == null || imageUpload.Content.Length == 0)
        {
            errors.Add(new ValidationError { Field = "Content", Message = "File content is required" });
        }
        else if (imageUpload.Content.Length > MaxImageSizeBytes)
        {
            errors.Add(new ValidationError { Field = "Content", Message = $"File size cannot exceed {MaxImageSizeBytes / (1024 * 1024)}MB" });
        }

        if (string.IsNullOrWhiteSpace(imageUpload.ContentType))
        {
            errors.Add(new ValidationError { Field = "ContentType", Message = "Content type is required" });
        }
        else if (!_allowedImageTypes.Contains(imageUpload.ContentType.ToLowerInvariant()))
        {
            errors.Add(new ValidationError { Field = "ContentType", Message = $"Unsupported image type. Allowed types: {string.Join(", ", _allowedImageTypes)}" });
        }

        return errors;
    }

    private static Dictionary<DevStatus, List<DevStatus>> GetValidStatusTransitions()
    {
        return new Dictionary<DevStatus, List<DevStatus>>
        {
            [DevStatus.Pending] = new() { DevStatus.DevRunning, DevStatus.Postpone, DevStatus.Canceled, DevStatus.Invalid },
            [DevStatus.DevRunning] = new() { DevStatus.NeedToTest, DevStatus.Postpone, DevStatus.Canceled },
            [DevStatus.NeedToTest] = new() { DevStatus.TestRunning, DevStatus.Solved, DevStatus.Pending, DevStatus.Postpone },
            [DevStatus.TestRunning] = new() { DevStatus.Solved, DevStatus.Pending, DevStatus.Postpone },
            [DevStatus.Solved] = new() { DevStatus.Pending }, // Reopen
            [DevStatus.Postpone] = new() { DevStatus.Pending, DevStatus.DevRunning, DevStatus.Canceled },
            [DevStatus.Invalid] = new() { DevStatus.Pending }, // Can be reopened if found to be valid
            [DevStatus.Canceled] = new() { } // Terminal state
        };
    }
}