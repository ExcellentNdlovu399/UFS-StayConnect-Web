using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using UFSStayConnect.DataAccess;
using UFSStayConnect.Models;
using UFSStayConnect.ViewModels;
using UFSStayConnect.Services;

namespace UFSStayConnect.Controllers
{
    [Authorize]
    public class ApplicationController : Controller
    {
        private readonly IRepositoryWrapper _repository;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<ApplicationController> _logger;

        private const long MaxFileSize = 5 * 1024 * 1024;

        public ApplicationController(
            IRepositoryWrapper repository,
            UserManager<ApplicationUser> userManager,
            ILogger<ApplicationController> logger)
        {
            _repository = repository;
            _userManager = userManager;
            _logger = logger;
        }

        // =========================================================
        // INDEX
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> Index(
            string? status,
            string? search)
        {
            try
            {
                if (User.IsInRole("Admin") ||
                    User.IsInRole("FinancialOfficer"))
                {
                    var applications = _repository.Application
                        .FindAllApplications()
                        .AsEnumerable();

                    if (!string.IsNullOrWhiteSpace(status))
                    {
                        applications = applications.Where(a =>
                            string.Equals(
                                a.Status,
                                status,
                                StringComparison.OrdinalIgnoreCase));
                    }

                    if (!string.IsNullOrWhiteSpace(search))
                    {
                        search = search.Trim();

                        applications = applications.Where(a =>
                            a.ApplicationID.ToString().Contains(search) ||
                            (a.User != null &&
                             ($"{a.User.FirstName} {a.User.Surname}")
                                .Contains(
                                    search,
                                    StringComparison.OrdinalIgnoreCase)) ||
                            (a.User != null &&
                             (a.User.Email ?? string.Empty).Contains(
                                 search,
                                 StringComparison.OrdinalIgnoreCase)) ||
                            (a.Accommodation?.Name ?? string.Empty)
                                .Contains(
                                    search,
                                    StringComparison.OrdinalIgnoreCase));
                    }

                    ViewBag.Status = status;
                    ViewBag.Search = search;

                    return View(applications.ToList());
                }

                if (!User.IsInRole("Customer"))
                {
                    return Forbid();
                }

                var user = await _userManager.GetUserAsync(User);

                if (user == null)
                {
                    return RedirectToAction("Login", "Account");
                }

                var customerApplications = _repository.Application
                    .FindByUser(user.Id)
                    .ToList();

                return View(customerApplications);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading applications.");

                TempData["ErrorMessage"] =
                    "Unable to load applications.";

                return View(new List<Application>());
            }
        }

        // =========================================================
        // APPLY - GET
        // =========================================================

        [Authorize(Roles = "Customer")]
        [HttpGet]
        public async Task<IActionResult> Apply(int accommodationId)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);

                if (user == null)
                {
                    return RedirectToAction("Login", "Account");
                }

                var accommodation = _repository.Accommodation
                    .GetAccommodationById(accommodationId);

                if (accommodation == null)
                {
                    return NotFound();
                }

                if (!accommodation.Available ||
                    accommodation.AvailableRooms <= 0)
                {
                    TempData["ErrorMessage"] =
                        "This accommodation is no longer available.";

                    return RedirectToAction(
                        "Details",
                        "Accommodation",
                        new { id = accommodationId });
                }

                var existingApplications = _repository.Application
                    .FindByUser(user.Id)
                    .ToList();

                // Prevent duplicate active applications for the SAME
                // accommodation only. Applications for other
                // accommodations are always allowed.
                var existingApplication = existingApplications
                    .FirstOrDefault(a =>
                        a.AccommodationID == accommodationId &&
                        (string.Equals(
                             a.Status,
                             "Pending",
                             StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(
                             a.Status,
                             "Approved",
                             StringComparison.OrdinalIgnoreCase)));

                if (existingApplication != null)
                {
                    TempData["ErrorMessage"] =
                        "You already have an active application for this accommodation.";

                    return RedirectToAction(
                        nameof(Details),
                        new { id = existingApplication.ApplicationID });
                }

                var model = new ApplicationViewModel
                {
                    AccommodationID = accommodation.AccommodationID,

                    FullName = $"{user.FirstName} {user.Surname}".Trim(),

                    StudentOrStaffNumber =
                        string.Equals(
                            user.UserType,
                            "Student",
                            StringComparison.OrdinalIgnoreCase)
                            ? user.StudentNumber ?? string.Empty
                            : user.EmployeeNumber ?? string.Empty,

                    Email = user.Email ?? string.Empty,
                    PhoneNumber = user.PhoneNumber ?? string.Empty,
                    UserType = user.UserType
                };

                ViewBag.Accommodation = accommodation;

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error opening application for accommodation {AccommodationID}.",
                    accommodationId);

