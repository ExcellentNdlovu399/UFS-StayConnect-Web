using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UFSStayConnect.Data;
using UFSStayConnect.Models;

namespace UFSStayConnect.Controllers.Api;

[ApiController]
[Route("api/staff/service-usage")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin,FinancialOfficer")]
public class StaffServiceUsageApiController : ControllerBase
{
    private readonly AppDbContext _db;
    public StaffServiceUsageApiController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int? applicationId)
    {
        var q = _db.ServiceUsageRecords.AsNoTracking().Include(x => x.Application).ThenInclude(a => a!.User)
            .Include(x => x.Application).ThenInclude(a => a!.Accommodation).Include(x => x.Service).AsQueryable();
        if (applicationId.HasValue) q = q.Where(x => x.ApplicationID == applicationId.Value);
        var records = await q.OrderByDescending(x => x.ReadingDate).Take(500).Select(x => new
        {
            serviceUsageRecordId = x.ServiceUsageRecordID, applicationId = x.ApplicationID,
            customer = x.Application == null || x.Application.User == null ? "Unknown" : x.Application.User.FirstName + " " + x.Application.User.Surname,
            accommodation = x.Application == null || x.Application.Accommodation == null ? "Residence" : x.Application.Accommodation.Name,
            serviceId = x.ServiceID, service = x.Service == null ? "Service" : x.Service.ServiceName,
            readingDate = x.ReadingDate, previousReading = x.PreviousReading, currentReading = x.CurrentReading,
            unitsUsed = x.UnitsUsed, ratePerUnit = x.RatePerUnit, amountDue = x.AmountDue,
            billingStatus = x.BillingStatus, billingDueDate = x.BillingDueDate, notes = x.Notes ?? ""
        }).ToListAsync();
        var applications = await _db.Applications.AsNoTracking().Include(a => a.User).Include(a => a.Accommodation)
            .Where(a => a.Status == "Approved").OrderBy(a => a.Accommodation!.Name).Select(a => new
            {
                applicationId = a.ApplicationID, customer = a.User == null ? "Unknown" : a.User.FirstName + " " + a.User.Surname,
                accommodation = a.Accommodation == null ? "Residence" : a.Accommodation.Name
            }).ToListAsync();
        return Ok(new { total = records.Count, records, approvedApplications = applications });
    }

    [HttpGet("services")]
    public async Task<IActionResult> GetServices([FromQuery] int applicationId)
    {
        var application = await _db.Applications.AsNoTracking().FirstOrDefaultAsync(a => a.ApplicationID == applicationId && a.Status == "Approved");
        if (application == null) return BadRequest(new { message = "Choose an approved application." });
        var rows = await _db.AccommodationServices.AsNoTracking().Include(x => x.Service)
            .Where(x => x.AccommodationID == application.AccommodationID && x.IsEnabled && x.IsPayAsYouGo && x.Service != null && x.Service.IsActive)
            .OrderBy(x => x.Service!.ServiceName).Select(x => new { serviceId = x.ServiceID, serviceName = x.Service!.ServiceName,
                ratePerUnit = x.RatePerUnit, unitOfMeasure = x.UnitOfMeasure, freeUnits = x.FreeUnits }).ToListAsync();
        return Ok(new { services = rows });
    }

    public sealed class UsageRequest
    {
        public int ApplicationId { get; set; } public int ServiceId { get; set; }
        public decimal PreviousReading { get; set; } public decimal CurrentReading { get; set; }
        public DateTime BillingDueDate { get; set; } public string? Notes { get; set; }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UsageRequest request)
    {
        if (request.ApplicationId <= 0 || request.ServiceId <= 0 || request.PreviousReading < 0 || request.CurrentReading < request.PreviousReading)
            return BadRequest(new { message = "Select an approved application/service and enter valid meter readings." });
        if (request.BillingDueDate.Date < DateTime.Today) return BadRequest(new { message = "Billing due date cannot be in the past." });
        var application = await _db.Applications.Include(a => a.Accommodation).FirstOrDefaultAsync(a => a.ApplicationID == request.ApplicationId && a.Status == "Approved");
        if (application == null) return BadRequest(new { message = "The selected application is not approved." });
        var assignment = await _db.AccommodationServices.Include(x => x.Service).FirstOrDefaultAsync(x => x.AccommodationID == application.AccommodationID && x.ServiceID == request.ServiceId && x.IsEnabled && x.IsPayAsYouGo && x.Service != null && x.Service.IsActive);
        if (assignment == null) return BadRequest(new { message = "This metered service is not enabled for the selected accommodation." });
        var staffId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(staffId)) return Unauthorized();
        var units = request.CurrentReading - request.PreviousReading;
        var amount = Math.Round(units * assignment.RatePerUnit, 2, MidpointRounding.AwayFromZero);
        var record = new ServiceUsageRecord { ApplicationID = request.ApplicationId, ServiceID = request.ServiceId, ReadingDate = DateTime.Today,
            PreviousReading = request.PreviousReading, CurrentReading = request.CurrentReading, UnitsUsed = units, RatePerUnit = assignment.RatePerUnit,
            AmountDue = amount, RecordedByUserID = staffId, BillingStatus = "Unbilled", BillingDueDate = request.BillingDueDate,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim() };
        _db.ServiceUsageRecords.Add(record); await _db.SaveChangesAsync();
        return Ok(new { message = $"Usage recorded: {units:0.###} {assignment.UnitOfMeasure} × R{assignment.RatePerUnit:0.00} = R{amount:0.00}. Saved as unbilled.", serviceUsageRecordId = record.ServiceUsageRecordID, unitsUsed = units, amountDue = amount });
    }
}
