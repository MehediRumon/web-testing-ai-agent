using WebTestingAiAgent.Core.Models;

namespace WebTestingAiAgent.Core.Interfaces;

/// <summary>
/// Bug management service interface
/// </summary>
public interface IBugService
{
    Task<string> CreateBugAsync(CreateBugRequest request, string submitterId);
    Task<BugResponse?> GetBugAsync(string bugId);
    Task<List<BugResponse>> GetBugsAsync(BugListRequest request, string? requesterId = null);
    Task<bool> UpdateBugAsync(string bugId, UpdateBugRequest request, string updaterId);
    Task<bool> DeleteBugAsync(string bugId, string deleterId);
    Task<bool> AssignBugAsync(string bugId, string primaryAssigneeId, string? secondaryAssigneeId, string assignerId);
    Task<bool> UpdateBugStatusAsync(string bugId, DevStatus newStatus, string updaterId, string? comments = null);
    Task<List<BugStatusHistory>> GetBugStatusHistoryAsync(string bugId);
}

/// <summary>
/// User management service interface
/// </summary>
public interface IUserService
{
    Task<string> CreateUserAsync(CreateUserRequest request, string creatorId);
    Task<UserResponse?> GetUserAsync(string userId);
    Task<List<UserResponse>> GetUsersAsync(UserRole? role = null, string? leadId = null);
    Task<bool> UpdateUserAsync(string userId, UpdateUserRequest request, string updaterId);
    Task<bool> DeleteUserAsync(string userId, string deleterId);
    Task<List<UserResponse>> GetUsersByLeadAsync(string leadId);
    Task<UserResponse?> GetUserByUsernameAsync(string username);
}

/// <summary>
/// Authorization service for role-based permissions
/// </summary>
public interface IBugAuthorizationService
{
    Task<bool> CanCreateBugAsync(string userId);
    Task<bool> CanEditBugAsync(string userId, string bugId);
    Task<bool> CanDeleteBugAsync(string userId, string bugId);
    Task<bool> CanAssignBugAsync(string userId, string bugId, string assigneeId);
    Task<bool> CanUpdateBugStatusAsync(string userId, string bugId, DevStatus newStatus);
    Task<bool> CanManageUserAsync(string userId, string targetUserId);
    Task<bool> CanCreateUserAsync(string userId, UserRole targetRole);
    Task<List<DevStatus>> GetAllowedStatusTransitionsAsync(string userId, string bugId, DevStatus currentStatus);
}

/// <summary>
/// Bug image management service interface  
/// </summary>
public interface IBugImageService
{
    Task<string> UploadImageAsync(string bugId, BugImageUpload imageUpload, string uploaderId);
    Task<byte[]?> GetImageAsync(string imageId);
    Task<List<BugImageResponse>> GetBugImagesAsync(string bugId);
    Task<bool> DeleteImageAsync(string imageId, string deleterId);
    Task<string> GetImageDownloadUrlAsync(string imageId);
}

/// <summary>
/// Validation service for bug tracking operations
/// </summary>
public interface IBugValidationService
{
    Task<List<ValidationError>> ValidateCreateBugRequestAsync(CreateBugRequest request);
    Task<List<ValidationError>> ValidateUpdateBugRequestAsync(UpdateBugRequest request);
    Task<List<ValidationError>> ValidateCreateUserRequestAsync(CreateUserRequest request);
    Task<List<ValidationError>> ValidateUpdateUserRequestAsync(UpdateUserRequest request);
    Task<List<ValidationError>> ValidateStatusTransitionAsync(DevStatus currentStatus, DevStatus newStatus, UserRole userRole);
    Task<List<ValidationError>> ValidateImageUploadAsync(BugImageUpload imageUpload);
}

/// <summary>
/// Bug storage service interface
/// </summary>
public interface IBugStorageService
{
    // Bug operations
    Task<string> SaveBugAsync(Bug bug);
    Task<Bug?> GetBugAsync(string bugId);
    Task<List<Bug>> GetBugsAsync(BugListRequest request, string? requesterId = null);
    Task<bool> UpdateBugAsync(Bug bug);
    Task<bool> DeleteBugAsync(string bugId);
    
    // User operations
    Task<string> SaveUserAsync(User user);
    Task<User?> GetUserAsync(string userId);
    Task<List<User>> GetUsersAsync(UserRole? role = null, string? leadId = null);
    Task<bool> UpdateUserAsync(User user);
    Task<bool> DeleteUserAsync(string userId);
    Task<User?> GetUserByUsernameAsync(string username);
    
    // Bug assignment operations
    Task<bool> SaveBugAssignmentAsync(BugAssignment assignment);
    Task<List<BugAssignment>> GetBugAssignmentsAsync(string bugId);
    Task<bool> DeleteBugAssignmentsAsync(string bugId);
    
    // Bug status history operations
    Task<bool> SaveBugStatusHistoryAsync(BugStatusHistory statusHistory);
    Task<List<BugStatusHistory>> GetBugStatusHistoryAsync(string bugId);
    
    // Bug image operations
    Task<string> SaveBugImageAsync(BugImage image);
    Task<BugImage?> GetBugImageAsync(string imageId);
    Task<List<BugImage>> GetBugImagesAsync(string bugId);
    Task<bool> DeleteBugImageAsync(string imageId);
}