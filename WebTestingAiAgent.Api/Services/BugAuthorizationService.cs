using WebTestingAiAgent.Core.Interfaces;
using WebTestingAiAgent.Core.Models;

namespace WebTestingAiAgent.Api.Services;

public class BugAuthorizationService : IBugAuthorizationService
{
    private readonly IBugStorageService _storageService;

    public BugAuthorizationService(IBugStorageService storageService)
    {
        _storageService = storageService;
    }

    public async Task<bool> CanCreateBugAsync(string userId)
    {
        var user = await _storageService.GetUserAsync(userId);
        if (user == null || !user.IsActive) return false;

        // Only Testers can create bugs
        return user.Role == UserRole.Tester;
    }

    public async Task<bool> CanEditBugAsync(string userId, string bugId)
    {
        var user = await _storageService.GetUserAsync(userId);
        if (user == null || !user.IsActive) return false;

        var bug = await _storageService.GetBugAsync(bugId);
        if (bug == null) return false;

        return user.Role switch
        {
            UserRole.SuperAdmin => true,
            UserRole.Admin => true,
            UserRole.Tester => bug.SubmittedById == userId, // Testers can edit their own bugs
            UserRole.Developer => await IsAssignedToBugAsync(userId, bugId), // Developers can edit assigned bugs
            UserRole.DeveloperLead => await IsLeadOfAssignedDeveloperAsync(userId, bugId),
            UserRole.QALead => await IsLeadOfSubmitterAsync(userId, bugId),
            _ => false
        };
    }

    public async Task<bool> CanDeleteBugAsync(string userId, string bugId)
    {
        var user = await _storageService.GetUserAsync(userId);
        if (user == null || !user.IsActive) return false;

        return user.Role switch
        {
            UserRole.SuperAdmin => true,
            UserRole.Admin => true,
            _ => false // Only Super Admin and Admin can delete bugs
        };
    }

    public async Task<bool> CanAssignBugAsync(string userId, string bugId, string assigneeId)
    {
        var user = await _storageService.GetUserAsync(userId);
        if (user == null || !user.IsActive) return false;

        var assignee = await _storageService.GetUserAsync(assigneeId);
        if (assignee == null || !assignee.IsActive) return false;

        var bug = await _storageService.GetBugAsync(bugId);
        if (bug == null) return false;

        return user.Role switch
        {
            UserRole.SuperAdmin => true,
            UserRole.Admin => true,
            UserRole.Tester => bug.SubmittedById == userId && assignee.Role == UserRole.Developer, // Testers can assign their bugs to Developers
            UserRole.DeveloperLead => assignee.Role == UserRole.Developer && assignee.LeadId == userId, // Developer Leads can assign to their Developers
            UserRole.QALead => await IsLeadOfSubmitterAsync(userId, bugId), // QA Leads can assign bugs submitted by their Testers
            _ => false
        };
    }

    public async Task<bool> CanUpdateBugStatusAsync(string userId, string bugId, DevStatus newStatus)
    {
        var user = await _storageService.GetUserAsync(userId);
        if (user == null || !user.IsActive) return false;

        var bug = await _storageService.GetBugAsync(bugId);
        if (bug == null) return false;

        var allowedStatuses = await GetAllowedStatusTransitionsAsync(userId, bugId, bug.Status);
        return allowedStatuses.Contains(newStatus);
    }

    public async Task<bool> CanManageUserAsync(string userId, string targetUserId)
    {
        var user = await _storageService.GetUserAsync(userId);
        if (user == null || !user.IsActive) return false;

        var targetUser = await _storageService.GetUserAsync(targetUserId);
        if (targetUser == null) return false;

        return user.Role switch
        {
            UserRole.SuperAdmin => true, // Super Admin can manage all users
            UserRole.DeveloperLead => targetUser.Role == UserRole.Developer && targetUser.LeadId == userId, // Developer Leads manage their Developers
            UserRole.QALead => targetUser.Role == UserRole.Tester && targetUser.LeadId == userId, // QA Leads manage their Testers
            _ => userId == targetUserId // Users can manage themselves
        };
    }

