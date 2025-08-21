using WebTestingAiAgent.Core.Interfaces;
using WebTestingAiAgent.Core.Models;

namespace WebTestingAiAgent.Api.Services;

public class BugStorageService : IBugStorageService
{
    private readonly Dictionary<string, Bug> _bugs = new();
    private readonly Dictionary<string, User> _users = new();
    private readonly Dictionary<string, List<BugAssignment>> _bugAssignments = new();
    private readonly Dictionary<string, List<BugStatusHistory>> _bugStatusHistory = new();
    private readonly Dictionary<string, List<BugImage>> _bugImages = new();
    private readonly Dictionary<string, User> _usersByUsername = new();

    public BugStorageService()
    {
        // Initialize with some default users
        InitializeDefaultUsers();
    }

    private void InitializeDefaultUsers()
    {
        var superAdmin = new User
        {
            Id = "super-admin-1",
            Username = "superadmin",
            Email = "superadmin@company.com",
            FullName = "Super Administrator",
            Role = UserRole.SuperAdmin,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        var devLead = new User
        {
            Id = "dev-lead-1",
            Username = "devlead",
            Email = "devlead@company.com",
            FullName = "Developer Lead",
            Role = UserRole.DeveloperLead,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        var qaLead = new User
        {
            Id = "qa-lead-1",
            Username = "qalead",
            Email = "qalead@company.com",
            FullName = "QA Lead",
            Role = UserRole.QALead,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        var developer = new User
        {
            Id = "developer-1",
            Username = "developer1",
            Email = "developer1@company.com",
            FullName = "John Developer",
            Role = UserRole.Developer,
            LeadId = devLead.Id,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        var tester = new User
        {
            Id = "tester-1",
            Username = "tester1",
            Email = "tester1@company.com",
            FullName = "Jane Tester",
            Role = UserRole.Tester,
            LeadId = qaLead.Id,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        var users = new[] { superAdmin, devLead, qaLead, developer, tester };

        foreach (var user in users)
        {
            _users[user.Id] = user;
            _usersByUsername[user.Username.ToLowerInvariant()] = user;
        }
    }

    #region Bug Operations

    public async Task<string> SaveBugAsync(Bug bug)
    {
        await Task.CompletedTask;
        _bugs[bug.Id] = bug;
        return bug.Id;
    }

    public async Task<Bug?> GetBugAsync(string bugId)
    {
        await Task.CompletedTask;
        return _bugs.TryGetValue(bugId, out var bug) ? bug : null;
    }

    public async Task<List<Bug>> GetBugsAsync(BugListRequest request, string? requesterId = null)
    {
        await Task.CompletedTask;
        
        var bugs = _bugs.Values.AsQueryable();

        // Apply filters
        if (!string.IsNullOrEmpty(request.AssigneeId))
        {
            var bugIdsWithAssignee = _bugAssignments
                .Where(kv => kv.Value.Any(a => a.AssigneeId == request.AssigneeId))
                .Select(kv => kv.Key)
                .ToHashSet();
            bugs = bugs.Where(b => bugIdsWithAssignee.Contains(b.Id));
        }

        if (!string.IsNullOrEmpty(request.SubmittedById))
        {
            bugs = bugs.Where(b => b.SubmittedById == request.SubmittedById);
        }

        if (request.Status.HasValue)
        {
            bugs = bugs.Where(b => b.Status == request.Status.Value);
        }

        if (request.Priority.HasValue)
        {
            bugs = bugs.Where(b => b.Priority == request.Priority.Value);
        }

        if (request.BugType.HasValue)
        {
            bugs = bugs.Where(b => b.BugType == request.BugType.Value);
        }

        if (!string.IsNullOrEmpty(request.SearchTerm))
        {
            var searchTerm = request.SearchTerm.ToLowerInvariant();
            bugs = bugs.Where(b => 
                b.Title.ToLowerInvariant().Contains(searchTerm) ||
                b.Description.ToLowerInvariant().Contains(searchTerm) ||
                b.UrlMenu.ToLowerInvariant().Contains(searchTerm));
        }

        // Apply pagination
        var totalCount = bugs.Count();
        var pagedBugs = bugs
            .OrderByDescending(b => b.SubmittedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        return pagedBugs;
    }

    public async Task<bool> UpdateBugAsync(Bug bug)
    {
        await Task.CompletedTask;
        
        if (!_bugs.ContainsKey(bug.Id))
            return false;

        _bugs[bug.Id] = bug;
        return true;
    }

    public async Task<bool> DeleteBugAsync(string bugId)
    {
        await Task.CompletedTask;
        
        var removed = _bugs.Remove(bugId);
        if (removed)
        {
            // Clean up related data
            _bugAssignments.Remove(bugId);
            _bugStatusHistory.Remove(bugId);
            _bugImages.Remove(bugId);
        }
        
        return removed;
    }

    #endregion

    #region User Operations

    public async Task<string> SaveUserAsync(User user)
    {
        await Task.CompletedTask;
        _users[user.Id] = user;
        _usersByUsername[user.Username.ToLowerInvariant()] = user;
        return user.Id;
    }

    public async Task<User?> GetUserAsync(string userId)
    {
        await Task.CompletedTask;
        return _users.TryGetValue(userId, out var user) ? user : null;
    }

    public async Task<List<User>> GetUsersAsync(UserRole? role = null, string? leadId = null)
    {
        await Task.CompletedTask;
        
        var users = _users.Values.AsQueryable();

        if (role.HasValue)
        {
            users = users.Where(u => u.Role == role.Value);
        }

        if (!string.IsNullOrEmpty(leadId))
        {
            users = users.Where(u => u.LeadId == leadId);
        }

        return users.OrderBy(u => u.FullName).ToList();
    }

    public async Task<bool> UpdateUserAsync(User user)
    {
        await Task.CompletedTask;
        
        if (!_users.ContainsKey(user.Id))
            return false;

        // Update username index if changed
        var oldUser = _users[user.Id];
        if (oldUser.Username != user.Username)
        {
            _usersByUsername.Remove(oldUser.Username.ToLowerInvariant());
            _usersByUsername[user.Username.ToLowerInvariant()] = user;
        }

        _users[user.Id] = user;
        return true;
    }

    public async Task<bool> DeleteUserAsync(string userId)
    {
        await Task.CompletedTask;
        
        if (!_users.TryGetValue(userId, out var user))
            return false;

        _users.Remove(userId);
        _usersByUsername.Remove(user.Username.ToLowerInvariant());
        return true;
    }

    public async Task<User?> GetUserByUsernameAsync(string username)
    {
        await Task.CompletedTask;
        return _usersByUsername.TryGetValue(username.ToLowerInvariant(), out var user) ? user : null;
    }

    #endregion

    #region Bug Assignment Operations

    public async Task<bool> SaveBugAssignmentAsync(BugAssignment assignment)
    {
        await Task.CompletedTask;
        
        if (!_bugAssignments.ContainsKey(assignment.BugId))
        {
            _bugAssignments[assignment.BugId] = new List<BugAssignment>();
        }

        _bugAssignments[assignment.BugId].Add(assignment);
        return true;
    }

    public async Task<List<BugAssignment>> GetBugAssignmentsAsync(string bugId)
    {
        await Task.CompletedTask;
        return _bugAssignments.TryGetValue(bugId, out var assignments) ? assignments : new List<BugAssignment>();
    }

    public async Task<bool> DeleteBugAssignmentsAsync(string bugId)
    {
        await Task.CompletedTask;
        return _bugAssignments.Remove(bugId);
    }

    #endregion

    #region Bug Status History Operations

    public async Task<bool> SaveBugStatusHistoryAsync(BugStatusHistory statusHistory)
    {
        await Task.CompletedTask;
        
        if (!_bugStatusHistory.ContainsKey(statusHistory.BugId))
        {
            _bugStatusHistory[statusHistory.BugId] = new List<BugStatusHistory>();
        }

        _bugStatusHistory[statusHistory.BugId].Add(statusHistory);
        return true;
    }

    public async Task<List<BugStatusHistory>> GetBugStatusHistoryAsync(string bugId)
    {
        await Task.CompletedTask;
        
        var history = _bugStatusHistory.TryGetValue(bugId, out var statusHistory) 
            ? statusHistory 
            : new List<BugStatusHistory>();

        return history.OrderBy(h => h.ChangedAt).ToList();
    }

    #endregion

    #region Bug Image Operations

    public async Task<string> SaveBugImageAsync(BugImage image)
    {
        await Task.CompletedTask;
        
        if (!_bugImages.ContainsKey(image.BugId))
        {
            _bugImages[image.BugId] = new List<BugImage>();
        }

        _bugImages[image.BugId].Add(image);
        return image.Id;
    }

    public async Task<BugImage?> GetBugImageAsync(string imageId)
    {
        await Task.CompletedTask;
        
        return _bugImages.Values
            .SelectMany(images => images)
            .FirstOrDefault(img => img.Id == imageId);
    }

    public async Task<List<BugImage>> GetBugImagesAsync(string bugId)
    {
        await Task.CompletedTask;
        
        return _bugImages.TryGetValue(bugId, out var images) 
            ? images.OrderBy(img => img.UploadedAt).ToList() 
            : new List<BugImage>();
    }

    public async Task<bool> DeleteBugImageAsync(string imageId)
    {
        await Task.CompletedTask;
        
        foreach (var bugImages in _bugImages.Values)
        {
            var image = bugImages.FirstOrDefault(img => img.Id == imageId);
            if (image != null)
            {
                bugImages.Remove(image);
                return true;
            }
        }
        
        return false;
    }

    #endregion
}