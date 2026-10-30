using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using UFSStayConnect.Data;
using UFSStayConnect.DataAccess;
using UFSStayConnect.Models;

namespace UFSStayConnect.Controllers.Api
{
    public class ApplicationSubmitRequest
    {
        public int AccommodationId { get; set; }
        public int NumberOfVehicles { get; set; }
        public bool DeclarationAccepted { get; set; }
        public IFormFile? IDDocument { get; set; }
        public IFormFile? RegistrationDocument { get; set; }
        public IFormFile? EmploymentDocument { get; set; }
    }

    /// <summary>
    /// Mobile equivalent of the web ApplicationController (customer side):
    /// list, details, submit (with PDFs), withdraw, view document.
    /// </summary>
    [ApiController]
    [Route("api/applications")]
    [Authorize(AuthenticationSchemes = ApiHelpers.Bearer)]
    public class ApiApplicationsController : ControllerBase
    {
        private const long MaxFileSize = 5 * 1024 * 1024;

        private readonly IRepositoryWrapper _repository;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<ApiApplicationsController> _logger;
        private readonly AppDbContext _context;

        public ApiApplicationsController(
            IRepositoryWrapper repository,
             AppDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<ApiApplicationsController> logger)
        {
            _repository = repository;
            _userManager = userManager;
            _logger = logger;
            _context = context;
        }

        // GET api/applications
        [HttpGet]
        public async Task<IActionResult> List()
        {
            var user = await ApiHelpers.ResolveUserAsync(_userManager, User);
            if (user == null) return Unauthorized();

            var list = _repository.Application
                .FindByUser(user.Id)
                .OrderByDescending(a => a.ApplicationDate)
                .ToList()
                .Select(a => ToDto(ApiHelpers.Hydrate(_repository, a)))
                .ToList();

            return Ok(list);
        }

        // GET api/applications/5
        //[HttpGet("{id:int}")]
        //public async Task<IActionResult> Get(int id)
        //{
        //    var user = await ApiHelpers.ResolveUserAsync(_userManager, User);
        //    if (user == null) return Unauthorized();

        //    var application = _repository.Application.GetApplicationById(id);
        //    if (application == null) return NotFound(new { message = "Application not found." });
        //    if (application.UserID != user.Id) return Forbid();

        //    return Ok(ToDto(ApiHelpers.Hydrate(_repository, application)));
        //}

