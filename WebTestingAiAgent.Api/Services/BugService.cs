using WebTestingAiAgent.Core.Interfaces;
using WebTestingAiAgent.Core.Models;

namespace WebTestingAiAgent.Api.Services;

public class BugService : IBugService
{
    private readonly IBugStorageService _storageService;
    private readonly IBugAuthorizationService _authService;
    private readonly IBugValidationService _validationService;
    private readonly IUserService _userService;

    public BugService(
        IBugStorageService storageService,
        IBugAuthorizationService authService,
        IBugValidationService validationService,
        IUserService userService)
    {
        _storageService = storageService;
        _authService = authService;
        _validationService = validationService;
        _userService = userService;
    }

    public async Task<string> CreateBugAsync(CreateBugRequest request, string submitterId)
    {
        // Validate permissions
        if (!await _authService.CanCreateBugAsync(submitterId))
        {
            throw new UnauthorizedAccessException("User does not have permission to create bugs");
        }

        // Validate request
        var validationErrors = await _validationService.ValidateCreateBugRequestAsync(request);
        if (validationErrors.Any())
        {
            throw new ArgumentException($"Validation failed: {string.Join(", ", validationErrors.Select(e => e.Message))}");
        }

        var bug = new Bug
        {
            Id = Guid.NewGuid().ToString(),
            Title = request.Title,
            Description = request.Description,
            UrlMenu = request.UrlMenu,
            BugType = request.BugType,
            Priority = request.Priority,
            Status = DevStatus.Pending,
            SubmittedById = submitterId,
            SubmittedAt = DateTime.UtcNow
        };

        var bugId = await _storageService.SaveBugAsync(bug);

        // Handle assignments
        if (!string.IsNullOrEmpty(request.PrimaryAssigneeId))
        {
            await AssignBugAsync(bugId, request.PrimaryAssigneeId, request.SecondaryAssigneeId, submitterId);
        }

        // Save initial status history
        var statusHistory = new BugStatusHistory
        {
            Id = Guid.NewGuid().ToString(),
            BugId = bugId,
            OldStatus = DevStatus.Pending,
            NewStatus = DevStatus.Pending,
            ChangedById = submitterId,
            ChangedAt = DateTime.UtcNow,
            Comments = "Bug created"
        };
        await _storageService.SaveBugStatusHistoryAsync(statusHistory);

        return bugId;
    }

    public async Task<BugResponse?> GetBugAsync(string bugId)
    {
        var bug = await _storageService.GetBugAsync(bugId);
        if (bug == null) return null;

        return await MapBugToResponseAsync(bug);
    }

    public async Task<List<BugResponse>> GetBugsAsync(BugListRequest request, string? requesterId = null)
    {
        var bugs = await _storageService.GetBugsAsync(request, requesterId);
        var responses = new List<BugResponse>();

        foreach (var bug in bugs)
        {
            responses.Add(await MapBugToResponseAsync(bug));
        }

        return responses;
    }

    public async Task<bool> UpdateBugAsync(string bugId, UpdateBugRequest request, string updaterId)
    {
        var bug = await _storageService.GetBugAsync(bugId);
        if (bug == null) return false;

        if (!await _authService.CanEditBugAsync(updaterId, bugId))
        {
            throw new UnauthorizedAccessException("User does not have permission to edit this bug");
        }

        var validationErrors = await _validationService.ValidateUpdateBugRequestAsync(request);
        if (validationErrors.Any())
        {
            throw new ArgumentException($"Validation failed: {string.Join(", ", validationErrors.Select(e => e.Message))}");
        }

        var originalStatus = bug.Status;
        var statusChanged = false;

        // Update fields
        if (!string.IsNullOrEmpty(request.Title)) bug.Title = request.Title;
        if (!string.IsNullOrEmpty(request.Description)) bug.Description = request.Description;
        if (!string.IsNullOrEmpty(request.UrlMenu)) bug.UrlMenu = request.UrlMenu;
        if (request.BugType.HasValue) bug.BugType = request.BugType.Value;
        if (request.Priority.HasValue) bug.Priority = request.Priority.Value;
        if (!string.IsNullOrEmpty(request.QaRemarks)) bug.QaRemarks = request.QaRemarks;
        if (!string.IsNullOrEmpty(request.ProgrammerRemarks)) bug.ProgrammerRemarks = request.ProgrammerRemarks;

        // Handle status change
        if (request.Status.HasValue && request.Status.Value != originalStatus)
        {
            if (!await _authService.CanUpdateBugStatusAsync(updaterId, bugId, request.Status.Value))
            {
                throw new UnauthorizedAccessException("User does not have permission to set this status");
            }

            bug.Status = request.Status.Value;
            statusChanged = true;

            // Track reopen count
            if (originalStatus == DevStatus.Solved && request.Status.Value == DevStatus.Pending)
            {
                bug.ReopenCount++;
            }

            // Update QA check time if status changes
            if (IsQAStatus(request.Status.Value))
            {
                bug.QaLastCheckTime = DateTime.UtcNow;
            }
        }

        await _storageService.UpdateBugAsync(bug);

        // Log status change
        if (statusChanged)
        {
            var statusHistory = new BugStatusHistory
            {
                Id = Guid.NewGuid().ToString(),
                BugId = bugId,
                OldStatus = originalStatus,
                NewStatus = bug.Status,
                ChangedById = updaterId,
                ChangedAt = DateTime.UtcNow,
                Comments = request.Comments ?? ""
            };
            await _storageService.SaveBugStatusHistoryAsync(statusHistory);
        }

        // Handle assignment changes
        if (!string.IsNullOrEmpty(request.PrimaryAssigneeId) || !string.IsNullOrEmpty(request.SecondaryAssigneeId))
        {
            await AssignBugAsync(bugId, request.PrimaryAssigneeId!, request.SecondaryAssigneeId, updaterId);
        }

        return true;
    }

