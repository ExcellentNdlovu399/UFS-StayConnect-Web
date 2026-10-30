using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UFSStayConnect.DataAccess;
using UFSStayConnect.Models;
using UFSStayConnect.ViewModels;
using UFSStayConnect.Services;

namespace UFSStayConnect.Controllers
{
    public class AccommodationController : Controller
    {
        private readonly IRepositoryWrapper _repository;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<AccommodationController> _logger;

        private static readonly string[] AllowedImageExtensions =
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".webp"
        };

        private const long MaxImageSize = 5 * 1024 * 1024;

        public AccommodationController(
            IRepositoryWrapper repository,
            IWebHostEnvironment environment,
            ILogger<AccommodationController> logger)
        {
            _repository = repository;
            _environment = environment;
            _logger = logger;
        }

        // =========================================================
        // PUBLIC ACCOMMODATION INDEX
        // =========================================================

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Index(
            string? location,
            string? type,
            int? serviceId)
        {
            try
            {
                var accommodations =
                    _repository.Accommodation
                        .FindAvailableAccommodations()
                        .Where(a => a.AvailableRooms > 0);

                // Filter by location
                if (!string.IsNullOrWhiteSpace(location))
                {
                    accommodations = accommodations.Where(a =>
                        !string.IsNullOrWhiteSpace(a.Location) &&
                        a.Location.Contains(
                            location,
                            StringComparison.OrdinalIgnoreCase));
                }

                // Filter by type
                if (!string.IsNullOrWhiteSpace(type))
                {
                    accommodations = accommodations.Where(a =>
                        !string.IsNullOrWhiteSpace(a.Type) &&
                        a.Type.Equals(
                            type,
                            StringComparison.OrdinalIgnoreCase));
                }

                // Filter by service
                if (serviceId.HasValue)
                {
                    accommodations = accommodations.Where(a =>
                        a.AccommodationServices != null &&
                        a.AccommodationServices.Any(
                            s => s.ServiceID == serviceId.Value));
                }

                ViewBag.Services =
                    _repository.Service.FindAllServices();

                return View(accommodations.ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error loading available accommodation.");

                TempData["ErrorMessage"] =
                    "Unable to load accommodation.";

                return View(new List<Accommodation>());
            }
        }

        // =========================================================
        // ADMIN ACCOMMODATION MANAGEMENT
        // =========================================================

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public IActionResult Manage()
        {
            try
            {
                var accommodations =
                    _repository.Accommodation
                        .FindAllAccommodations();

                return View(accommodations.ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error loading accommodation management records.");

                TempData["ErrorMessage"] =
                    "Unable to load accommodation records.";

                return View(new List<Accommodation>());
            }
        }

        // =========================================================
        // DETAILS
        // =========================================================

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Details(int id)
        {
            if (id <= 0)
            {
                return BadRequest();
            }

            try
            {
                var accommodation =
                    _repository.Accommodation
                        .GetAccommodationById(id);

                if (accommodation == null)
                {
                    return NotFound();
                }

                return View(accommodation);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error loading accommodation {AccommodationID}.",
                    id);

                return StatusCode(
                    500,
                    "An error occurred while loading the accommodation.");
            }
        }

        // =========================================================
        // CREATE - GET
        // =========================================================

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public IActionResult Create()
        {
            return View(new AccommodationViewModel
            {
                Available = true
            });
        }

        // =========================================================
        // CREATE - POST
        // =========================================================

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            AccommodationViewModel model)
        {
            // Validate accommodation
            ValidateAccommodation(model);

            // Validate image
            if (model.ImageFile != null)
            {
                ValidateImage(model.ImageFile);
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                string? imagePath = null;

                // Save image if one was selected
                if (model.ImageFile != null)
                {
                    imagePath =
                        await SaveImageAsync(model.ImageFile);
                }

                // IMPORTANT:
                // When an accommodation is created,
                // all rooms are initially available.
                int totalRooms = model.TotalRooms;
                int availableRooms = totalRooms;

                var accommodation = new Accommodation
                {
                    Name = model.Name.Trim(),

                    Location = model.Location.Trim(),

                    Type = model.Type,

                    Description = model.Description.Trim(),

                    MonthlyRent = model.MonthlyRent,

                    Bedrooms = model.Bedrooms,

                    Bathrooms = model.Bathrooms,

                    TotalRooms = totalRooms,

                    AvailableRooms = availableRooms,

                    ImagePath = imagePath,

                    Available =
                        totalRooms > 0 && model.Available,

                    DateAdded = DateTime.UtcNow
                };

                _repository.Accommodation
                    .CreateAccommodation(accommodation);

                _repository.Save();

                var rentType = _repository.PaymentType.FindAllPaymentTypes()
                    .FirstOrDefault(p => p.Name.Equals("Rent", StringComparison.OrdinalIgnoreCase));
                if (rentType != null)
                {
                    _repository.AccommodationCharge.CreateCharge(new AccommodationCharge
                    {
                        AccommodationID = accommodation.AccommodationID,
                        PaymentTypeID = rentType.PaymentTypeID,
                        Amount = accommodation.MonthlyRent,
                        Frequency = "Monthly",
                        DueDate = UFSStayConnect.Services.PaymentDueDateCalculator.DefaultDueDate(),
                        ReminderDaysBeforeDue = 3,
                        ReminderDaysAfterDue = 3,
                        Description = "Monthly accommodation rent.",
                        IsActive = true
                    });
                    _repository.Save();
                }

                for (var i = 1; i <= accommodation.TotalRooms; i++)
                {
                    _repository.Room.CreateRoom(new Room
                    {
                        AccommodationID = accommodation.AccommodationID,
                        RoomNumber = i.ToString("D2"),
                        RoomType = "Standard",
                        Status = "Available"
                    });
                }
                _repository.Save();

                TempData["SuccessMessage"] =
                    "Accommodation was added successfully.";

                // Always return Admin to Accommodation Management
                return RedirectToAction(nameof(Manage));
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error creating accommodation.");

                ModelState.AddModelError(
                    "",
                    "The accommodation could not be added. Please try again.");

                return View(model);
            }
        }

