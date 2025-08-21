using Microsoft.AspNetCore.Mvc;
using WebTestingAiAgent.Core.Interfaces;
using WebTestingAiAgent.Core.Models;

namespace WebTestingAiAgent.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BugsController : ControllerBase
{
    private readonly IBugService _bugService;
    private readonly IBugImageService _imageService;

    public BugsController(IBugService bugService, IBugImageService imageService)
    {
        _bugService = bugService;
        _imageService = imageService;
    }

    /// <summary>
    /// Create a new bug
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<CreateRunResponse>> CreateBug([FromBody] CreateBugRequest request)
    {
        try
        {
            // TODO: Get actual user ID from authentication context
            var submitterId = GetCurrentUserId();
            
            var bugId = await _bugService.CreateBugAsync(request, submitterId);
            
            return Ok(new { BugId = bugId });
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
            return StatusCode(500, new ApiErrorResponse { Message = "An error occurred while creating the bug", Errors = new List<ValidationError> { new() { Field = "General", Message = ex.Message } } });
        }
    }

    /// <summary>
    /// Get a bug by ID
    /// </summary>
    [HttpGet("{bugId}")]
    public async Task<ActionResult<BugResponse>> GetBug(string bugId)
    {
        try
        {
            var bug = await _bugService.GetBugAsync(bugId);
            if (bug == null)
            {
                return NotFound(new ApiErrorResponse { Message = "Bug not found" });
            }

            return Ok(bug);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiErrorResponse { Message = "An error occurred while retrieving the bug", Errors = new List<ValidationError> { new() { Field = "General", Message = ex.Message } } });
        }
    }

    /// <summary>
    /// Get bugs with filtering and pagination
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<BugResponse>>> GetBugs(
        [FromQuery] string? assigneeId = null,
        [FromQuery] string? submittedById = null,
        [FromQuery] DevStatus? status = null,
        [FromQuery] Priority? priority = null,
        [FromQuery] BugType? bugType = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? searchTerm = null)
    {
        try
        {
            var request = new BugListRequest
            {
                AssigneeId = assigneeId,
                SubmittedById = submittedById,
                Status = status,
                Priority = priority,
                BugType = bugType,
                Page = page,
                PageSize = Math.Min(pageSize, 100), // Limit page size
                SearchTerm = searchTerm
            };

            var currentUserId = GetCurrentUserId();
            var bugs = await _bugService.GetBugsAsync(request, currentUserId);
            
            return Ok(bugs);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiErrorResponse { Message = "An error occurred while retrieving bugs", Errors = new List<ValidationError> { new() { Field = "General", Message = ex.Message } } });
        }
    }

    /// <summary>
    /// Update a bug
    /// </summary>
    [HttpPut("{bugId}")]
    public async Task<ActionResult> UpdateBug(string bugId, [FromBody] UpdateBugRequest request)
    {
        try
        {
            var updaterId = GetCurrentUserId();
            var success = await _bugService.UpdateBugAsync(bugId, request, updaterId);
            
            if (!success)
            {
                return NotFound(new ApiErrorResponse { Message = "Bug not found" });
            }

            return Ok(new { Message = "Bug updated successfully" });
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
            return StatusCode(500, new ApiErrorResponse { Message = "An error occurred while updating the bug", Errors = new List<ValidationError> { new() { Field = "General", Message = ex.Message } } });
        }
    }

    /// <summary>
    /// Assign a bug to users
    /// </summary>
    [HttpPost("{bugId}/assign")]
    public async Task<ActionResult> AssignBug(string bugId, [FromBody] AssignBugRequest request)
    {
        try
        {
            var assignerId = GetCurrentUserId();
            var success = await _bugService.AssignBugAsync(bugId, request.PrimaryAssigneeId, request.SecondaryAssigneeId, assignerId);
            
            if (!success)
            {
                return NotFound(new ApiErrorResponse { Message = "Bug not found" });
            }

            return Ok(new { Message = "Bug assigned successfully" });
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
            return StatusCode(500, new ApiErrorResponse { Message = "An error occurred while assigning the bug", Errors = new List<ValidationError> { new() { Field = "General", Message = ex.Message } } });
        }
    }

    /// <summary>
    /// Update bug status
    /// </summary>
    [HttpPost("{bugId}/status")]
    public async Task<ActionResult> UpdateBugStatus(string bugId, [FromBody] UpdateBugStatusRequest request)
    {
        try
        {
            var updaterId = GetCurrentUserId();
            var success = await _bugService.UpdateBugStatusAsync(bugId, request.Status, updaterId, request.Comments);
            
            if (!success)
            {
                return NotFound(new ApiErrorResponse { Message = "Bug not found" });
            }

            return Ok(new { Message = "Bug status updated successfully" });
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
            return StatusCode(500, new ApiErrorResponse { Message = "An error occurred while updating bug status", Errors = new List<ValidationError> { new() { Field = "General", Message = ex.Message } } });
        }
    }

    /// <summary>
    /// Get bug status history
    /// </summary>
    [HttpGet("{bugId}/history")]
    public async Task<ActionResult<List<BugStatusHistory>>> GetBugStatusHistory(string bugId)
    {
        try
        {
            var history = await _bugService.GetBugStatusHistoryAsync(bugId);
            return Ok(history);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiErrorResponse { Message = "An error occurred while retrieving bug history", Errors = new List<ValidationError> { new() { Field = "General", Message = ex.Message } } });
        }
    }

    /// <summary>
    /// Upload images for a bug
    /// </summary>
    [HttpPost("{bugId}/images")]
    public async Task<ActionResult> UploadBugImages(string bugId, [FromForm] List<IFormFile> images, [FromForm] List<string>? labels = null)
    {
        try
        {
            var uploaderId = GetCurrentUserId();
            var uploadedImageIds = new List<string>();

            for (int i = 0; i < images.Count; i++)
            {
                var image = images[i];
                
                using var memoryStream = new MemoryStream();
                await image.CopyToAsync(memoryStream);
                
                var imageUpload = new BugImageUpload
                {
                    FileName = image.FileName,
                    Content = memoryStream.ToArray(),
                    ContentType = image.ContentType,
                    Label = labels != null && i < labels.Count ? labels[i] : ""
                };

                var imageId = await _imageService.UploadImageAsync(bugId, imageUpload, uploaderId);
                uploadedImageIds.Add(imageId);
            }

            return Ok(new { Message = "Images uploaded successfully", ImageIds = uploadedImageIds });
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
            return StatusCode(500, new ApiErrorResponse { Message = "An error occurred while uploading images", Errors = new List<ValidationError> { new() { Field = "General", Message = ex.Message } } });
        }
    }

    /// <summary>
    /// Get images for a bug
    /// </summary>
    [HttpGet("{bugId}/images")]
    public async Task<ActionResult<List<BugImageResponse>>> GetBugImages(string bugId)
    {
        try
        {
            var images = await _imageService.GetBugImagesAsync(bugId);
            return Ok(images);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiErrorResponse { Message = "An error occurred while retrieving bug images", Errors = new List<ValidationError> { new() { Field = "General", Message = ex.Message } } });
        }
    }

    /// <summary>
    /// Download a bug image
    /// </summary>
    [HttpGet("images/{imageId}/download")]
    public async Task<ActionResult> DownloadBugImage(string imageId)
    {
        try
        {
            var imageData = await _imageService.GetImageAsync(imageId);
            if (imageData == null)
            {
                return NotFound(new ApiErrorResponse { Message = "Image not found" });
            }

            // TODO: Get proper content type and filename from metadata
            return File(imageData, "application/octet-stream");
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiErrorResponse { Message = "An error occurred while downloading the image", Errors = new List<ValidationError> { new() { Field = "General", Message = ex.Message } } });
        }
    }

    /// <summary>
    /// Delete a bug image
    /// </summary>
    [HttpDelete("images/{imageId}")]
    public async Task<ActionResult> DeleteBugImage(string imageId)
    {
        try
        {
            var deleterId = GetCurrentUserId();
            var success = await _imageService.DeleteImageAsync(imageId, deleterId);
            
            if (!success)
            {
                return NotFound(new ApiErrorResponse { Message = "Image not found" });
            }

            return Ok(new { Message = "Image deleted successfully" });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiErrorResponse { Message = "An error occurred while deleting the image", Errors = new List<ValidationError> { new() { Field = "General", Message = ex.Message } } });
        }
    }

    /// <summary>
    /// Delete a bug (Admin/SuperAdmin only)
    /// </summary>
    [HttpDelete("{bugId}")]
    public async Task<ActionResult> DeleteBug(string bugId)
    {
        try
        {
            var deleterId = GetCurrentUserId();
            var success = await _bugService.DeleteBugAsync(bugId, deleterId);
            
            if (!success)
            {
                return NotFound(new ApiErrorResponse { Message = "Bug not found" });
            }

            return Ok(new { Message = "Bug deleted successfully" });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiErrorResponse { Message = "An error occurred while deleting the bug", Errors = new List<ValidationError> { new() { Field = "General", Message = ex.Message } } });
        }
    }

    private string GetCurrentUserId()
    {
        // TODO: Implement proper authentication and get user ID from JWT token or session
        // For now, return a default user ID for testing
        return "tester-1"; // Default to the test tester user
    }
}

// Additional DTOs for the bug controller
public class AssignBugRequest
{
    public string PrimaryAssigneeId { get; set; } = string.Empty;
    public string? SecondaryAssigneeId { get; set; }
}

public class UpdateBugStatusRequest
{
    public DevStatus Status { get; set; }
    public string? Comments { get; set; }
}