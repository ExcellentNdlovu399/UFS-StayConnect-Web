using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UFSStayConnect.Data;
using UFSStayConnect.Models;

namespace UFSStayConnect.Controllers.Api
{
    /// <summary>
    /// Authenticated image-upload endpoint for the Android Administrator accommodation form.
    /// Stores images in wwwroot/images/accommodation and persists the relative ImagePath.
    /// </summary>
    [ApiController]
    [Authorize(Roles = "Admin")]
    [Route("api/admin/catalog/accommodations")]
    public sealed class AdminAccommodationImageController : ControllerBase
    {
        private const long MaxImageSizeBytes = 5 * 1024 * 1024;
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp"
        };

        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<AdminAccommodationImageController> _logger;

        public AdminAccommodationImageController(
            AppDbContext db,
            IWebHostEnvironment environment,
            ILogger<AdminAccommodationImageController> logger)
        {
            _db = db;
            _environment = environment;
            _logger = logger;
        }

        // POST api/admin/catalog/accommodations/12/image
        // Multipart form field: image
        [HttpPost("{id:int}/image")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(MaxImageSizeBytes + 256 * 1024)]
        public async Task<IActionResult> UploadImage(
            [FromRoute] int id,
            [FromForm(Name = "image")] IFormFile? image,
            CancellationToken cancellationToken)
        {
            if (image == null || image.Length == 0)
                return BadRequest(new { message = "Choose an image to upload." });

            if (image.Length > MaxImageSizeBytes)
                return BadRequest(new { message = "Images must be 5 MB or smaller." });

            var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(extension))
                return BadRequest(new { message = "Only JPG, JPEG, PNG and WEBP images are allowed." });

            if (!await HasValidImageSignatureAsync(image, extension, cancellationToken))
                return BadRequest(new { message = "The selected file is not a valid JPG, PNG or WEBP image." });

            var accommodation = await _db.Accommodations
                .FirstOrDefaultAsync(a => a.AccommodationID == id, cancellationToken);
            if (accommodation == null)
                return NotFound(new { message = "Accommodation not found." });

            var webRoot = _environment.WebRootPath;
            if (string.IsNullOrWhiteSpace(webRoot))
                webRoot = Path.Combine(_environment.ContentRootPath, "wwwroot");

            var imageFolder = Path.Combine(webRoot, "images", "accommodation");
            Directory.CreateDirectory(imageFolder);

            var fileName = $"{Guid.NewGuid():N}{extension}";
            var physicalPath = Path.Combine(imageFolder, fileName);
            var newImagePath = $"/images/accommodation/{fileName}";
            var oldImagePath = accommodation.ImagePath;

            try
            {
                await using (var output = new FileStream(
                    physicalPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    useAsync: true))
                {
                    await image.CopyToAsync(output, cancellationToken);
                }

                accommodation.ImagePath = newImagePath;
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                try
                {
                    if (System.IO.File.Exists(physicalPath)) System.IO.File.Delete(physicalPath);
                }
                catch (Exception cleanupError)
                {
                    _logger.LogWarning(cleanupError, "Could not remove incomplete accommodation image {ImagePath}.", physicalPath);
                }

                _logger.LogError(ex, "Failed to save image for accommodation {AccommodationId}.", id);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "The accommodation photo could not be saved." });
            }

            DeletePreviousImage(oldImagePath, webRoot);

            return Ok(new
            {
                message = "Accommodation photo uploaded successfully.",
                accommodationId = accommodation.AccommodationID,
                imagePath = accommodation.ImagePath
            });
        }

        private static async Task<bool> HasValidImageSignatureAsync(
            IFormFile image,
            string extension,
            CancellationToken cancellationToken)
        {
            var header = new byte[12];
            var total = 0;
            await using (var stream = image.OpenReadStream())
            {
                while (total < header.Length)
                {
                    var read = await stream.ReadAsync(header.AsMemory(total, header.Length - total), cancellationToken);
                    if (read <= 0) break;
                    total += read;
                }
            }

            if (extension is ".jpg" or ".jpeg")
                return total >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;

            if (extension == ".png")
                return total >= 8 && header[0] == 0x89 && header[1] == 0x50
                    && header[2] == 0x4E && header[3] == 0x47
                    && header[4] == 0x0D && header[5] == 0x0A
                    && header[6] == 0x1A && header[7] == 0x0A;

            if (extension == ".webp")
                return total >= 12 && header[0] == (byte)'R' && header[1] == (byte)'I'
                    && header[2] == (byte)'F' && header[3] == (byte)'F'
                    && header[8] == (byte)'W' && header[9] == (byte)'E'
                    && header[10] == (byte)'B' && header[11] == (byte)'P';

            return false;
        }

        private void DeletePreviousImage(string? imagePath, string webRoot)
        {
            if (string.IsNullOrWhiteSpace(imagePath)) return;

            // Delete only files previously stored inside this feature's own image folder.
            var normalized = imagePath.Replace('\\', '/');
            const string prefix = "/images/accommodation/";
            if (!normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return;

            var fileName = Path.GetFileName(normalized);
            if (string.IsNullOrWhiteSpace(fileName)) return;

            var oldPhysicalPath = Path.Combine(webRoot, "images", "accommodation", fileName);
            try
            {
                if (System.IO.File.Exists(oldPhysicalPath)) System.IO.File.Delete(oldPhysicalPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not delete replaced accommodation image {ImagePath}.", imagePath);
            }
        }
    }
}
