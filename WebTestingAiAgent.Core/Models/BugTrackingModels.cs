using System.ComponentModel.DataAnnotations;

namespace WebTestingAiAgent.Core.Models;

#region Enumerations

public enum UserRole
{
    SuperAdmin,
    DeveloperLead,
    QALead,
    Tester,
    Developer,
    Admin
}

public enum Priority
{
    Regular,
    Top,
    Medium,
    Low
}

public enum DevStatus
{
    Pending,
    DevRunning,
    NeedToTest,
    TestRunning,
    Solved,
    Postpone,
    Invalid,
    Canceled
}

public enum BugType
{
    UiUxPc,
    UiUxMobile,
    UiUxPcAndMobile,
    Functional,
    FunctionalEnhancement,
    BusinessLogic,
    LoadTest
}

#endregion

#region Core Models

public class User
{
    public string Id { get; set; } = string.Empty;
    
    [Required]
    public string Username { get; set; } = string.Empty;
    
    [Required]
    public string Email { get; set; } = string.Empty;
    
    public string FullName { get; set; } = string.Empty;
    
    [Required]
    public UserRole Role { get; set; }
    
    [Required]
    public string Password { get; set; } = string.Empty; // Hashed password
    
    public string? LeadId { get; set; } // For Developers/Testers under a Lead
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public bool IsActive { get; set; } = true;
    
    public bool MustChangePassword { get; set; } = false; // For first-time login
}

public class Bug
{
    public string Id { get; set; } = string.Empty;
    
    [Required]
    public string Title { get; set; } = string.Empty; // "Bug" field in requirements
    
    public string Description { get; set; } = string.Empty;
    
    [Required]
    public string UrlMenu { get; set; } = string.Empty; // "URL / Menu" field
    
    [Required]
    public BugType BugType { get; set; }
    
    [Required]
    public Priority Priority { get; set; }
    
    [Required]
    public DevStatus Status { get; set; } = DevStatus.Pending;
    
    [Required]
    public string SubmittedById { get; set; } = string.Empty; // "Bug Submitted By"
    
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow; // "Bug Submitted Time"
    
    public DateTime? QaLastCheckTime { get; set; }
    
    public string QaRemarks { get; set; } = string.Empty;
    
    public string ProgrammerRemarks { get; set; } = string.Empty;
    
    public int ReopenCount { get; set; } = 0; // "Bug Re-Open Count"
    
    public List<BugImage> Images { get; set; } = new();
    
    public List<BugAssignment> Assignments { get; set; } = new();
    
    public List<BugStatusHistory> StatusHistory { get; set; } = new();
}

public class BugImage
{
    public string Id { get; set; } = string.Empty;
    
    public string BugId { get; set; } = string.Empty;
    
    [Required]
    public string FileName { get; set; } = string.Empty;
    
    [Required]
    public string FilePath { get; set; } = string.Empty; // Could be URL or file path
    
    public string Label { get; set; } = string.Empty; // "Image 1", "Image 2", etc.
    
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    
    public long FileSize { get; set; }
    
    public string ContentType { get; set; } = string.Empty;
}

public class BugAssignment
{
    public string Id { get; set; } = string.Empty;
    
    public string BugId { get; set; } = string.Empty;
    
    public string AssigneeId { get; set; } = string.Empty;
    
    public bool IsPrimary { get; set; } // true for "Bug Assign-01", false for "Bug Assign-02"
    
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    
    public string AssignedById { get; set; } = string.Empty;
}

public class BugStatusHistory
{
    public string Id { get; set; } = string.Empty;
    
    public string BugId { get; set; } = string.Empty;
    
    public DevStatus OldStatus { get; set; }
    
    public DevStatus NewStatus { get; set; }
    
    public string ChangedById { get; set; } = string.Empty;
    
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    
    public string Comments { get; set; } = string.Empty;
}

#endregion

#region API Models

public class CreateBugRequest
{
    [Required]
    public string Title { get; set; } = string.Empty;
    
    public string Description { get; set; } = string.Empty;
    
    [Required]
    public string UrlMenu { get; set; } = string.Empty;
    
    [Required]
    public BugType BugType { get; set; }
    
    [Required]
    public Priority Priority { get; set; }
    
    public string PrimaryAssigneeId { get; set; } = string.Empty;
    
    public string? SecondaryAssigneeId { get; set; }
    
    public List<BugImageUpload> Images { get; set; } = new();
}

public class UpdateBugRequest
{
    public string? Title { get; set; }
    
    public string? Description { get; set; }
    
    public string? UrlMenu { get; set; }
    
    public BugType? BugType { get; set; }
    
    public Priority? Priority { get; set; }
    
    public DevStatus? Status { get; set; }
    
    public string? QaRemarks { get; set; }
    
    public string? ProgrammerRemarks { get; set; }
    
    public string? PrimaryAssigneeId { get; set; }
    
    public string? SecondaryAssigneeId { get; set; }
    
    public string? Comments { get; set; } // For status change comments
}

public class BugImageUpload
{
    [Required]
    public string FileName { get; set; } = string.Empty;
    
    [Required]
    public byte[] Content { get; set; } = Array.Empty<byte>();
    
    [Required]
    public string ContentType { get; set; } = string.Empty;
    
    public string Label { get; set; } = string.Empty;
}

public class CreateUserRequest
{
    [Required]
    public string Username { get; set; } = string.Empty;
    
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    public string FullName { get; set; } = string.Empty;
    
    [Required]
    public UserRole Role { get; set; }
    
    [Required]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters long")]
    public string Password { get; set; } = string.Empty;
    
    public string? LeadId { get; set; }
}

public class UpdateUserRequest
{
    public string? FullName { get; set; }
    
    public UserRole? Role { get; set; }
    
    public string? LeadId { get; set; }
    
    public bool? IsActive { get; set; }
}

public class BugListRequest
{
    public string? AssigneeId { get; set; }
    
    public string? SubmittedById { get; set; }
    
    public DevStatus? Status { get; set; }
    
    public Priority? Priority { get; set; }
    
    public BugType? BugType { get; set; }
    
    public int Page { get; set; } = 1;
    
    public int PageSize { get; set; } = 20;
    
    public string? SearchTerm { get; set; }
}

public class BugResponse
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string UrlMenu { get; set; } = string.Empty;
    public BugType BugType { get; set; }
    public Priority Priority { get; set; }
    public DevStatus Status { get; set; }
    public string SubmittedById { get; set; } = string.Empty;
    public string SubmittedByName { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
    public DateTime? QaLastCheckTime { get; set; }
    public string QaRemarks { get; set; } = string.Empty;
    public string ProgrammerRemarks { get; set; } = string.Empty;
    public int ReopenCount { get; set; }
    public string? PrimaryAssigneeName { get; set; }
    public string? SecondaryAssigneeName { get; set; }
    public List<BugImageResponse> Images { get; set; } = new();
}

public class BugImageResponse
{
    public string Id { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public long FileSize { get; set; }
}

public class UserResponse
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public string? LeadName { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
}

#endregion