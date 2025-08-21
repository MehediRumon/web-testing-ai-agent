using WebTestingAiAgent.Core.Interfaces;
using WebTestingAiAgent.Core.Models;

namespace WebTestingAiAgent.Api.Services;

public class BugImageService : IBugImageService
{
    private readonly IBugStorageService _storageService;
    private readonly IBugValidationService _validationService;
    private readonly IBugAuthorizationService _authService;
    private readonly IStorageService _fileStorageService; // Reuse existing storage service
    private readonly Dictionary<string, byte[]> _imageData = new(); // In-memory image storage

    public BugImageService(
        IBugStorageService storageService,
        IBugValidationService validationService,
        IBugAuthorizationService authService,
        IStorageService fileStorageService)
    {
        _storageService = storageService;
        _validationService = validationService;
        _authService = authService;
        _fileStorageService = fileStorageService;
    }

    public async Task<string> UploadImageAsync(string bugId, BugImageUpload imageUpload, string uploaderId)
    {
        // Verify bug exists
        var bug = await _storageService.GetBugAsync(bugId);
        if (bug == null)
        {
            throw new ArgumentException("Bug not found");
        }

        // Check permissions
        if (!await _authService.CanEditBugAsync(uploaderId, bugId))
        {
            throw new UnauthorizedAccessException("User does not have permission to upload images for this bug");
        }

        // Validate image
        var validationErrors = await _validationService.ValidateImageUploadAsync(imageUpload);
        if (validationErrors.Any())
        {
            throw new ArgumentException($"Validation failed: {string.Join(", ", validationErrors.Select(e => e.Message))}");
        }

        var imageId = Guid.NewGuid().ToString();
        var fileName = SanitizeFileName(imageUpload.FileName);
        
        // Generate label if not provided
        var label = string.IsNullOrEmpty(imageUpload.Label) 
            ? await GenerateImageLabelAsync(bugId) 
            : imageUpload.Label;

        // Save image data
        _imageData[imageId] = imageUpload.Content;

        // Also save to file storage for persistence
        var filePath = $"bugs/{bugId}/images/{imageId}_{fileName}";
        await _fileStorageService.SaveArtifactAsync(bugId, filePath, imageUpload.Content);

        // Save image metadata
        var bugImage = new BugImage
        {
            Id = imageId,
            BugId = bugId,
            FileName = fileName,
            FilePath = filePath,
            Label = label,
            UploadedAt = DateTime.UtcNow,
            FileSize = imageUpload.Content.Length,
            ContentType = imageUpload.ContentType
        };

        await _storageService.SaveBugImageAsync(bugImage);

        return imageId;
    }

    public async Task<byte[]?> GetImageAsync(string imageId)
    {
        // Try in-memory cache first
        if (_imageData.TryGetValue(imageId, out var imageData))
        {
            return imageData;
        }

        // Fallback to storage service
        var bugImage = await _storageService.GetBugImageAsync(imageId);
        if (bugImage == null) return null;

        try
        {
            var data = await _fileStorageService.GetArtifactAsync(bugImage.BugId, bugImage.FilePath);
            // Cache in memory for faster access
            _imageData[imageId] = data;
            return data;
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<BugImageResponse>> GetBugImagesAsync(string bugId)
    {
        var images = await _storageService.GetBugImagesAsync(bugId);
        
        return images.Select(image => new BugImageResponse
        {
            Id = image.Id,
            FileName = image.FileName,
            Label = image.Label,
            DownloadUrl = $"/api/bugs/images/{image.Id}/download",
            UploadedAt = image.UploadedAt,
            FileSize = image.FileSize
        }).ToList();
    }

    public async Task<bool> DeleteImageAsync(string imageId, string deleterId)
    {
        var bugImage = await _storageService.GetBugImageAsync(imageId);
        if (bugImage == null) return false;

        // Check permissions
        if (!await _authService.CanEditBugAsync(deleterId, bugImage.BugId))
        {
            throw new UnauthorizedAccessException("User does not have permission to delete images for this bug");
        }

        // Remove from in-memory cache
        _imageData.Remove(imageId);

        // Remove from file storage
        try
        {
            await _fileStorageService.GetArtifactAsync(bugImage.BugId, bugImage.FilePath); // Check if exists
            // Note: IStorageService doesn't have a delete method, so we'll just remove from cache
        }
        catch
        {
            // File doesn't exist, continue with metadata removal
        }

        // Remove metadata
        return await _storageService.DeleteBugImageAsync(imageId);
    }

    public async Task<string> GetImageDownloadUrlAsync(string imageId)
    {
        await Task.CompletedTask;
        return $"/api/bugs/images/{imageId}/download";
    }

    private async Task<string> GenerateImageLabelAsync(string bugId)
    {
        var existingImages = await _storageService.GetBugImagesAsync(bugId);
        var imageCount = existingImages.Count + 1;
        return $"Image {imageCount}";
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
        
        // Ensure we have a valid extension
        if (!Path.HasExtension(sanitized))
        {
            sanitized += ".jpg"; // Default extension
        }

        return sanitized;
    }
}