    public async Task<bool> DeleteBugAsync(string bugId, string deleterId)
    {
        if (!await _authService.CanDeleteBugAsync(deleterId, bugId))
        {
            throw new UnauthorizedAccessException("User does not have permission to delete this bug");
        }

        return await _storageService.DeleteBugAsync(bugId);
    }

    public async Task<bool> AssignBugAsync(string bugId, string primaryAssigneeId, string? secondaryAssigneeId, string assignerId)
    {
        if (!await _authService.CanAssignBugAsync(assignerId, bugId, primaryAssigneeId))
        {
            throw new UnauthorizedAccessException("User does not have permission to assign this bug");
        }

        // Remove existing assignments
        await _storageService.DeleteBugAssignmentsAsync(bugId);

        // Add primary assignment
        var primaryAssignment = new BugAssignment
        {
            Id = Guid.NewGuid().ToString(),
            BugId = bugId,
            AssigneeId = primaryAssigneeId,
            IsPrimary = true,
            AssignedAt = DateTime.UtcNow,
            AssignedById = assignerId
        };
        await _storageService.SaveBugAssignmentAsync(primaryAssignment);

        // Add secondary assignment if provided
        if (!string.IsNullOrEmpty(secondaryAssigneeId))
        {
            var secondaryAssignment = new BugAssignment
            {
                Id = Guid.NewGuid().ToString(),
                BugId = bugId,
                AssigneeId = secondaryAssigneeId,
                IsPrimary = false,
                AssignedAt = DateTime.UtcNow,
                AssignedById = assignerId
            };
            await _storageService.SaveBugAssignmentAsync(secondaryAssignment);
        }

        return true;
    }

    public async Task<bool> UpdateBugStatusAsync(string bugId, DevStatus newStatus, string updaterId, string? comments = null)
    {
        var request = new UpdateBugRequest
        {
            Status = newStatus,
            Comments = comments
        };

        return await UpdateBugAsync(bugId, request, updaterId);
    }

    public async Task<List<BugStatusHistory>> GetBugStatusHistoryAsync(string bugId)
    {
        return await _storageService.GetBugStatusHistoryAsync(bugId);
    }

    private async Task<BugResponse> MapBugToResponseAsync(Bug bug)
    {
        var submitter = await _userService.GetUserAsync(bug.SubmittedById);
        var assignments = await _storageService.GetBugAssignmentsAsync(bug.Id);
        var images = await _storageService.GetBugImagesAsync(bug.Id);

        var primaryAssignment = assignments.FirstOrDefault(a => a.IsPrimary);
        var secondaryAssignment = assignments.FirstOrDefault(a => !a.IsPrimary);

        UserResponse? primaryAssignee = null;
        UserResponse? secondaryAssignee = null;

        if (primaryAssignment != null)
        {
            primaryAssignee = await _userService.GetUserAsync(primaryAssignment.AssigneeId);
        }

        if (secondaryAssignment != null)
        {
            secondaryAssignee = await _userService.GetUserAsync(secondaryAssignment.AssigneeId);
        }

        return new BugResponse
        {
            Id = bug.Id,
            Title = bug.Title,
            Description = bug.Description,
            UrlMenu = bug.UrlMenu,
            BugType = bug.BugType,
            Priority = bug.Priority,
            Status = bug.Status,
            SubmittedById = bug.SubmittedById,
            SubmittedByName = submitter?.FullName ?? "Unknown",
            SubmittedAt = bug.SubmittedAt,
            QaLastCheckTime = bug.QaLastCheckTime,
            QaRemarks = bug.QaRemarks,
            ProgrammerRemarks = bug.ProgrammerRemarks,
            ReopenCount = bug.ReopenCount,
            PrimaryAssigneeName = primaryAssignee?.FullName,
            SecondaryAssigneeName = secondaryAssignee?.FullName,
            Images = images.Select(i => new BugImageResponse
            {
                Id = i.Id,
                FileName = i.FileName,
                Label = i.Label,
                DownloadUrl = $"/api/bugs/images/{i.Id}/download",
                UploadedAt = i.UploadedAt,
                FileSize = i.FileSize
            }).ToList()
        };
    }

    private static bool IsQAStatus(DevStatus status)
    {
        return status == DevStatus.NeedToTest || status == DevStatus.TestRunning || status == DevStatus.Solved;
    }
}