using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using UFSStayConnect.Data;
using UFSStayConnect.Models;
using UFSStayConnect.ViewModels;

namespace UFSStayConnect.Controllers;

[Authorize(Roles = "Admin,FinancialOfficer")]
public class ServiceUsageController : Controller
{
    private readonly AppDbContext _context;
    public ServiceUsageController(AppDbContext context) => _context = context;

    [HttpGet]
    public async Task<IActionResult> Create(int applicationId)
    {
        var application = await _context.Applications.Include(a => a.Accommodation)
            .FirstOrDefaultAsync(a => a.ApplicationID == applicationId);
        if (application == null) return NotFound();
        await PopulatePageAsync(application);
        return View(new RecordServiceUsageViewModel { ApplicationID = applicationId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RecordServiceUsageViewModel model)
    {
        var application = await _context.Applications.Include(a => a.Accommodation)
            .FirstOrDefaultAsync(a => a.ApplicationID == model.ApplicationID);
        if (application == null) return NotFound();

        var assignment = await _context.AccommodationServices.Include(x => x.Service)
            .FirstOrDefaultAsync(x => x.AccommodationID == application.AccommodationID
                && x.ServiceID == model.ServiceID && x.IsEnabled && x.IsPayAsYouGo
                && x.Service != null && x.Service.IsActive);

        if (assignment == null)
            ModelState.AddModelError(nameof(model.ServiceID), "This pay-as-you-go service is not enabled for the selected accommodation.");
        if (model.CurrentReading < model.PreviousReading)
            ModelState.AddModelError(nameof(model.CurrentReading), "Current reading cannot be less than the previous reading.");
        if (model.BillingDueDate.Date < DateTime.Today)
            ModelState.AddModelError(nameof(model.BillingDueDate), "Billing due date cannot be in the past.");

        if (!ModelState.IsValid)
        {
            await PopulatePageAsync(application);
            return View(model);
        }

        var unitsUsed = model.CurrentReading - model.PreviousReading;
        var rate = assignment!.RatePerUnit;
        var amount = Math.Round(unitsUsed * rate, 2, MidpointRounding.AwayFromZero);
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Forbid();

        _context.ServiceUsageRecords.Add(new ServiceUsageRecord
        {
            ApplicationID = model.ApplicationID,
            ServiceID = model.ServiceID,
            ReadingDate = DateTime.Today,
            PreviousReading = model.PreviousReading,
            CurrentReading = model.CurrentReading,
            UnitsUsed = unitsUsed,
            RatePerUnit = rate,
            AmountDue = amount,
            RecordedByUserID = userId,
            BillingStatus = "Unbilled",
            BillingDueDate = model.BillingDueDate,
            Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim()
        });
        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Usage recorded: {unitsUsed:0.###} {assignment.UnitOfMeasure} × R{rate:0.00} = R{amount:0.00}. The charge is saved as unbilled.";
        return RedirectToAction("Manage", "AccommodationCharge");
    }

    private async Task PopulatePageAsync(Application application)
    {
        ViewBag.ApplicationLabel = $"Application #{application.ApplicationID} — {application.Accommodation?.Name ?? "Accommodation"}";
        var assignments = await _context.AccommodationServices.Include(x => x.Service)
            .Where(x => x.AccommodationID == application.AccommodationID && x.IsEnabled && x.IsPayAsYouGo)
            .ToListAsync();
        ViewBag.Services = assignments.Where(x => x.Service != null && x.Service.IsActive)
            .Select(x => new SelectListItem
            {
                Value = x.ServiceID.ToString(),
                Text = $"{x.Service!.ServiceName} (R{x.RatePerUnit:0.00}/{x.UnitOfMeasure})"
            }).ToList();
    }
}
