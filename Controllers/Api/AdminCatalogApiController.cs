using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UFSStayConnect.Data;
using UFSStayConnect.Models;
using UFSStayConnect.Services;
using System.IO;

namespace UFSStayConnect.Controllers.Api;

/// <summary>Mobile endpoints for the Administrator's accommodation/service configuration and staff usage recording.</summary>
[ApiController]
[Route("api/admin/catalog")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
public class AdminCatalogApiController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<AdminCatalogApiController> _logger;
    private readonly IWebHostEnvironment _environment;

    public AdminCatalogApiController(
        AppDbContext db,
        ILogger<AdminCatalogApiController> logger,
        IWebHostEnvironment environment)
    {
        _db = db;
        _logger = logger;
        _environment = environment;
    }

    // ---------------------------- accommodation ----------------------------
    [HttpGet("accommodations")]
    public async Task<IActionResult> Accommodations()
    {
        var items = await _db.Accommodations.AsNoTracking().Include(a => a.Rooms).OrderBy(a => a.Name).Select(a => new
        {
            accommodationId = a.AccommodationID,
            name = a.Name,
            location = a.Location,
            type = a.Type,
            description = a.Description,
            monthlyRent = a.MonthlyRent,
            bedrooms = a.Bedrooms,
            bathrooms = a.Bathrooms,
            totalRooms = a.TotalRooms,
            availableRooms = a.AvailableRooms,
            available = a.Available,
            imagePath = a.ImagePath ?? "",
            actualRoomCount = a.Rooms.Count,
            occupiedRoomCount = a.Rooms.Count(r => r.Status == "Occupied")
        }).ToListAsync();
        return Ok(new { total = items.Count, accommodations = items });
    }


    /// <summary>
    /// Receives the photo selected from Android's accommodation form.
    /// The path is saved on the same Accommodation.ImagePath field used by
    /// the public Android API and the website's accommodation views.
    /// </summary>
    [HttpPost("accommodations/{id:int}/image")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(5 * 1024 * 1024 + 128 * 1024)]
    public async Task<IActionResult> UploadAccommodationImage(int id, [FromForm] IFormFile? image)
    {
        var accommodation = await _db.Accommodations.FirstOrDefaultAsync(a => a.AccommodationID == id);
        if (accommodation == null)
            return NotFound(new { message = "Accommodation not found." });

        if (image == null || image.Length <= 0)
            return BadRequest(new { message = "Choose an accommodation photo to upload." });

        const long maxImageSize = 5 * 1024 * 1024;
        if (image.Length > maxImageSize)
            return BadRequest(new { message = "The accommodation photo cannot exceed 5 MB." });

        var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
        if (extension is not ".jpg" and not ".jpeg" and not ".png" and not ".webp")
            return BadRequest(new { message = "Only JPG, JPEG, PNG and WEBP photos are supported." });

        // Verify the actual image signature instead of trusting the file name.
        byte[] header = new byte[12];
        int bytesRead;
        await using (var validationStream = image.OpenReadStream())
        {
            bytesRead = await validationStream.ReadAsync(header.AsMemory(0, header.Length));
        }
        if (!IsSupportedImageHeader(header, bytesRead, extension))
            return BadRequest(new { message = "The selected file is not a valid JPG, PNG or WEBP image." });

        var webRoot = _environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot))
            webRoot = Path.Combine(_environment.ContentRootPath, "wwwroot");

        var folder = Path.Combine(webRoot, "images", "accommodation");
        Directory.CreateDirectory(folder);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var physicalPath = Path.Combine(folder, fileName);
        try
        {
            await using (var stream = new FileStream(physicalPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            await using (var source = image.OpenReadStream())
            {
                await source.CopyToAsync(stream);
            }

            var oldImagePath = accommodation.ImagePath;
            accommodation.ImagePath = $"/images/accommodation/{fileName}";
            await _db.SaveChangesAsync();
            TryDeletePreviousAccommodationImage(oldImagePath, folder, fileName);

            return Ok(new
            {
                message = "Accommodation photo uploaded successfully.",
                accommodationId = accommodation.AccommodationID,
                imagePath = accommodation.ImagePath
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not save accommodation photo for {AccommodationId}.", id);
            try { if (System.IO.File.Exists(physicalPath)) System.IO.File.Delete(physicalPath); } catch { }
            return StatusCode(500, new { message = "The photo could not be saved. Please try again." });
        }
    }

    private static bool IsSupportedImageHeader(byte[] header, int length, string extension)
    {
        if ((extension == ".jpg" || extension == ".jpeg") && length >= 3)
            return header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;

        if (extension == ".png" && length >= 8)
            return header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47
                && header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A;

        if (extension == ".webp" && length >= 12)
            return header[0] == (byte)'R' && header[1] == (byte)'I' && header[2] == (byte)'F' && header[3] == (byte)'F'
                && header[8] == (byte)'W' && header[9] == (byte)'E' && header[10] == (byte)'B' && header[11] == (byte)'P';

        return false;
    }

    private static void TryDeletePreviousAccommodationImage(string? oldImagePath, string folder, string newFileName)
    {
        if (string.IsNullOrWhiteSpace(oldImagePath)) return;
        var oldName = Path.GetFileName(oldImagePath);
        if (string.IsNullOrWhiteSpace(oldName) || string.Equals(oldName, newFileName, StringComparison.OrdinalIgnoreCase)) return;
        var oldPath = Path.Combine(folder, oldName);
        try { if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath); } catch { }
    }

    public sealed class AccommodationRequest
    {
        public string? Name { get; set; }
        public string? Location { get; set; }
        public string? Type { get; set; }
        public string? Description { get; set; }
        public decimal MonthlyRent { get; set; }
        public int Bedrooms { get; set; } = 1; public int Bathrooms { get; set; } = 1;
        public int TotalRooms { get; set; } = 1; public bool Available { get; set; } = true;
    }
    private IActionResult? ValidateAccommodation(AccommodationRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Name) || string.IsNullOrWhiteSpace(r.Location) || string.IsNullOrWhiteSpace(r.Type) || string.IsNullOrWhiteSpace(r.Description)) return BadRequest(new { message = "Name, location, type and description are required." });
        if (r.MonthlyRent < 0 || r.Bedrooms < 1 || r.Bathrooms < 1 || r.TotalRooms < 1 || r.TotalRooms > 100) return BadRequest(new { message = "Rent must be non-negative; bedrooms/bathrooms must be at least 1; total rooms must be 1–100." });
        return null;
    }
    [HttpPost("accommodations")]
    public async Task<IActionResult> CreateAccommodation([FromBody] AccommodationRequest request)
    {
        var validation = ValidateAccommodation(request); if (validation != null) return validation;
        var a = new Accommodation
        {
            Name = request.Name!.Trim(),
            Location = request.Location!.Trim(),
            Type = request.Type!.Trim(),
            Description = request.Description!.Trim(),
            MonthlyRent = request.MonthlyRent,
            Bedrooms = request.Bedrooms,
            Bathrooms = request.Bathrooms,
            TotalRooms = request.TotalRooms,
            AvailableRooms = request.Available && request.TotalRooms > 0 ? request.TotalRooms : 0,
            Available = request.Available && request.TotalRooms > 0,
            DateAdded = DateTime.UtcNow
        };
        await using var tx = await _db.Database.BeginTransactionAsync();
        _db.Accommodations.Add(a); await _db.SaveChangesAsync();
        for (var i = 1; i <= request.TotalRooms; i++) _db.Rooms.Add(new Room { AccommodationID = a.AccommodationID, RoomNumber = i.ToString("D2"), RoomType = "Standard", Status = request.Available ? "Available" : "Unavailable" });
        var rentType = await _db.PaymentTypes.FirstOrDefaultAsync(p => p.Name.ToLower() == "rent");
        if (rentType != null && !await _db.AccommodationCharges.AnyAsync(c => c.AccommodationID == a.AccommodationID && c.PaymentTypeID == rentType.PaymentTypeID))
            _db.AccommodationCharges.Add(new AccommodationCharge { AccommodationID = a.AccommodationID, PaymentTypeID = rentType.PaymentTypeID, Amount = a.MonthlyRent, Frequency = "Monthly", DueDate = PaymentDueDateCalculator.DefaultDueDate(), ReminderDaysBeforeDue = 3, ReminderDaysAfterDue = 3, Description = "Monthly accommodation rent.", IsActive = true });
        await _db.SaveChangesAsync(); await tx.CommitAsync();
        return Ok(new { message = "Accommodation created with its initial rooms.", accommodationId = a.AccommodationID });
    }

    [HttpPut("accommodations/{id:int}")]
    public async Task<IActionResult> UpdateAccommodation(int id, [FromBody] AccommodationRequest request)
    {
        var a = await _db.Accommodations.Include(x => x.Rooms).FirstOrDefaultAsync(x => x.AccommodationID == id);
        if (a == null) return NotFound(new { message = "Accommodation not found." });
        var validation = ValidateAccommodation(request); if (validation != null) return validation;
        a.Name = request.Name!.Trim(); a.Location = request.Location!.Trim(); a.Type = request.Type!.Trim(); a.Description = request.Description!.Trim();
        a.MonthlyRent = request.MonthlyRent; a.Bedrooms = request.Bedrooms; a.Bathrooms = request.Bathrooms;
        a.TotalRooms = a.Rooms.Count > 0 ? a.Rooms.Count : request.TotalRooms;
        a.AvailableRooms = a.Rooms.Count > 0 ? a.Rooms.Count(r => r.Status == "Available") : (request.Available ? request.TotalRooms : 0);
        a.Available = request.Available && a.AvailableRooms > 0;
        var rentType = await _db.PaymentTypes.FirstOrDefaultAsync(p => p.Name.ToLower() == "rent");
        if (rentType != null)
        {
            var charge = await _db.AccommodationCharges.FirstOrDefaultAsync(c => c.AccommodationID == id && c.PaymentTypeID == rentType.PaymentTypeID);
            if (charge != null) charge.Amount = a.MonthlyRent;
        }
        await _db.SaveChangesAsync(); return Ok(new { message = "Accommodation updated successfully." });
    }

    [HttpDelete("accommodations/{id:int}")]
    public async Task<IActionResult> DeleteAccommodation(int id)
    {
        var a = await _db.Accommodations.FirstOrDefaultAsync(x => x.AccommodationID == id);
        if (a == null) return NotFound(new { message = "Accommodation not found." });
        var chargeIds = await _db.AccommodationCharges.Where(c => c.AccommodationID == id).Select(c => c.AccommodationChargeID).ToListAsync();
        var hasApplications = await _db.Applications.AnyAsync(x => x.AccommodationID == id);
        var hasPayments = await _db.Payments.AnyAsync(p =>
            (p.Application != null && p.Application.AccommodationID == id) ||
            (p.AccommodationChargeID.HasValue && chargeIds.Contains(p.AccommodationChargeID.Value)));
        var hasReviews = await _db.Reviews.AnyAsync(r => r.AccommodationID == id);
        if (hasApplications || hasPayments || hasReviews)
            return Conflict(new { message = "This accommodation has application, payment, or review history. Disable availability instead of deleting it." });
        _db.AccommodationCharges.RemoveRange(_db.AccommodationCharges.Where(c => c.AccommodationID == id));
        _db.AccommodationServices.RemoveRange(_db.AccommodationServices.Where(s => s.AccommodationID == id));
        _db.Rooms.RemoveRange(_db.Rooms.Where(r => r.AccommodationID == id));
        _db.Accommodations.Remove(a);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Accommodation deleted." });
    }

    // ---------------------------- rooms ----------------------------
    [HttpGet("accommodations/{accommodationId:int}/rooms")]
    public async Task<IActionResult> Rooms(int accommodationId)
    {
        var exists = await _db.Accommodations.AnyAsync(a => a.AccommodationID == accommodationId); if (!exists) return NotFound(new { message = "Accommodation not found." });
        var rows = await _db.Rooms.AsNoTracking().Where(r => r.AccommodationID == accommodationId).OrderBy(r => r.RoomNumber)
            .Select(r => new { roomId = r.RoomID, accommodationId = r.AccommodationID, roomNumber = r.RoomNumber, floor = r.Floor ?? "", roomType = r.RoomType, status = r.Status, description = r.Description ?? "" }).ToListAsync();
        return Ok(new { total = rows.Count, rooms = rows });
    }
    public sealed class RoomRequest { public int AccommodationId { get; set; } public string? RoomNumber { get; set; } public string? Floor { get; set; } public string? RoomType { get; set; } public string? Status { get; set; } public string? Description { get; set; } }
    [HttpPost("rooms")]
    public async Task<IActionResult> CreateRoom([FromBody] RoomRequest r)
    {
        if (r.AccommodationId <= 0 || string.IsNullOrWhiteSpace(r.RoomNumber)) return BadRequest(new { message = "Accommodation and room number are required." });
        if (!await _db.Accommodations.AnyAsync(a => a.AccommodationID == r.AccommodationId)) return NotFound(new { message = "Accommodation not found." });
        if (await _db.Rooms.AnyAsync(x => x.AccommodationID == r.AccommodationId && x.RoomNumber == r.RoomNumber.Trim())) return Conflict(new { message = "That room number already exists for this accommodation." });
        var status = new[] { "Available", "Occupied", "Unavailable" }.Contains(r.Status) ? r.Status! : "Available";
        var room = new Room { AccommodationID = r.AccommodationId, RoomNumber = r.RoomNumber.Trim(), Floor = r.Floor?.Trim(), RoomType = string.IsNullOrWhiteSpace(r.RoomType) ? "Standard" : r.RoomType.Trim(), Status = status, Description = r.Description?.Trim() };
        _db.Rooms.Add(room);
        await _db.SaveChangesAsync();
        await RecountRooms(r.AccommodationId);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Room created.", roomId = room.RoomID });
    }
    [HttpPut("rooms/{id:int}")]
    public async Task<IActionResult> UpdateRoom(int id, [FromBody] RoomRequest r)
    {
        var room = await _db.Rooms.FirstOrDefaultAsync(x => x.RoomID == id); if (room == null) return NotFound(new { message = "Room not found." });
        if (string.IsNullOrWhiteSpace(r.RoomNumber)) return BadRequest(new { message = "Room number is required." });
        if (await _db.Rooms.AnyAsync(x => x.RoomID != id && x.AccommodationID == room.AccommodationID && x.RoomNumber == r.RoomNumber.Trim())) return Conflict(new { message = "That room number already exists." });
        var status = new[] { "Available", "Occupied", "Unavailable" }.Contains(r.Status) ? r.Status! : room.Status;
        if (status != "Occupied" && await _db.Applications.AnyAsync(a => a.RoomID == id && a.Status == "Approved")) return Conflict(new { message = "A room allocated to an approved application cannot be made available until that allocation is changed." });
        room.RoomNumber = r.RoomNumber.Trim(); room.Floor = r.Floor?.Trim(); room.RoomType = string.IsNullOrWhiteSpace(r.RoomType) ? "Standard" : r.RoomType.Trim(); room.Status = status; room.Description = r.Description?.Trim();
        await RecountRooms(room.AccommodationID); await _db.SaveChangesAsync(); return Ok(new { message = "Room updated." });
    }
    [HttpDelete("rooms/{id:int}")]
    public async Task<IActionResult> DeleteRoom(int id)
    {
        var room = await _db.Rooms.FirstOrDefaultAsync(x => x.RoomID == id); if (room == null) return NotFound(new { message = "Room not found." });
        if (await _db.Applications.AnyAsync(a => a.RoomID == id)) return Conflict(new { message = "This room is linked to application history and cannot be deleted." });
        var accId = room.AccommodationID; _db.Rooms.Remove(room); await _db.SaveChangesAsync(); await RecountRooms(accId); await _db.SaveChangesAsync(); return Ok(new { message = "Room deleted." });
    }
    private async Task RecountRooms(int accommodationId)
    {
        var a = await _db.Accommodations.FirstOrDefaultAsync(x => x.AccommodationID == accommodationId); if (a == null) return;
        var rooms = await _db.Rooms.Where(r => r.AccommodationID == accommodationId).ToListAsync();
        a.TotalRooms = rooms.Count; a.AvailableRooms = rooms.Count(r => r.Status == "Available"); a.Available = a.AvailableRooms > 0;
    }

    // ---------------------------- service catalogue / residence assignment ----------------------------
    [HttpGet("services")]
    public async Task<IActionResult> Services()
    {
        var rows = await _db.Services.AsNoTracking().OrderBy(s => s.ServiceName).Select(s => new { serviceId = s.ServiceID, serviceName = s.ServiceName, description = s.Description ?? "", ratePerUnit = s.RatePerUnit, chargingMethod = s.ChargingMethod, unitOfMeasure = s.UnitOfMeasure, isActive = s.IsActive }).ToListAsync();
        return Ok(new { total = rows.Count, services = rows });
    }
    public sealed class ServiceRequest { public string? ServiceName { get; set; } public string? Description { get; set; } public decimal RatePerUnit { get; set; } public string? ChargingMethod { get; set; } public string? UnitOfMeasure { get; set; } public bool IsActive { get; set; } = true; }
    [HttpPost("services")]
    public async Task<IActionResult> CreateService([FromBody] ServiceRequest r)
    {
        var name = r.ServiceName?.Trim() ?? ""; if (name.Length == 0) return BadRequest(new { message = "Service name is required." });
        if (r.RatePerUnit < 0) return BadRequest(new { message = "Rate cannot be negative." });
        if (await _db.Services.AnyAsync(s => s.ServiceName.ToLower() == name.ToLower())) return Conflict(new { message = "A service with this name already exists." });
        var mode = r.ChargingMethod == "Fixed" ? "Fixed" : "PayAsYouGo";
        var s = new Service { ServiceName = name, Description = r.Description?.Trim(), RatePerUnit = r.RatePerUnit, ChargingMethod = mode, UnitOfMeasure = string.IsNullOrWhiteSpace(r.UnitOfMeasure) ? "unit" : r.UnitOfMeasure.Trim(), IsActive = r.IsActive };
        _db.Services.Add(s); await _db.SaveChangesAsync(); return Ok(new { message = "Service created.", serviceId = s.ServiceID });
    }
    [HttpPut("services/{id:int}")]
    public async Task<IActionResult> UpdateService(int id, [FromBody] ServiceRequest r)
    {
        var s = await _db.Services.FirstOrDefaultAsync(x => x.ServiceID == id); if (s == null) return NotFound(new { message = "Service not found." });
        var name = r.ServiceName?.Trim() ?? ""; if (name.Length == 0 || r.RatePerUnit < 0) return BadRequest(new { message = "Enter a service name and a non-negative rate." });
        if (await _db.Services.AnyAsync(x => x.ServiceID != id && x.ServiceName.ToLower() == name.ToLower())) return Conflict(new { message = "A service with this name already exists." });
        s.ServiceName = name; s.Description = r.Description?.Trim(); s.RatePerUnit = r.RatePerUnit; s.ChargingMethod = r.ChargingMethod == "Fixed" ? "Fixed" : "PayAsYouGo"; s.UnitOfMeasure = string.IsNullOrWhiteSpace(r.UnitOfMeasure) ? "unit" : r.UnitOfMeasure.Trim(); s.IsActive = r.IsActive;
        await _db.SaveChangesAsync(); return Ok(new { message = "Service updated." });
    }
    [HttpPost("services/{id:int}/status")]
    public async Task<IActionResult> ToggleService(int id)
    {
        var s = await _db.Services.FirstOrDefaultAsync(x => x.ServiceID == id); if (s == null) return NotFound(new { message = "Service not found." });
        s.IsActive = !s.IsActive; await _db.SaveChangesAsync(); return Ok(new { message = $"Service {(s.IsActive ? "activated" : "deactivated")}.", isActive = s.IsActive });
    }

    [HttpGet("accommodations/{id:int}/services")]
    public async Task<IActionResult> AssignedServices(int id)
    {
        if (!await _db.Accommodations.AnyAsync(a => a.AccommodationID == id)) return NotFound(new { message = "Accommodation not found." });
        var services = await _db.Services.AsNoTracking().OrderBy(s => s.ServiceName).Select(s => new
        {
            serviceId = s.ServiceID,
            serviceName = s.ServiceName,
            description = s.Description ?? "",
            defaultRate = s.RatePerUnit,
            unitOfMeasure = s.UnitOfMeasure,
            isPayAsYouGo = s.ChargingMethod == "PayAsYouGo",
            isActive = s.IsActive,
            assignment = _db.AccommodationServices.Where(x => x.AccommodationID == id && x.ServiceID == s.ServiceID).Select(x => new { ratePerUnit = x.RatePerUnit, unitOfMeasure = x.UnitOfMeasure, freeUnits = x.FreeUnits, isPayAsYouGo = x.IsPayAsYouGo, isEnabled = x.IsEnabled }).FirstOrDefault()
        }).ToListAsync();
        return Ok(new { accommodationId = id, services });
    }
    public sealed class AssignmentRequest { public int ServiceId { get; set; } public decimal RatePerUnit { get; set; } public string? UnitOfMeasure { get; set; } public int FreeUnits { get; set; } public bool IsPayAsYouGo { get; set; } public bool IsEnabled { get; set; } }
    public sealed class AssignmentBatchRequest { public List<AssignmentRequest>? Items { get; set; } }
    [HttpPut("accommodations/{id:int}/services")]
    public async Task<IActionResult> SaveAssignments(int id, [FromBody] AssignmentBatchRequest request)
    {
        var accommodation = await _db.Accommodations.FirstOrDefaultAsync(a => a.AccommodationID == id); if (accommodation == null) return NotFound(new { message = "Accommodation not found." });
        foreach (var r in request?.Items ?? new List<AssignmentRequest>())
        {
            var service = await _db.Services.FirstOrDefaultAsync(s => s.ServiceID == r.ServiceId); if (service == null) continue;
            var assignment = await _db.AccommodationServices.FirstOrDefaultAsync(x => x.AccommodationID == id && x.ServiceID == r.ServiceId);
            if (assignment == null)
            {
                if (!r.IsEnabled) continue;
                assignment = new AccommodationService { AccommodationID = id, ServiceID = r.ServiceId }; _db.AccommodationServices.Add(assignment);
            }
            assignment.RatePerUnit = Math.Max(0m, r.RatePerUnit); assignment.UnitOfMeasure = string.IsNullOrWhiteSpace(r.UnitOfMeasure) ? service.UnitOfMeasure : r.UnitOfMeasure.Trim(); assignment.FreeUnits = Math.Max(0, r.FreeUnits); assignment.IsPayAsYouGo = r.IsPayAsYouGo; assignment.IsEnabled = r.IsEnabled;
        }
        await _db.SaveChangesAsync(); return Ok(new { message = "Accommodation services saved." });
    }

    // ---------------------------- payment types ----------------------------
    [HttpGet("payment-types")]
    public async Task<IActionResult> PaymentTypes()
    {
        var rows = await _db.PaymentTypes.AsNoTracking().OrderBy(p => p.Name).Select(p => new { paymentTypeId = p.PaymentTypeID, name = p.Name, description = p.Description ?? "", isActive = p.IsActive }).ToListAsync();
        return Ok(new { total = rows.Count, paymentTypes = rows });
    }
    public sealed class PaymentTypeRequest { public string? Name { get; set; } public string? Description { get; set; } public bool IsActive { get; set; } = true; }
    [HttpPost("payment-types")]
    public async Task<IActionResult> CreatePaymentType([FromBody] PaymentTypeRequest r)
    {
        var name = r.Name?.Trim() ?? ""; if (name.Length == 0) return BadRequest(new { message = "Payment type name is required." });
        if (await _db.PaymentTypes.AnyAsync(p => p.Name.ToLower() == name.ToLower())) return Conflict(new { message = "A payment type with this name already exists." });
        var p = new PaymentType { Name = name, Description = r.Description?.Trim(), IsActive = r.IsActive }; _db.PaymentTypes.Add(p); await _db.SaveChangesAsync(); return Ok(new { message = "Payment type created.", paymentTypeId = p.PaymentTypeID });
    }
    [HttpPut("payment-types/{id:int}")]
    public async Task<IActionResult> UpdatePaymentType(int id, [FromBody] PaymentTypeRequest r)
    {
        var p = await _db.PaymentTypes.FirstOrDefaultAsync(x => x.PaymentTypeID == id); if (p == null) return NotFound(new { message = "Payment type not found." });
        var name = r.Name?.Trim() ?? ""; if (name.Length == 0) return BadRequest(new { message = "Payment type name is required." });
        if (await _db.PaymentTypes.AnyAsync(x => x.PaymentTypeID != id && x.Name.ToLower() == name.ToLower())) return Conflict(new { message = "A payment type with this name already exists." });
        p.Name = name; p.Description = r.Description?.Trim(); p.IsActive = r.IsActive; await _db.SaveChangesAsync(); return Ok(new { message = "Payment type updated." });
    }
    [HttpPost("payment-types/{id:int}/status")]
    public async Task<IActionResult> TogglePaymentType(int id)
    {
        var p = await _db.PaymentTypes.FirstOrDefaultAsync(x => x.PaymentTypeID == id); if (p == null) return NotFound(new { message = "Payment type not found." });
        p.IsActive = !p.IsActive; await _db.SaveChangesAsync(); return Ok(new { message = $"Payment type {(p.IsActive ? "activated" : "deactivated")}.", isActive = p.IsActive });
    }

    // ---------------------------- accommodation charges ----------------------------
    [HttpGet("accommodations/{id:int}/charges")]
    public async Task<IActionResult> Charges(int id)
    {
        var accommodation = await _db.Accommodations.AsNoTracking().FirstOrDefaultAsync(a => a.AccommodationID == id); if (accommodation == null) return NotFound(new { message = "Accommodation not found." });
        var rows = await _db.AccommodationCharges.AsNoTracking().Include(c => c.PaymentType).Where(c => c.AccommodationID == id).OrderBy(c => c.PaymentType!.Name)
            .Select(c => new {
                accommodationChargeId = c.AccommodationChargeID,
                accommodationId = c.AccommodationID,
                accommodation = accommodation.Name,
                paymentTypeId = c.PaymentTypeID,
                paymentType = c.PaymentType == null ? "Unknown" : c.PaymentType.Name,
                amount = c.Amount,
                frequency = c.Frequency,
                dueDate = c.DueDate,
                reminderDaysBeforeDue = c.ReminderDaysBeforeDue,
                reminderDaysAfterDue = c.ReminderDaysAfterDue,
                description = c.Description ?? "",
                isActive = c.IsActive
            }).ToListAsync();
        return Ok(new { total = rows.Count, charges = rows });
    }
    public sealed class ChargeRequest { public int AccommodationId { get; set; } public int PaymentTypeId { get; set; } public decimal Amount { get; set; } public string? Frequency { get; set; } public DateTime? DueDate { get; set; } public int ReminderDaysBeforeDue { get; set; } = 3; public int ReminderDaysAfterDue { get; set; } = 3; public string? Description { get; set; } public bool IsActive { get; set; } = true; }
    [HttpPost("charges")]
    public async Task<IActionResult> CreateCharge([FromBody] ChargeRequest r)
    {
        if (r.AccommodationId <= 0 || r.PaymentTypeId <= 0 || r.Amount < 0) return BadRequest(new { message = "Choose an accommodation and payment type and enter a non-negative amount." });
        if (!await _db.Accommodations.AnyAsync(a => a.AccommodationID == r.AccommodationId)) return NotFound(new { message = "Accommodation not found." });
        if (!await _db.PaymentTypes.AnyAsync(p => p.PaymentTypeID == r.PaymentTypeId && p.IsActive)) return BadRequest(new { message = "Select an active payment type." });
        if (await _db.AccommodationCharges.AnyAsync(c => c.AccommodationID == r.AccommodationId && c.PaymentTypeID == r.PaymentTypeId)) return Conflict(new { message = "This charge type already exists for this accommodation. Edit the existing charge instead." });
        var c = new AccommodationCharge { AccommodationID = r.AccommodationId, PaymentTypeID = r.PaymentTypeId, Amount = r.Amount, Frequency = string.IsNullOrWhiteSpace(r.Frequency) ? "Monthly" : r.Frequency.Trim(), DueDate = r.DueDate ?? PaymentDueDateCalculator.DefaultDueDate(), ReminderDaysBeforeDue = Math.Clamp(r.ReminderDaysBeforeDue, 0, 31), ReminderDaysAfterDue = Math.Clamp(r.ReminderDaysAfterDue, 0, 31), Description = r.Description?.Trim(), IsActive = r.IsActive };
        _db.AccommodationCharges.Add(c); await _db.SaveChangesAsync(); return Ok(new { message = "Accommodation charge created.", accommodationChargeId = c.AccommodationChargeID });
    }
    [HttpPut("charges/{id:int}")]
    public async Task<IActionResult> UpdateCharge(int id, [FromBody] ChargeRequest r)
    {
        var c = await _db.AccommodationCharges.FirstOrDefaultAsync(x => x.AccommodationChargeID == id); if (c == null) return NotFound(new { message = "Charge not found." });
        if (r.Amount < 0 || r.AccommodationId <= 0 || r.PaymentTypeId <= 0) return BadRequest(new { message = "Enter a valid accommodation, payment type, and non-negative amount." });
        if (await _db.AccommodationCharges.AnyAsync(x => x.AccommodationChargeID != id && x.AccommodationID == r.AccommodationId && x.PaymentTypeID == r.PaymentTypeId)) return Conflict(new { message = "This charge type already exists for this accommodation." });
        c.AccommodationID = r.AccommodationId; c.PaymentTypeID = r.PaymentTypeId; c.Amount = r.Amount; c.Frequency = string.IsNullOrWhiteSpace(r.Frequency) ? "Monthly" : r.Frequency.Trim(); c.DueDate = r.DueDate; c.ReminderDaysBeforeDue = Math.Clamp(r.ReminderDaysBeforeDue, 0, 31); c.ReminderDaysAfterDue = Math.Clamp(r.ReminderDaysAfterDue, 0, 31); c.Description = r.Description?.Trim(); c.IsActive = r.IsActive;
        await _db.SaveChangesAsync(); return Ok(new { message = "Charge updated." });
    }
    [HttpPost("charges/{id:int}/status")]
    public async Task<IActionResult> ToggleCharge(int id)
    {
        var c = await _db.AccommodationCharges.FirstOrDefaultAsync(x => x.AccommodationChargeID == id); if (c == null) return NotFound(new { message = "Charge not found." });
        c.IsActive = !c.IsActive; await _db.SaveChangesAsync(); return Ok(new { message = $"Charge {(c.IsActive ? "activated" : "deactivated")}.", isActive = c.IsActive });
    }
    [HttpDelete("charges/{id:int}")]
    public async Task<IActionResult> DeleteCharge(int id)
    {
        var c = await _db.AccommodationCharges.FirstOrDefaultAsync(x => x.AccommodationChargeID == id); if (c == null) return NotFound(new { message = "Charge not found." });
        if (await _db.Payments.AnyAsync(p => p.AccommodationChargeID == id)) return Conflict(new { message = "This charge has payment history. Deactivate it instead." });
        _db.AccommodationCharges.Remove(c); await _db.SaveChangesAsync(); return Ok(new { message = "Charge deleted." });
    }
}
