using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;

namespace ljp_itsolutions.Services
{
    public interface IPhotoService
    {
        Task<ImageUploadResult> AddPhotoAsync(IFormFile file);
        Task<DeletionResult> DeletePhotoAsync(string publicId);
    }

    public class CloudinarySettings
    {
        public string CloudName { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string ApiSecret { get; set; } = string.Empty;
    }

    public class PhotoService : IPhotoService
    {
        private readonly Cloudinary _cloudinary;

        public PhotoService(IOptions<CloudinarySettings> config)
        {
            var acc = new Account(
                config.Value.CloudName,
                config.Value.ApiKey,
                config.Value.ApiSecret
            );

            _cloudinary = new Cloudinary(acc);
        }

        public async Task<ImageUploadResult> AddPhotoAsync(IFormFile file)
        {
            var uploadResult = new ImageUploadResult();

            if (file.Length > 0)
            {
                // Security Hardening: File Validation
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                
                if (!allowedExtensions.Contains(extension))
                {
                    return new ImageUploadResult { Error = new CloudinaryDotNet.Actions.Error { Message = "Unsupported file extension." } };
                }

                if (!file.ContentType.StartsWith("image/"))
                {
                    return new ImageUploadResult { Error = new CloudinaryDotNet.Actions.Error { Message = "Invalid image content type." } };
                }

                if (file.Length > 5 * 1024 * 1024) // 5MB Limit
                {
                    return new ImageUploadResult { Error = new CloudinaryDotNet.Actions.Error { Message = "File size exceeds 5MB limit." } };
                }

                using var stream = file.OpenReadStream();
                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    Transformation = new Transformation().Height(500).Width(500).Crop("fill").Gravity("auto"),
                    Folder = "ljp_erp"
                };
                uploadResult = await _cloudinary.UploadAsync(uploadParams);
            }

            return uploadResult;
        }

        public async Task<DeletionResult> DeletePhotoAsync(string publicId)
        {
            var deleteParams = new DeletionParams(publicId);
            return await _cloudinary.DestroyAsync(deleteParams);
        }
    }
}