                TempData["ErrorMessage"] =
                    "Unable to open the accommodation application.";

                return RedirectToAction("Index", "Accommodation");
            }
        }

        // =========================================================
        // APPLY - POST
        // =========================================================

        [Authorize(Roles = "Customer")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Apply(ApplicationViewModel model)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);

                if (user == null)
                {
                    return Challenge();
                }

                // Populate account details from the authenticated user.
                model.FullName =
                    $"{user.FirstName} {user.Surname}".Trim();

                model.StudentOrStaffNumber =
                    string.Equals(
                        user.UserType,
                        "Student",
                        StringComparison.OrdinalIgnoreCase)
                        ? user.StudentNumber ?? string.Empty
                        : user.EmployeeNumber ?? string.Empty;

                model.Email = user.Email ?? string.Empty;
                model.PhoneNumber = user.PhoneNumber ?? string.Empty;
                model.UserType = user.UserType ?? string.Empty;

                ModelState.Remove(nameof(model.FullName));
                ModelState.Remove(nameof(model.StudentOrStaffNumber));
                ModelState.Remove(nameof(model.Email));
                ModelState.Remove(nameof(model.PhoneNumber));
                ModelState.Remove(nameof(model.UserType));

                var accommodation = _repository.Accommodation
                    .GetAccommodationById(model.AccommodationID);

                if (accommodation == null)
                {
                    return NotFound();
                }

                ViewBag.Accommodation = accommodation;

                var applications = _repository.Application
                    .FindByUser(user.Id)
                    .ToList();

                if (!accommodation.Available ||
                    accommodation.AvailableRooms <= 0)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        "This accommodation is no longer available.");

                    return View(model);
                }

                // Allow applications for any number of different
                // accommodations, but prevent duplicates for the same one.
                if (applications.Any(a =>
                        a.AccommodationID == model.AccommodationID &&
                        (string.Equals(
                             a.Status,
                             "Pending",
                             StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(
                             a.Status,
                             "Approved",
                             StringComparison.OrdinalIgnoreCase))))
                {
                    ModelState.AddModelError(
                        string.Empty,
                        "You already have an active application for this accommodation.");

                    return View(model);
                }

                if (!model.DeclarationAccepted)
                {
                    ModelState.AddModelError(
                        nameof(model.DeclarationAccepted),
                        "You must accept the declaration before submitting.");
                }

                ValidatePdf(model.IDDocument, "Certified Copy of ID");

                if (string.Equals(
                    user.UserType,
                    "Student",
                    StringComparison.OrdinalIgnoreCase))
                {
                    ValidatePdf(
                        model.RegistrationDocument,
                        "Proof of Registration");
                }
                else if (string.Equals(
                    user.UserType,
                    "Staff",
                    StringComparison.OrdinalIgnoreCase))
                {
                    ValidatePdf(
                        model.EmploymentDocument,
                        "Proof of Employment");
                }
                else
                {
                    ModelState.AddModelError(
                        string.Empty,
                        "Your account type could not be determined.");
                }

                if (!ModelState.IsValid)
                {
                    return View(model);
                }

                byte[] idBytes =
                    await ReadFileAsBytesAsync(model.IDDocument!);

                byte[]? registrationBytes = null;
                byte[]? employmentBytes = null;

                if (string.Equals(
                    user.UserType,
                    "Student",
                    StringComparison.OrdinalIgnoreCase))
                {
                    registrationBytes = await ReadFileAsBytesAsync(
                        model.RegistrationDocument!);
                }
                else if (string.Equals(
                    user.UserType,
                    "Staff",
                    StringComparison.OrdinalIgnoreCase))
                {
                    employmentBytes = await ReadFileAsBytesAsync(
                        model.EmploymentDocument!);
                }

                var application = new Application
                {
                    UserID = user.Id,
                    AccommodationID = accommodation.AccommodationID,
                    ApplicationDate = DateTime.UtcNow,
                    Status = "Pending",

                    IDDocument = idBytes,
                    IDDocumentName =
                        Path.GetFileName(model.IDDocument!.FileName),

                    RegistrationDocument = registrationBytes,
                    RegistrationDocumentName =
                        model.RegistrationDocument != null
                            ? Path.GetFileName(
                                model.RegistrationDocument.FileName)
                            : null,

                    EmploymentDocument = employmentBytes,
                    EmploymentDocumentName =
                        model.EmploymentDocument != null
                            ? Path.GetFileName(
                                model.EmploymentDocument.FileName)
                            : null,

                    NumberOfVehicles = model.NumberOfVehicles,

                    // Parking fee is calculated by the application,
                    // not accepted from browser-submitted values.
                    ParkingFee = 0m
                };

                _repository.Application.CreateApplication(application);

                // Notify the appropriate staff members.
                await NotifyApplicationStaffAsync(application, user);

                _repository.Save();

                TempData["SuccessMessage"] =
                    "Your accommodation application was submitted successfully.";

                return RedirectToAction(
                    nameof(Details),
                    new { id = application.ApplicationID });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error submitting accommodation application for accommodation {AccommodationID}.",
                    model.AccommodationID);

                ModelState.AddModelError(
                    string.Empty,
                    "The application could not be saved. Please try again. If the problem continues, contact the administrator.");

                var accommodation = _repository.Accommodation
                    .GetAccommodationById(model.AccommodationID);

                ViewBag.Accommodation = accommodation;

                return View(model);
            }
        }

        // =========================================================
        // DETAILS
        // =========================================================

        [HttpGet]
        public IActionResult Details(int id)
        {
            try
            {
                var application = _repository.Application
                    .GetApplicationById(id);

                if (application == null)
                {
                    return NotFound();
                }

                if (User.IsInRole("Admin") ||
                    User.IsInRole("FinancialOfficer"))
                {
                    ViewBag.AvailableRooms = _repository.Room
                        .FindAvailableByAccommodation(
                            application.AccommodationID)
                        .ToList();

                    return View(application);
                }

                if (!User.IsInRole("Customer"))
                {
                    return Forbid();
                }

                var userId = User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

                if (application.UserID != userId)
                {
                    return Forbid();
                }

                return View(application);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error loading application {ApplicationID}.",
                    id);

                TempData["ErrorMessage"] =
                    "Unable to load the application.";

                return RedirectToAction(nameof(Index));
            }
        }

        // =========================================================
        // WITHDRAW PENDING APPLICATION
        // =========================================================

        [Authorize(Roles = "Customer")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Withdraw(int id)
        {
            var application = _repository.Application
                .GetApplicationById(id);

            if (application == null)
            {
                return NotFound();
            }

            var userId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (application.UserID != userId)
            {
                return Forbid();
            }

            if (!string.Equals(
                application.Status,
                "Pending",
                StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] =
                    "Only pending applications can be withdrawn.";

                return RedirectToAction(nameof(Details), new { id });
            }

            application.Status = "Withdrawn";
            application.RejectionReason = null;

            _repository.Application.UpdateApplication(application);

            _repository.Notification.CreateNotification(new Notification
            {
                UserID = application.UserID,
                Title = "Application Withdrawn",
                Message =
                    $"Your application for {application.Accommodation?.Name ?? "the selected residence"} has been withdrawn.",
                DateCreated = DateTime.UtcNow
            });

            _repository.Save();

            TempData["SuccessMessage"] =
                "Your pending application was withdrawn successfully.";

            return RedirectToAction(nameof(Index));
        }

        // =========================================================
        // VIEW PDF DOCUMENT
        // =========================================================

        [HttpGet]
        public IActionResult ViewDocument(int id, string type)
        {
            try
            {
                var application = _repository.Application
                    .GetApplicationById(id);

                if (application == null)
                {
                    return NotFound();
                }

                var isManagement =
                    User.IsInRole("Admin") ||
                    User.IsInRole("FinancialOfficer");

                if (!isManagement)
                {
                    if (!User.IsInRole("Customer"))
                    {
                        return Forbid();
                    }

                    var userId = User.FindFirstValue(
                        ClaimTypes.NameIdentifier);

                    if (application.UserID != userId)
                    {
                        return Forbid();
                    }
                }

                byte[]? document = null;
                string fileName = "document.pdf";

                switch (type?.ToLowerInvariant())
                {
                    case "id":
                        document = application.IDDocument;
                        fileName = application.IDDocumentName ?? "ID.pdf";
                        break;

                    case "registration":
                        document = application.RegistrationDocument;
                        fileName =
                            application.RegistrationDocumentName
                            ?? "Registration.pdf";
                        break;

                    case "employment":
                        document = application.EmploymentDocument;
                        fileName =
                            application.EmploymentDocumentName
                            ?? "Employment.pdf";
                        break;

                    default:
                        return BadRequest("Invalid document type.");
                }

                if (document == null || document.Length == 0)
                {
                    return NotFound(
                        "The requested document was not found.");
                }

                return File(
                    document,
                    "application/pdf",
                    fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error loading application document.");

                return StatusCode(
                    500,
                    "Unable to load the document.");
            }
        }

        // =========================================================
        // APPROVE APPLICATION
        // =========================================================

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Approve(int id, int roomId)
        {
            try
            {
                if (id <= 0 || roomId <= 0)
                {
                    TempData["ErrorMessage"] =
                        "Please select a valid application and room.";

                    return RedirectToAction(
                        nameof(Details),
                        new { id });
                }

                var application = _repository.Application
                    .GetApplicationById(id);

                if (application == null)
                {
                    TempData["ErrorMessage"] =
                        "The application could not be found.";

                    return RedirectToAction(nameof(Index));
                }

                if (!string.Equals(
                    application.Status,
                    "Pending",
                    StringComparison.OrdinalIgnoreCase))
                {
                    TempData["ErrorMessage"] =
                        $"This application is already {application.Status}.";

                    return RedirectToAction(
                        nameof(Details),
                        new { id });
                }

                var accommodation = _repository.Accommodation
                    .GetAccommodationById(
                        application.AccommodationID);

                if (accommodation == null)
                {
                    TempData["ErrorMessage"] =
                        "The accommodation could not be found.";

                    return RedirectToAction(
                        nameof(Details),
                        new { id });
                }

                var room = _repository.Room.GetRoomById(roomId);

                if (room == null)
                {
                    TempData["ErrorMessage"] =
                        "The selected room could not be found.";

                    return RedirectToAction(
                        nameof(Details),
                        new { id });
                }

                if (room.AccommodationID != application.AccommodationID)
                {
                    TempData["ErrorMessage"] =
                        "The selected room does not belong to this accommodation.";

                    return RedirectToAction(
                        nameof(Details),
                        new { id });
                }

                if (!string.Equals(
                    room.Status,
                    "Available",
                    StringComparison.OrdinalIgnoreCase))
                {
                    TempData["ErrorMessage"] =
                        $"Room {room.RoomNumber} is no longer available.";

                    return RedirectToAction(
                        nameof(Details),
                        new { id });
                }

                // Approve ONLY the selected application.
                // Other applications belonging to this customer remain
                // unchanged, whether pending or already approved.

                application.Status = "Approved";
                application.RoomID = room.RoomID;
                application.RejectionReason = null;

                // Parking becomes payable only after approval and room allocation.
                var configuredCharges = _repository.AccommodationCharge
                    .FindByAccommodation(accommodation.AccommodationID)
                    .ToList();
                application.ParkingFee = ParkingFeeCalculator.Calculate(
                    accommodation,
                    application.NumberOfVehicles,
                    configuredCharges);

                room.Status = "Occupied";

                _repository.Application.UpdateApplication(application);
                _repository.Room.UpdateRoom(room);

                var accommodationRooms = _repository.Room
                    .FindByAccommodation(accommodation.AccommodationID)
                    .ToList();

                accommodation.TotalRooms = accommodationRooms.Count;

                accommodation.AvailableRooms = accommodationRooms.Count(r =>
                    string.Equals(
                        r.Status,
                        "Available",
                        StringComparison.OrdinalIgnoreCase));

                accommodation.Available =
                    accommodation.AvailableRooms > 0;

                _repository.Accommodation
                    .UpdateAccommodation(accommodation);

                // Do not cancel, reject, or modify any other application.
                // Customers are permitted to apply for multiple
                // different accommodations.

                _repository.Notification.CreateNotification(new Notification
                {
                    UserID = application.UserID,
                    Title = "Accommodation Application Approved",
                    Message = ParkingFeeCalculator.AddParkingNote(
                        $"Your application has been approved. You have been allocated Room {room.RoomNumber} at {accommodation.Name}.",
                        application.ParkingFee),
                    DateCreated = DateTime.UtcNow
                });

                _repository.Save();

                TempData["SuccessMessage"] =
                    $"Application #{application.ApplicationID} approved successfully. Room {room.RoomNumber} has been allocated.";

                return RedirectToAction(
                    nameof(Details),
                    new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error approving application {ApplicationID} with room {RoomID}.",
                    id,
                    roomId);

                // Log technical details, but do not expose exception
                // details to the user.
                TempData["ErrorMessage"] =
                    "The application could not be approved. Please try again.";

                return RedirectToAction(
                    nameof(Details),
                    new { id });
            }
        }

        // =========================================================
        // REJECT APPLICATION
        // =========================================================

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Reject(int id, string reason)
        {
            try
            {
                if (id <= 0)
                {
                    return BadRequest();
                }

                if (string.IsNullOrWhiteSpace(reason))
                {
                    TempData["ErrorMessage"] =
                        "A rejection reason is required.";

                    return RedirectToAction(
                        nameof(Details),
                        new { id });
                }

                reason = reason.Trim();

                if (reason.Length > 1000)
                {
                    TempData["ErrorMessage"] =
                        "The rejection reason cannot exceed 1000 characters.";

                    return RedirectToAction(
                        nameof(Details),
                        new { id });
                }

                var application = _repository.Application
                    .GetApplicationById(id);

                if (application == null)
                {
                    return NotFound();
                }

                if (!string.Equals(
                    application.Status,
                    "Pending",
                    StringComparison.OrdinalIgnoreCase))
                {
                    TempData["ErrorMessage"] =
                        "Only pending applications can be rejected.";

                    return RedirectToAction(
                        nameof(Details),
                        new { id });
                }

                // Reject only the selected application.
                application.Status = "Rejected";
                application.RejectionReason = reason;

                _repository.Application.UpdateApplication(application);

                _repository.Notification.CreateNotification(new Notification
                {
                    UserID = application.UserID,
                    Title = "Accommodation Application Rejected",
                    Message =
                        $"Your accommodation application was rejected. Reason: {reason}",
                    DateCreated = DateTime.UtcNow
                });

                _repository.Save();

                TempData["SuccessMessage"] =
                    $"Application #{application.ApplicationID} was rejected.";

                return RedirectToAction(
                    nameof(Details),
                    new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error rejecting application {ApplicationID}.",
                    id);

                TempData["ErrorMessage"] =
                    "The application could not be rejected.";

                return RedirectToAction(
                    nameof(Details),
                    new { id });
            }
        }

        // =========================================================
        // NOTIFY STAFF
        // =========================================================

        private async Task NotifyApplicationStaffAsync(
            Application application,
            ApplicationUser customer)
        {
            var accommodation = _repository.Accommodation
                .GetAccommodationById(application.AccommodationID);

            var message =
                $"Customer {customer.FirstName} {customer.Surname} submitted a new accommodation application for {accommodation?.Name ?? "a residence"}. Review and verify the application documents.";

            foreach (var role in new[] { "Admin", "FinancialOfficer" })
            {
                var staff = await _userManager.GetUsersInRoleAsync(role);

                foreach (var staffUser in staff)
                {
                    _repository.Notification.CreateNotification(
                        new Notification
                        {
                            UserID = staffUser.Id,
                            Title = "New Accommodation Application",
                            Message = message,
                            DateCreated = DateTime.UtcNow
                        });
                }
            }
        }

        // =========================================================
        // VALIDATE PDF
        // =========================================================

        private void ValidatePdf(
            IFormFile? file,
            string documentName)
        {
            if (file == null)
            {
                ModelState.AddModelError(
                    string.Empty,
                    $"{documentName} is required.");

                return;
            }

            if (file.Length <= 0)
            {
                ModelState.AddModelError(
                    string.Empty,
                    $"{documentName} is empty.");

                return;
            }

            if (file.Length > MaxFileSize)
            {
                ModelState.AddModelError(
                    string.Empty,
                    $"{documentName} must not exceed 5 MB.");

                return;
            }

            var extension = Path.GetExtension(file.FileName)
                .ToLowerInvariant();

            if (extension != ".pdf")
            {
                ModelState.AddModelError(
                    string.Empty,
                    $"{documentName} must be a PDF file.");

                return;
            }

            if (!string.Equals(
                file.ContentType,
                "application/pdf",
                StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(
                    string.Empty,
                    $"{documentName} must be a valid PDF file.");
            }
        }

        // =========================================================
        // READ PDF
        // =========================================================

        private async Task<byte[]> ReadFileAsBytesAsync(IFormFile file)
        {
            await using var memoryStream = new MemoryStream();

            await file.CopyToAsync(memoryStream);

            return memoryStream.ToArray();
        }
    }
}