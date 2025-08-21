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
    private string _currentUserId = "super-admin-1"; // Default for demo

    // Demo users for different roles
    private readonly Dictionary<string, User> _demoUsers = new()
    {
        ["super-admin-1"] = new User
        {
            Id = "super-admin-1",
            Username = "superadmin",
            Email = "admin@demo.com",
            FullName = "Super Administrator",
            Role = UserRole.SuperAdmin,
            IsActive = true
        },
        ["dev-lead-1"] = new User
        {
            Id = "dev-lead-1",
            Username = "devlead",
            Email = "devlead@demo.com",
            FullName = "Development Lead",
            Role = UserRole.DeveloperLead,
            IsActive = true
        },
        ["qa-lead-1"] = new User
        {
            Id = "qa-lead-1",
            Username = "qalead",
            Email = "qalead@demo.com",
            FullName = "QA Lead",
            Role = UserRole.QALead,
            IsActive = true
        },
        ["tester-1"] = new User
        {
            Id = "tester-1",
            Username = "tester",
            Email = "tester@demo.com",
            FullName = "QA Tester",
            Role = UserRole.Tester,
            LeadId = "qa-lead-1",
            IsActive = true
        },
        ["developer-1"] = new User
        {
            Id = "developer-1",
            Username = "developer",
            Email = "dev@demo.com",
            FullName = "Software Developer",
            Role = UserRole.Developer,
            LeadId = "dev-lead-1",
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

        // Try to get from API first, fallback to demo user
        try
        {
            var response = await _httpClient.GetAsync($"api/users/{_currentUserId}");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                // Parse and set _currentUser from API response
            }
        }
        catch
        {
            // API not available, use demo user
        }

        // Use demo user
        _currentUser = _demoUsers.TryGetValue(_currentUserId, out var user) ? user : _demoUsers["super-admin-1"];
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