    public async Task<bool> CanCreateUserAsync(string userId, UserRole targetRole)
    {
        var user = await _storageService.GetUserAsync(userId);
        if (user == null || !user.IsActive) return false;

        return user.Role switch
        {
            UserRole.SuperAdmin => true, // Super Admin can create any user
            UserRole.DeveloperLead => targetRole == UserRole.Developer, // Developer Leads can add Developers
            UserRole.QALead => targetRole == UserRole.Tester, // QA Leads can add Testers
            _ => false
        };
    }

    public async Task<List<DevStatus>> GetAllowedStatusTransitionsAsync(string userId, string bugId, DevStatus currentStatus)
    {
        var user = await _storageService.GetUserAsync(userId);
        if (user == null || !user.IsActive) return new List<DevStatus>();

        var bug = await _storageService.GetBugAsync(bugId);
        if (bug == null) return new List<DevStatus>();

        var allowedStatuses = new List<DevStatus>();

        switch (user.Role)
        {
            case UserRole.SuperAdmin:
            case UserRole.Admin:
                // Admin can set all statuses
                allowedStatuses.AddRange(Enum.GetValues<DevStatus>());
                break;

            case UserRole.Tester:
                if (bug.SubmittedById == userId) // Only for bugs they submitted
                {
                    allowedStatuses.AddRange(new[]
                    {
                        DevStatus.Pending,
                        DevStatus.NeedToTest,
                        DevStatus.TestRunning,
                        DevStatus.Solved,
                        DevStatus.Postpone,
                        DevStatus.Canceled
                    });
                }
                break;

            case UserRole.Developer:
                if (await IsAssignedToBugAsync(userId, bugId)) // Only for bugs assigned to them
                {
                    allowedStatuses.AddRange(new[]
                    {
                        DevStatus.DevRunning,
                        DevStatus.NeedToTest,
                        DevStatus.Postpone,
                        DevStatus.Invalid,
                        DevStatus.Canceled
                    });
                }
                break;

            case UserRole.DeveloperLead:
                if (await IsLeadOfAssignedDeveloperAsync(userId, bugId))
                {
                    allowedStatuses.AddRange(new[]
                    {
                        DevStatus.DevRunning,
                        DevStatus.NeedToTest,
                        DevStatus.Postpone,
                        DevStatus.Invalid,
                        DevStatus.Canceled
                    });
                }
                break;

            case UserRole.QALead:
                if (await IsLeadOfSubmitterAsync(userId, bugId))
                {
                    allowedStatuses.AddRange(new[]
                    {
                        DevStatus.Pending,
                        DevStatus.NeedToTest,
                        DevStatus.TestRunning,
                        DevStatus.Solved,
                        DevStatus.Postpone,
                        DevStatus.Canceled
                    });
                }
                break;
        }

        return allowedStatuses.Distinct().ToList();
    }

    private async Task<bool> IsAssignedToBugAsync(string userId, string bugId)
    {
        var assignments = await _storageService.GetBugAssignmentsAsync(bugId);
        return assignments.Any(a => a.AssigneeId == userId);
    }

    private async Task<bool> IsLeadOfAssignedDeveloperAsync(string leadId, string bugId)
    {
        var assignments = await _storageService.GetBugAssignmentsAsync(bugId);
        
        foreach (var assignment in assignments)
        {
            var assignee = await _storageService.GetUserAsync(assignment.AssigneeId);
            if (assignee?.Role == UserRole.Developer && assignee.LeadId == leadId)
            {
                return true;
            }
        }
        
        return false;
    }

    private async Task<bool> IsLeadOfSubmitterAsync(string leadId, string bugId)
    {
        var bug = await _storageService.GetBugAsync(bugId);
        if (bug == null) return false;

        var submitter = await _storageService.GetUserAsync(bug.SubmittedById);
        return submitter?.Role == UserRole.Tester && submitter.LeadId == leadId;
    }
}