using WebTestingAiAgent.Core.Models;

namespace WebTestingAiAgent.Web.Services;

public interface IUserContextService
{
    Task<User?> GetCurrentUserAsync();
    Task SetCurrentUserAsync(string userId);
    bool IsInRole(UserRole role);
    bool CanManageUsers();
    bool CanViewBugs();
    bool CanCreateBugs();
    bool CanEditBugs();
    bool CanAssignBugs();
    bool CanUpdateBugStatus();
}

public class UserContextService : IUserContextService
{
    private readonly HttpClient _httpClient;
    private User? _currentUser;
    private string _currentUserId = string.Empty;

    // Only one hardcoded super admin user
    private readonly Dictionary<string, User> _demoUsers = new()
    {
        ["super-admin-1"] = new User
        {
            Id = "super-admin-1",
            Username = "superadmin",
            Email = "admin@bugtracksystem.com",
            FullName = "Super Administrator",
            Role = UserRole.SuperAdmin,
            Password = "admin123", // In real app, this would be hashed
            IsActive = true
        }
    };

    public UserContextService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<User?> GetCurrentUserAsync()
    {
        if (_currentUser != null) 
            return _currentUser;

        if (string.IsNullOrEmpty(_currentUserId))
            return null;

        // Try to get from API first, fallback to demo user
        try
        {
            var response = await _httpClient.GetAsync($"api/users/{_currentUserId}");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                // Parse and set _currentUser from API response
                // For now, we'll use demo users
            }
        }
        catch
        {
            // API not available, use demo user
        }

        // Use demo user or return null if not found
        _currentUser = _demoUsers.TryGetValue(_currentUserId, out var user) ? user : null;
        return _currentUser;
    }

    public async Task SetCurrentUserAsync(string userId)
    {
        _currentUserId = userId;
        _currentUser = null; // Force refresh
        await GetCurrentUserAsync();
    }

    public bool IsInRole(UserRole role)
    {
        var user = GetCurrentUserAsync().Result;
        return user?.Role == role;
    }

    public bool CanManageUsers()
    {
        var user = GetCurrentUserAsync().Result;
        return user?.Role switch
        {
            UserRole.SuperAdmin => true,
            UserRole.DeveloperLead => true, // Can manage developers
            UserRole.QALead => true, // Can manage testers
            _ => false
        };
    }

    public bool CanViewBugs()
    {
        // All authenticated users can view bugs
        return GetCurrentUserAsync().Result != null;
    }

    public bool CanCreateBugs()
    {
        var user = GetCurrentUserAsync().Result;
        return user?.Role switch
        {
            UserRole.SuperAdmin => true,
            UserRole.Tester => true, // Testers can submit bugs
            UserRole.QALead => true, // QA Leads can create bugs
            _ => false
        };
    }

    public bool CanEditBugs()
    {
        var user = GetCurrentUserAsync().Result;
        return user?.Role switch
        {
            UserRole.SuperAdmin => true, // Can edit any bug
            UserRole.Tester => true, // Can edit their own bugs
            UserRole.QALead => true, // Can edit bugs from their testers
            UserRole.DeveloperLead => true, // Can edit bugs assigned to their developers
            _ => false
        };
    }

    public bool CanAssignBugs()
    {
        var user = GetCurrentUserAsync().Result;
        return user?.Role switch
        {
            UserRole.SuperAdmin => true,
            UserRole.Tester => true, // Can assign bugs to developers
            UserRole.QALead => true, // Can assign bugs to testers
            UserRole.DeveloperLead => true, // Can assign bugs to developers
            _ => false
        };
    }

    public bool CanUpdateBugStatus()
    {
        var user = GetCurrentUserAsync().Result;
        return user?.Role switch
        {
            UserRole.SuperAdmin => true,
            UserRole.Developer => true, // Can update status of assigned bugs
            UserRole.Tester => true, // Can update status and reopen bugs
            UserRole.DeveloperLead => true, // Can update status of team's bugs
            UserRole.QALead => true, // Can update status of team's bugs
            _ => false
        };
    }
}