        // =========================================================
        // EDIT - GET
        // =========================================================

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public IActionResult Edit(int id)
        {
            if (id <= 0)
            {
                return BadRequest();
            }

            try
            {
                var accommodation =
                    _repository.Accommodation
                        .GetAccommodationById(id);

                if (accommodation == null)
                {
                    return NotFound();
                }

                var model = new AccommodationViewModel
                {
                    AccommodationID =
                        accommodation.AccommodationID,

                    Name =
                        accommodation.Name,

                    Location =
                        accommodation.Location,

                    Type =
                        accommodation.Type,

                    Description =
                        accommodation.Description,

                    MonthlyRent =
                        accommodation.MonthlyRent,

                    Bedrooms =
                        accommodation.Bedrooms,

                    Bathrooms =
                        accommodation.Bathrooms,

                    TotalRooms =
                        accommodation.TotalRooms,

                    AvailableRooms =
                        accommodation.AvailableRooms,

                    ExistingImagePath =
                        accommodation.ImagePath,

                    Available =
                        accommodation.Available
                };

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error loading accommodation {AccommodationID} for editing.",
                    id);

                return StatusCode(
                    500,
                    "An error occurred while loading the accommodation.");
            }
        }

        // =========================================================
        // EDIT - POST
        // =========================================================

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            AccommodationViewModel model)
        {
            // Validate basic accommodation information
            ValidateAccommodation(model);

            // Validate image
            if (model.ImageFile != null)
            {
                ValidateImage(model.ImageFile);
            }

            if (model.AccommodationID <= 0)
            {
                ModelState.AddModelError(
                    "AccommodationID",
                    "Invalid accommodation.");

                return View(model);
            }

            // Get the existing accommodation before validation
            Accommodation? existingAccommodation =
                _repository.Accommodation
                    .GetAccommodationById(model.AccommodationID);

            if (existingAccommodation == null)
            {
                return NotFound();
            }

            // -----------------------------------------------------
            // Calculate currently occupied rooms
            //
            // Example:
            // TotalRooms = 20
            // AvailableRooms = 5
            //
            // OccupiedRooms = 20 - 5 = 15
            // -----------------------------------------------------

            int occupiedRooms =
                existingAccommodation.TotalRooms -
                existingAccommodation.AvailableRooms;

            // -----------------------------------------------------
            // The new total cannot be lower than occupied rooms.
            // -----------------------------------------------------

            if (model.TotalRooms < occupiedRooms)
            {
                ModelState.AddModelError(
                    "TotalRooms",
                    $"Total rooms cannot be less than the {occupiedRooms} currently occupied room(s).");
            }

            if (!ModelState.IsValid)
            {
                // Preserve the existing image when returning
                // to the Edit page after validation failure.
                model.ExistingImagePath =
                    existingAccommodation.ImagePath;

                model.AvailableRooms =
                    existingAccommodation.AvailableRooms;

                return View(model);
            }

            try
            {
                // -------------------------------------------------
                // Calculate new available rooms
                //
                // New Available =
                // New Total - Occupied Rooms
                // -------------------------------------------------

                int newAvailableRooms =
                    model.TotalRooms - occupiedRooms;

                existingAccommodation.Name =
                    model.Name.Trim();

                existingAccommodation.Location =
                    model.Location.Trim();

                existingAccommodation.Type =
                    model.Type;

                existingAccommodation.Description =
                    model.Description.Trim();

                existingAccommodation.MonthlyRent =
                    model.MonthlyRent;

                existingAccommodation.Bedrooms =
                    model.Bedrooms;

                existingAccommodation.Bathrooms =
                    model.Bathrooms;

                existingAccommodation.TotalRooms =
                    model.TotalRooms;

                existingAccommodation.AvailableRooms =
                    newAvailableRooms;

                // -------------------------------------------------
                // Accommodation is only available if:
                //
                // 1. There is at least one room available
                // 2. Admin has not disabled it
                // -------------------------------------------------

                existingAccommodation.Available =
                    newAvailableRooms > 0 &&
                    model.Available;

                // -------------------------------------------------
                // Replace image if a new image was uploaded
                // -------------------------------------------------

                if (model.ImageFile != null)
                {
                    var oldImagePath =
                        existingAccommodation.ImagePath;

                    existingAccommodation.ImagePath =
                        await SaveImageAsync(
                            model.ImageFile);

                    DeleteImage(oldImagePath);
                }

                _repository.Accommodation
                    .UpdateAccommodation(existingAccommodation);

                var rentType = _repository.PaymentType.FindAllPaymentTypes()
                    .FirstOrDefault(p => p.Name.Equals("Rent", StringComparison.OrdinalIgnoreCase));
                if (rentType != null)
                {
                    var rentCharge = _repository.AccommodationCharge
                        .FindByAccommodation(existingAccommodation.AccommodationID)
                        .FirstOrDefault(c => c.PaymentTypeID == rentType.PaymentTypeID);
                    if (rentCharge == null)
                    {
                        _repository.AccommodationCharge.CreateCharge(new AccommodationCharge
                        {
                            AccommodationID = existingAccommodation.AccommodationID,
                            PaymentTypeID = rentType.PaymentTypeID,
                            Amount = existingAccommodation.MonthlyRent,
                            Frequency = "Monthly",
                            Description = "Monthly accommodation rent.",
                            IsActive = true
                        });
                    }
                    else
                    {
                        rentCharge.Amount = existingAccommodation.MonthlyRent;
                        rentCharge.Frequency = "Monthly";
                        if (!rentCharge.DueDate.HasValue) rentCharge.DueDate = UFSStayConnect.Services.PaymentDueDateCalculator.DefaultDueDate();
                        rentCharge.ReminderDaysBeforeDue = 3;
                        rentCharge.ReminderDaysAfterDue = 3;
                        rentCharge.IsActive = true;
                        _repository.AccommodationCharge.UpdateCharge(rentCharge);
                    }
                }

                _repository.Save();

                TempData["SuccessMessage"] =
                    "Accommodation was updated successfully.";

                // Always return Admin to Accommodation Management
                return RedirectToAction(nameof(Manage));
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error updating accommodation {AccommodationID}.",
                    model.AccommodationID);

                ModelState.AddModelError(
                    "",
                    "The accommodation could not be updated. Please try again.");

                model.ExistingImagePath =
                    existingAccommodation.ImagePath;

                return View(model);
            }
        }

        // =========================================================
        // DELETE - GET
        // =========================================================

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public IActionResult Delete(int id)
        {
            if (id <= 0)
            {
                return BadRequest();
            }

            try
            {
                var accommodation =
                    _repository.Accommodation
                        .GetAccommodationById(id);

                if (accommodation == null)
                {
                    return NotFound();
                }

                return View(accommodation);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error loading accommodation {AccommodationID} for deletion.",
                    id);

                return StatusCode(
                    500,
                    "An error occurred while loading the accommodation.");
            }
        }

        // =========================================================
        // DELETE - POST
        // =========================================================

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            if (id <= 0)
            {
                return BadRequest();
            }

            try
            {
                var accommodation =
                    _repository.Accommodation
                        .GetAccommodationById(id);

                if (accommodation == null)
                {
                    return NotFound();
                }

                var imagePath =
                    accommodation.ImagePath;

                _repository.Accommodation
                    .DeleteAccommodation(accommodation);

                _repository.Save();

                // Delete image after successful database deletion
                DeleteImage(imagePath);

                TempData["SuccessMessage"] =
                    "Accommodation was deleted successfully.";

                // Return Admin to Accommodation Management
                return RedirectToAction(nameof(Manage));
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error deleting accommodation {AccommodationID}.",
                    id);

                TempData["ErrorMessage"] =
                    "The accommodation could not be deleted.";

                return RedirectToAction(nameof(Manage));
            }
        }

        // =========================================================
        // VALIDATION
        // =========================================================

        private void ValidateAccommodation(
            AccommodationViewModel model)
        {
            // Accommodation type
            if (model.Type != "Flat" &&
                model.Type != "Townhouse")
            {
                ModelState.AddModelError(
                    "Type",
                    "Accommodation type must be Flat or Townhouse.");
            }

            // Monthly rent
            if (model.MonthlyRent <= 0)
            {
                ModelState.AddModelError(
                    "MonthlyRent",
                    "Monthly rent must be greater than R0.");
            }

            if (model.Bedrooms < 1 || model.Bedrooms > 500)
            {
                ModelState.AddModelError(
                    "Bedrooms",
                    "Bedrooms must be between 1 and 500.");
            }

            if (model.Bathrooms < 1 || model.Bathrooms > 200)
            {
                ModelState.AddModelError(
                    "Bathrooms",
                    "Bathrooms must be between 1 and 200.");
            }

            if (model.TotalRooms < 1 || model.TotalRooms > 100)
            {
                ModelState.AddModelError(
                    "TotalRooms",
                    "Total rooms must be between 1 and 100.");
            }
        }

        // =========================================================
        // IMAGE VALIDATION
        // =========================================================

        private void ValidateImage(IFormFile image)
        {
            if (image.Length <= 0)
            {
                ModelState.AddModelError(
                    "ImageFile",
                    "The selected image is empty.");

                return;
            }

            if (image.Length > MaxImageSize)
            {
                ModelState.AddModelError(
                    "ImageFile",
                    "Image size cannot exceed 5 MB.");

                return;
            }

            var extension =
                Path.GetExtension(image.FileName)
                    .ToLowerInvariant();

            if (!AllowedImageExtensions.Contains(extension))
            {
                ModelState.AddModelError(
                    "ImageFile",
                    "Only JPG, JPEG, PNG and WEBP images are allowed.");
            }
        }

        // =========================================================
        // SAVE IMAGE
        // =========================================================

        private async Task<string> SaveImageAsync(
            IFormFile image)
        {
            var folder =
                Path.Combine(
                    _environment.WebRootPath,
                    "images",
                    "accommodation");

            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var extension =
                Path.GetExtension(image.FileName)
                    .ToLowerInvariant();

            var fileName =
                $"{Guid.NewGuid():N}{extension}";

            var filePath =
                Path.Combine(
                    folder,
                    fileName);

            await using var stream =
                new FileStream(
                    filePath,
                    FileMode.Create);

            await image.CopyToAsync(stream);

            return
                $"/images/accommodation/{fileName}";
        }

        // =========================================================
        // DELETE IMAGE
        // =========================================================

        private void DeleteImage(string? imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                return;
            }

            try
            {
                var relativePath =
                    imagePath
                        .TrimStart('/')
                        .Replace(
                            '/',
                            Path.DirectorySeparatorChar);

                var fullPath =
                    Path.Combine(
                        _environment.WebRootPath,
                        relativePath);

                if (System.IO.File.Exists(fullPath))
                {
                    System.IO.File.Delete(fullPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Could not delete accommodation image {ImagePath}.",
                    imagePath);
            }
        }
    }
}