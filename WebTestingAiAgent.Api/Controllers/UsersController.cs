using Microsoft.AspNetCore.Mvc;
using WebTestingAiAgent.Core.Interfaces;
using WebTestingAiAgent.Core.Models;

namespace WebTestingAiAgent.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IBugAuthorizationService _authService;

    public UsersController(IUserService userService, IBugAuthorizationService authService)
    {
        _userService = userService;
        _authService = authService;
    }

    /// <summary>
    /// Create a new user
    /// </summary>
    [HttpPost]
    public async Task<ActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        try
        {
            var creatorId = GetCurrentUserId();
            var userId = await _userService.CreateUserAsync(request, creatorId);
            
            return Ok(new { UserId = userId, Message = "User created successfully" });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiErrorResponse { Message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiErrorResponse { Message = "An error occurred while creating the user", Errors = new List<ValidationError> { new() { Field = "General", Message = ex.Message } } });
        }
    }

    /// <summary>
    /// Get a user by ID
    /// </summary>
    [HttpGet("{userId}")]
    public async Task<ActionResult<UserResponse>> GetUser(string userId)
    {
        try
        {
            var user = await _userService.GetUserAsync(userId);
            if (user == null)
            {
                return NotFound(new ApiErrorResponse { Message = "User not found" });
            }

            return Ok(user);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiErrorResponse { Message = "An error occurred while retrieving the user", Errors = new List<ValidationError> { new() { Field = "General", Message = ex.Message } } });
        }
    }

    /// <summary>
    /// Get users with optional filtering
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<UserResponse>>> GetUsers(
        [FromQuery] UserRole? role = null,
        [FromQuery] string? leadId = null)
    {
        try
        {
            var users = await _userService.GetUsersAsync(role, leadId);
            return Ok(users);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiErrorResponse { Message = "An error occurred while retrieving users", Errors = new List<ValidationError> { new() { Field = "General", Message = ex.Message } } });
        }
    }

    /// <summary>
    /// Get users by lead
    /// </summary>
    [HttpGet("by-lead/{leadId}")]
    public async Task<ActionResult<List<UserResponse>>> GetUsersByLead(string leadId)
    {
        try
        {
            var users = await _userService.GetUsersByLeadAsync(leadId);
            return Ok(users);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiErrorResponse { Message = "An error occurred while retrieving users by lead", Errors = new List<ValidationError> { new() { Field = "General", Message = ex.Message } } });
        }
    }

    /// <summary>
    /// Get current user information
    /// </summary>
    [HttpGet("me")]
    public async Task<ActionResult<UserResponse>> GetCurrentUser()
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            var user = await _userService.GetUserAsync(currentUserId);
            
            if (user == null)
            {
                return NotFound(new ApiErrorResponse { Message = "Current user not found" });
            }

            return Ok(user);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiErrorResponse { Message = "An error occurred while retrieving current user", Errors = new List<ValidationError> { new() { Field = "General", Message = ex.Message } } });
        }
    }

    /// <summary>
    /// Update a user
    /// </summary>
    [HttpPut("{userId}")]
    public async Task<ActionResult> UpdateUser(string userId, [FromBody] UpdateUserRequest request)
    {
        try
        {
            var updaterId = GetCurrentUserId();
            var success = await _userService.UpdateUserAsync(userId, request, updaterId);
            
            if (!success)
            {
                return NotFound(new ApiErrorResponse { Message = "User not found" });
            }

            return Ok(new { Message = "User updated successfully" });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiErrorResponse { Message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiErrorResponse { Message = "An error occurred while updating the user", Errors = new List<ValidationError> { new() { Field = "General", Message = ex.Message } } });
        }
    }

    /// <summary>
    /// Delete a user
    /// </summary>
    [HttpDelete("{userId}")]
    public async Task<ActionResult> DeleteUser(string userId)
    {
        try
        {
            var deleterId = GetCurrentUserId();
            var success = await _userService.DeleteUserAsync(userId, deleterId);
            
            if (!success)
            {
                return NotFound(new ApiErrorResponse { Message = "User not found" });
            }

            return Ok(new { Message = "User deleted successfully" });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiErrorResponse { Message = "An error occurred while deleting the user", Errors = new List<ValidationError> { new() { Field = "General", Message = ex.Message } } });
        }
    }

    /// <summary>
    /// Get developers under a lead
    /// </summary>
    [HttpGet("developers")]
    public async Task<ActionResult<List<UserResponse>>> GetDevelopers([FromQuery] string? leadId = null)
    {
        try
        {
            var developers = await _userService.GetUsersAsync(UserRole.Developer, leadId);
            return Ok(developers);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiErrorResponse { Message = "An error occurred while retrieving developers", Errors = new List<ValidationError> { new() { Field = "General", Message = ex.Message } } });
        }
    }

    /// <summary>
    /// Get testers under a lead
    /// </summary>
    [HttpGet("testers")]
    public async Task<ActionResult<List<UserResponse>>> GetTesters([FromQuery] string? leadId = null)
    {
        try
        {
            var testers = await _userService.GetUsersAsync(UserRole.Tester, leadId);
            return Ok(testers);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiErrorResponse { Message = "An error occurred while retrieving testers", Errors = new List<ValidationError> { new() { Field = "General", Message = ex.Message } } });
        }
    }

    /// <summary>
    /// Get available roles for user creation
    /// </summary>
    [HttpGet("roles")]
    public async Task<ActionResult<List<object>>> GetAvailableRoles()
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            var availableRoles = new List<object>();

            // Check what roles the current user can create
            foreach (UserRole role in Enum.GetValues<UserRole>())
            {
                if (await _authService.CanCreateUserAsync(currentUserId, role))
                {
                    availableRoles.Add(new { 
                        Value = (int)role, 
                        Name = role.ToString(),
                        DisplayName = GetRoleDisplayName(role)
                    });
                }
            }

            return Ok(availableRoles);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiErrorResponse { Message = "An error occurred while retrieving available roles", Errors = new List<ValidationError> { new() { Field = "General", Message = ex.Message } } });
        }
    }

    /// <summary>
    /// Get leads for a specific role type
    /// </summary>
    [HttpGet("leads")]
    public async Task<ActionResult<List<UserResponse>>> GetLeads([FromQuery] string roleType = "")
    {
        try
        {
            var leads = new List<UserResponse>();

            if (roleType.ToLowerInvariant() == "developer" || string.IsNullOrEmpty(roleType))
            {
                var devLeads = await _userService.GetUsersAsync(UserRole.DeveloperLead);
                leads.AddRange(devLeads);
            }

            if (roleType.ToLowerInvariant() == "tester" || string.IsNullOrEmpty(roleType))
            {
                var qaLeads = await _userService.GetUsersAsync(UserRole.QALead);
                leads.AddRange(qaLeads);
            }

            return Ok(leads.OrderBy(l => l.FullName).ToList());
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiErrorResponse { Message = "An error occurred while retrieving leads", Errors = new List<ValidationError> { new() { Field = "General", Message = ex.Message } } });
        }
    }

    private string GetCurrentUserId()
    {
        // TODO: Implement proper authentication and get user ID from JWT token or session
        // For now, return a default user ID for testing
        return "super-admin-1"; // Default to super admin for testing user management
    }

    private static string GetRoleDisplayName(UserRole role)
    {
        return role switch
        {
            UserRole.SuperAdmin => "Super Administrator",
            UserRole.DeveloperLead => "Developer Lead",
            UserRole.QALead => "QA Lead",
            UserRole.Tester => "Tester",
            UserRole.Developer => "Developer",
            UserRole.Admin => "Administrator",
            _ => role.ToString()
        };
    }
}