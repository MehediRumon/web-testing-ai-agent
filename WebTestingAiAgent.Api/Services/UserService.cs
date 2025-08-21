using WebTestingAiAgent.Core.Interfaces;
using WebTestingAiAgent.Core.Models;

namespace WebTestingAiAgent.Api.Services;

public class UserService : IUserService
{
    private readonly IBugStorageService _storageService;
    private readonly IBugAuthorizationService _authService;
    private readonly IBugValidationService _validationService;

    public UserService(
        IBugStorageService storageService,
        IBugAuthorizationService authService,
        IBugValidationService validationService)
    {
        _storageService = storageService;
        _authService = authService;
        _validationService = validationService;
    }

    public async Task<string> CreateUserAsync(CreateUserRequest request, string creatorId)
    {
        if (!await _authService.CanCreateUserAsync(creatorId, request.Role))
        {
            throw new UnauthorizedAccessException("User does not have permission to create users with this role");
        }

        var validationErrors = await _validationService.ValidateCreateUserRequestAsync(request);
        if (validationErrors.Any())
        {
            throw new ArgumentException($"Validation failed: {string.Join(", ", validationErrors.Select(e => e.Message))}");
        }

        // Check if username already exists
        var existingUser = await _storageService.GetUserByUsernameAsync(request.Username);
        if (existingUser != null)
        {
            throw new ArgumentException("Username already exists");
        }

        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Username = request.Username,
            Email = request.Email,
            FullName = request.FullName,
            Role = request.Role,
            LeadId = request.LeadId,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        return await _storageService.SaveUserAsync(user);
    }

    public async Task<UserResponse?> GetUserAsync(string userId)
    {
        var user = await _storageService.GetUserAsync(userId);
        if (user == null) return null;

        return await MapUserToResponseAsync(user);
    }

    public async Task<List<UserResponse>> GetUsersAsync(UserRole? role = null, string? leadId = null)
    {
        var users = await _storageService.GetUsersAsync(role, leadId);
        var responses = new List<UserResponse>();

        foreach (var user in users)
        {
            responses.Add(await MapUserToResponseAsync(user));
        }

        return responses;
    }

    public async Task<bool> UpdateUserAsync(string userId, UpdateUserRequest request, string updaterId)
    {
        var user = await _storageService.GetUserAsync(userId);
        if (user == null) return false;

        if (!await _authService.CanManageUserAsync(updaterId, userId))
        {
            throw new UnauthorizedAccessException("User does not have permission to update this user");
        }

        var validationErrors = await _validationService.ValidateUpdateUserRequestAsync(request);
        if (validationErrors.Any())
        {
            throw new ArgumentException($"Validation failed: {string.Join(", ", validationErrors.Select(e => e.Message))}");
        }

        // Update fields
        if (!string.IsNullOrEmpty(request.FullName)) user.FullName = request.FullName;
        if (request.Role.HasValue) user.Role = request.Role.Value;
        if (request.LeadId != null) user.LeadId = request.LeadId;
        if (request.IsActive.HasValue) user.IsActive = request.IsActive.Value;

        return await _storageService.UpdateUserAsync(user);
    }

    public async Task<bool> DeleteUserAsync(string userId, string deleterId)
    {
        if (!await _authService.CanManageUserAsync(deleterId, userId))
        {
            throw new UnauthorizedAccessException("User does not have permission to delete this user");
        }

        return await _storageService.DeleteUserAsync(userId);
    }

    public async Task<List<UserResponse>> GetUsersByLeadAsync(string leadId)
    {
        var users = await _storageService.GetUsersAsync(leadId: leadId);
        var responses = new List<UserResponse>();

        foreach (var user in users)
        {
            responses.Add(await MapUserToResponseAsync(user));
        }

        return responses;
    }

    public async Task<UserResponse?> GetUserByUsernameAsync(string username)
    {
        var user = await _storageService.GetUserByUsernameAsync(username);
        if (user == null) return null;

        return await MapUserToResponseAsync(user);
    }

    private async Task<UserResponse> MapUserToResponseAsync(User user)
    {
        string? leadName = null;
        if (!string.IsNullOrEmpty(user.LeadId))
        {
            var lead = await _storageService.GetUserAsync(user.LeadId);
            leadName = lead?.FullName;
        }

        return new UserResponse
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            FullName = user.FullName,
            Role = user.Role,
            LeadName = leadName,
            CreatedAt = user.CreatedAt,
            IsActive = user.IsActive
        };
    }
}