        // POST api/applications  (multipart/form-data)
        [HttpPost]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(20 * 1024 * 1024)]
        public async Task<IActionResult> Submit([FromForm] ApplicationSubmitRequest model)
        {
            try
            {
                var user = await ApiHelpers.ResolveUserAsync(_userManager, User);
                if (user == null) return Unauthorized();

                var accommodation = _repository.Accommodation
                    .GetAccommodationById(model.AccommodationId);

                if (accommodation == null)
                    return NotFound(new { message = "Accommodation not found." });

                if (!accommodation.Available || accommodation.AvailableRooms <= 0)
                    return BadRequest(new { message = "This accommodation is no longer available." });

                var duplicate = _repository.Application
                    .FindByUser(user.Id)
                    .ToList()
                    .Any(a => a.AccommodationID == model.AccommodationId &&
                              (ApiHelpers.Is(a.Status, "Pending") ||
                               ApiHelpers.Is(a.Status, "Approved")));

                if (duplicate)
                    return BadRequest(new { message = "You already have an active application for this accommodation." });

                if (!model.DeclarationAccepted)
                    return BadRequest(new { message = "You must accept the declaration before submitting." });

                if (model.NumberOfVehicles < 0 || model.NumberOfVehicles > 10)
                    return BadRequest(new { message = "Number of vehicles must be between 0 and 10." });

                var error = ValidatePdf(model.IDDocument, "Certified Copy of ID");
                if (error != null) return BadRequest(new { message = error });

                var isStudent = ApiHelpers.Is(user.UserType, "Student");
                var isStaff = ApiHelpers.Is(user.UserType, "Staff");

                if (!isStudent && !isStaff)
                    return BadRequest(new { message = "Your account type could not be determined." });

                error = isStudent
                    ? ValidatePdf(model.RegistrationDocument, "Proof of Registration")
                    : ValidatePdf(model.EmploymentDocument, "Proof of Employment");

                if (error != null) return BadRequest(new { message = error });

                var application = new Application
                {
                    UserID = user.Id,
                    AccommodationID = accommodation.AccommodationID,
                    ApplicationDate = DateTime.UtcNow,
                    Status = "Pending",

                    IDDocument = await ReadBytes(model.IDDocument!),
                    IDDocumentName = Path.GetFileName(model.IDDocument!.FileName),

                    RegistrationDocument = isStudent ? await ReadBytes(model.RegistrationDocument!) : null,
                    RegistrationDocumentName = isStudent ? Path.GetFileName(model.RegistrationDocument!.FileName) : null,

                    EmploymentDocument = isStaff ? await ReadBytes(model.EmploymentDocument!) : null,
                    EmploymentDocumentName = isStaff ? Path.GetFileName(model.EmploymentDocument!.FileName) : null,

                    NumberOfVehicles = model.NumberOfVehicles,
                    ParkingFee = 0m
                };

                _repository.Application.CreateApplication(application);
                await NotifyStaffAsync(application, user, accommodation.Name);
                _repository.Save();

                return Ok(new
                {
                    message = "Your accommodation application was submitted successfully.",
                    applicationId = application.ApplicationID
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting application via API.");
                return StatusCode(500, new { message = "The application could not be saved. Please try again." });
            }
        }

        // POST api/applications/5/withdraw
        [HttpPost("{id:int}/withdraw")]
        public async Task<IActionResult> Withdraw(int id)
        {
            var user = await ApiHelpers.ResolveUserAsync(_userManager, User);
            if (user == null) return Unauthorized();

            var application = _repository.Application.GetApplicationById(id);
            if (application == null) return NotFound(new { message = "Application not found." });
            if (application.UserID != user.Id) return Forbid();

            if (!ApiHelpers.Is(application.Status, "Pending"))
                return BadRequest(new { message = "Only pending applications can be withdrawn." });

            application.Status = "Withdrawn";
            application.RejectionReason = null;
            _repository.Application.UpdateApplication(application);

            _repository.Notification.CreateNotification(new Notification
            {
                UserID = application.UserID,
                Title = "Application Withdrawn",
                Message = $"Your application for {application.Accommodation?.Name ?? "the selected residence"} has been withdrawn.",
                DateCreated = DateTime.UtcNow
            });

            _repository.Save();

            return Ok(new { message = "Your pending application was withdrawn successfully." });
        }

        // GET api/applications/5/documents/id|registration|employment
        [HttpGet("{id:int}/documents/{type}")]
        public async Task<IActionResult> Document(int id, string type)
        {
            var user = await ApiHelpers.ResolveUserAsync(_userManager, User);
            if (user == null) return Unauthorized();

            var application = _repository.Application.GetApplicationById(id);
            if (application == null) return NotFound();
            if (application.UserID != user.Id) return Forbid();

            byte[]? bytes;
            string name;

            switch (type.ToLowerInvariant())
            {
                case "id":
                    bytes = application.IDDocument;
                    name = application.IDDocumentName ?? "ID.pdf";
                    break;
                case "registration":
                    bytes = application.RegistrationDocument;
                    name = application.RegistrationDocumentName ?? "Registration.pdf";
                    break;
                case "employment":
                    bytes = application.EmploymentDocument;
                    name = application.EmploymentDocumentName ?? "Employment.pdf";
                    break;
                default:
                    return BadRequest(new { message = "Invalid document type." });
            }

            if (bytes == null || bytes.Length == 0)
                return NotFound(new { message = "The requested document was not found." });

            return File(bytes, "application/pdf", name);
        }

        // ------------------------------------------------------------

        private object ToDto(Application a)
        {
            var status = a.Status ?? "Pending";
            var pending = ApiHelpers.Is(status, "Pending");
            var approved = ApiHelpers.Is(status, "Approved");
            var rejected = ApiHelpers.Is(status, "Rejected");

            var decisionTitle =
                approved ? "Application approved" :
                rejected ? "Application rejected" :
                ApiHelpers.Is(status, "Withdrawn") ? "Application withdrawn" :
                ApiHelpers.Is(status, "Cancelled") ? "Application cancelled" :
                "Decision";

            var timeline = new object[]
            {
                new { title = "Application submitted", completed = true, date = ApiHelpers.Iso(a.ApplicationDate) },
                new { title = "Documents under review", completed = !pending, date = "" },
                new { title = pending ? "Awaiting decision" : decisionTitle, completed = !pending, date = "" }
            };

            string? message = null;
            if (approved)
            {
                message = a.Room != null
                    ? $"Your application has been approved. You have been allocated Room {a.Room.RoomNumber} at {a.Accommodation?.Name}."
                    : "Your application has been approved.";
            }
            else if (rejected && !string.IsNullOrWhiteSpace(a.RejectionReason))
            {
                message = a.RejectionReason;
            }

            return new
            {
                applicationId = a.ApplicationID,
                accommodationId = a.AccommodationID,
                accommodationName = a.Accommodation?.Name ?? $"Accommodation #{a.AccommodationID}",
                location = a.Accommodation?.Location,
                roomNumber = a.Room?.RoomNumber,
                status,
                submittedAt = ApiHelpers.Iso(a.ApplicationDate),
                rejectionReason = a.RejectionReason ?? "",
                numberOfVehicles = a.NumberOfVehicles,
                canWithdraw = pending,
                message = message ?? "",
                imagePath = ApiHelpers.ImagePath(Request, a.Accommodation),
                timeline
            };
        }


        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetApplicationDetails(int id)
        {
            var user = await ApiHelpers.ResolveUserAsync(_userManager, User);

            if (user == null)
                return Unauthorized(new { message = "Please sign in again." });

            var application = await _context.Applications
                .AsNoTracking()
                .Include(a => a.User)
                .Include(a => a.Accommodation)
                .Include(a => a.Room)
                .FirstOrDefaultAsync(a =>
                    a.ApplicationID == id &&
                    a.UserID == user.Id);

            if (application == null)
            {
                return NotFound(new
                {
                    message = "Application not found."
                });
            }

            var applicant = application.User;
            var accommodation = application.Accommodation;

            return Ok(new
            {
                applicationId = application.ApplicationID,
                accommodationId = application.AccommodationID,
                applicationDate = application.ApplicationDate,
                submittedAt = application.ApplicationDate,
                status = application.Status,
                rejectionReason = application.RejectionReason,
                moveInDate = application.MoveInDate,
                numberOfVehicles = application.NumberOfVehicles,
                parkingFee = application.ParkingFee,
                roomNumber = application.Room?.RoomNumber,

                applicant = applicant == null ? null : new
                {
                    fullName = $"{applicant.FirstName} {applicant.Surname}".Trim(),
                    firstName = applicant.FirstName,
                    surname = applicant.Surname,
                    email = applicant.Email,
                    phoneNumber = applicant.PhoneNumber,
                    userType = applicant.UserType,
                    studentNumber = applicant.StudentNumber,
                    employeeNumber = applicant.EmployeeNumber
                },

                accommodation = accommodation == null ? null : new
                {
                    accommodationId = accommodation.AccommodationID,
                    name = accommodation.Name,
                    location = accommodation.Location,
                    type = accommodation.Type,
                    description = accommodation.Description,
                    monthlyRent = accommodation.MonthlyRent,
                    bedrooms = accommodation.Bedrooms,
                    bathrooms = accommodation.Bathrooms,
                    totalRooms = accommodation.TotalRooms,
                    availableRooms = accommodation.AvailableRooms,
                    imagePath = accommodation.ImagePath
                },

                documents = new
                {
                    hasIdDocument = application.IDDocument != null
                                    && application.IDDocument.Length > 0,
                    idDocumentName = application.IDDocumentName,

                    hasRegistrationDocument =
                        application.RegistrationDocument != null
                        && application.RegistrationDocument.Length > 0,
                    registrationDocumentName = application.RegistrationDocumentName,

                    hasFundingDocument =
                        application.FundingDocument != null
                        && application.FundingDocument.Length > 0,
                    fundingDocumentName = application.FundingDocumentName,

                    hasEmploymentDocument =
                        application.EmploymentDocument != null
                        && application.EmploymentDocument.Length > 0,
                    employmentDocumentName = application.EmploymentDocumentName
                }
            });
        }


        private static string? ValidatePdf(IFormFile? file, string name)
        {
            if (file == null) return $"{name} is required.";
            if (file.Length <= 0) return $"{name} is empty.";
            if (file.Length > MaxFileSize) return $"{name} must not exceed 5 MB.";

            if (Path.GetExtension(file.FileName).ToLowerInvariant() != ".pdf")
                return $"{name} must be a PDF file.";

            return null;
        }

        private static async Task<byte[]> ReadBytes(IFormFile file)
        {
            await using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            return ms.ToArray();
        }

        private async Task NotifyStaffAsync(Application application, ApplicationUser customer, string? accommodationName)
        {
            var message =
                $"Customer {customer.FirstName} {customer.Surname} submitted a new accommodation application for {accommodationName ?? "a residence"}. Review and verify the application documents.";

            foreach (var role in new[] { "Admin", "FinancialOfficer" })
            {
                foreach (var staff in await _userManager.GetUsersInRoleAsync(role))
                {
                    _repository.Notification.CreateNotification(new Notification
                    {
                        UserID = staff.Id,
                        Title = "New Accommodation Application",
                        Message = message,
                        DateCreated = DateTime.UtcNow
                    });
                }
            }
        }
    }
}