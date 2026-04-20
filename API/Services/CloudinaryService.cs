using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Models.Settings;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;

namespace API.Services
{
    public class CloudinaryService
    {
        private readonly Cloudinary _cloudinary;
        private readonly CloudinarySettings _settings;

        public CloudinaryService(IOptions<CloudinarySettings> options)
        {
            _settings = options.Value;
            var account = new Account(_settings.CloudName, _settings.ApiKey, _settings.ApiSecret);
            _cloudinary = new Cloudinary(account) { Api = { Secure = true } };
        }

        // ── Upload ────────────────────────────────────────────────────────────
        public async Task<CloudinaryUploadResult> UploadImageAsync(IFormFile file, string? folder = null)
        {
            try
            {
                await using var stream = file.OpenReadStream();

                var uploadParams = new CloudinaryDotNet.Actions.ImageUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    Folder = folder ?? _settings.CatalogFolder,
                    UseFilename = false,
                    UniqueFilename = true,
                    Overwrite = false,
                    // Auto-optimise: resize to max 800×800, auto quality, auto format (webp where supported)
                    Transformation = new Transformation()
                                        .Width(800).Height(800)
                                        .Crop("limit")
                                        .Quality("auto")
                                        .FetchFormat("auto")
                };

                var result = await _cloudinary.UploadAsync(uploadParams);

                if (result.Error != null)
                {
                    Console.WriteLine($"[CloudinaryService] Upload error: {result.Error.Message}");
                    return new CloudinaryUploadResult { Success = false, Error = result.Error.Message };
                }

                return new CloudinaryUploadResult
                {
                    Success = true,
                    SecureUrl = result.SecureUrl.ToString(),
                    PublicId = result.PublicId
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CloudinaryService] UploadImageAsync exception: {ex.Message}");
                return new CloudinaryUploadResult { Success = false, Error = ex.Message };
            }
        }

        // ── Delete ────────────────────────────────────────────────────────────
        public async Task<bool> DeleteImageAsync(string publicId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(publicId)) return false;

                var result = await _cloudinary.DestroyAsync(new DeletionParams(publicId)
                {
                    ResourceType = ResourceType.Image
                });

                return result.Result == "ok";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CloudinaryService] DeleteImageAsync exception: {ex.Message}");
                return false;
            }
        }

        // ── Replace ───────────────────────────────────────────────────────────
        public async Task<CloudinaryUploadResult> ReplaceImageAsync(IFormFile newFile, string? oldImageUrl, string? folder = null)
        {
            // Delete old image from Cloudinary (non-blocking — failure doesn't abort the upload)
            if (!string.IsNullOrWhiteSpace(oldImageUrl))
            {
                string? oldPublicId = ExtractPublicId(oldImageUrl);
                if (!string.IsNullOrWhiteSpace(oldPublicId))
                    await DeleteImageAsync(oldPublicId);
            }

            return await UploadImageAsync(newFile, folder ?? _settings.CatalogFolder);
        }

        // ── ExtractPublicId ───────────────────────────────────────────────────
        public string? ExtractPublicId(string? secureUrl)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(secureUrl)) return null;

                // Split on "/upload/" — everything after is version + public_id + extension
                var parts = secureUrl.Split("/upload/");
                if (parts.Length < 2) return null;

                string afterUpload = parts[1];
                // e.g. "v1234567/farmbridge/catalog/abc123.jpg"

                // Strip version prefix  (v<digits>/)
                if (afterUpload.StartsWith("v") && afterUpload.Contains("/"))
                {
                    int slash = afterUpload.IndexOf('/');
                    afterUpload = afterUpload[(slash + 1)..];
                }
                // now "farmbridge/catalog/abc123.jpg"

                // Strip file extension
                int dot = afterUpload.LastIndexOf('.');
                if (dot > 0) afterUpload = afterUpload[..dot];

                return afterUpload; // "farmbridge/catalog/abc123"
            }
            catch
            {
                return null;
            }
        }

        public class CloudinaryUploadResult
        {
            public bool Success { get; set; }
            public string SecureUrl { get; set; } = string.Empty;
            public string PublicId { get; set; } = string.Empty;
            public string? Error { get; set; }
        }
    }
}