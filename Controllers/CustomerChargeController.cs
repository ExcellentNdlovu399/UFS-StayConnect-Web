using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UFSStayConnect.Data;
using UFSStayConnect.Models;

namespace UFSStayConnect.Controllers;

[Authorize]
public class CustomerChargeController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    public CustomerChargeController(AppDbContext db, UserManager<ApplicationUser> users) { _db = db; _users = users; }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Index()
    {
        var charges = await _db.CustomerCharges.Include(c=>c.User).Include(c=>c.Accommodation)
            .Include(c=>c.PaymentType).Include(c=>c.Payments).OrderByDescending(c=>c.AssignedAt).ToListAsync();
        ViewBag.Customers = await _users.Users.OrderBy(u=>u.FirstName).ToListAsync();
        ViewBag.Applications = await _db.Applications.Include(a=>a.Accommodation).Include(a=>a.User)
            .Where(a=>a.Status=="Approved" && a.RoomID != null).OrderByDescending(a=>a.ApplicationDate).ToListAsync();
        ViewBag.PaymentTypes = await _db.PaymentTypes.OrderBy(p=>p.Name).ToListAsync();
        return View("AdminIndex", charges);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(string userId, int applicationId, int paymentTypeId, string title,
        string? description, decimal amount, DateTime dueDate, string? adminNote)
    {
        title = (title ?? "").Trim();
        var app = await _db.Applications.Include(a=>a.Accommodation).FirstOrDefaultAsync(a=>a.ApplicationID==applicationId);
        var user = await _users.FindByIdAsync(userId);
        var type = await _db.PaymentTypes.FindAsync(paymentTypeId);
        if (user == null || (user != null && !await _users.IsInRoleAsync(user, "Customer")) ||
            app == null || app.UserID != userId || app.Status != "Approved" || app.RoomID == null ||
            type == null || amount <= 0 || string.IsNullOrWhiteSpace(title) || dueDate == default)
        {
            TempData["ErrorMessage"] = "Select a valid customer with an approved accommodation and enter a valid charge.";
            return RedirectToAction(nameof(Index));
        }
        _db.CustomerCharges.Add(new CustomerCharge {
            UserID=userId, ApplicationID=app.ApplicationID, AccommodationID=app.AccommodationID,
            PaymentTypeID=paymentTypeId, Title=title, Description=description?.Trim(),
            Amount=decimal.Round(amount,2), DueDate=dueDate.Date, AdminNote=adminNote?.Trim(), AssignedAt=DateTime.UtcNow
        });
        _db.Notifications.Add(new Notification { UserID=userId, Title="New charge assigned",
            Message=$"{title}: R{amount:N2} is due {dueDate:dd MMM yyyy}.", DateCreated=DateTime.UtcNow });
        await _db.SaveChangesAsync();
        TempData["SuccessMessage"] = "Charge assigned to customer. It is now visible on web and Android.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin")]
    public async Task<IActionResult> Cancel(int id)
    {
        var c = await _db.CustomerCharges.Include(x=>x.Payments).FirstOrDefaultAsync(x=>x.CustomerChargeID==id);
        if (c == null) return NotFound();
        if (c.Payments.Any(p=>p.PaymentStatus=="Paid")) TempData["ErrorMessage"]="A charge with a confirmed payment cannot be cancelled here. Record a finance adjustment instead.";
        else { c.IsActive=false; await _db.SaveChangesAsync(); TempData["SuccessMessage"]="Charge cancelled."; }
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Customer")]
    public IActionResult Mine()
    {
        // Compatibility route: customer charges now appear inside Payment/Index.
        return RedirectToAction("Index", "Payment");
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Customer")]
    public async Task<IActionResult> Pay(int id, string paymentMethod, string? referenceNumber)
    {
        var user = await _users.GetUserAsync(User);
        if(user==null) return Challenge();
        var c = await _db.CustomerCharges.Include(x=>x.Payments).FirstOrDefaultAsync(x=>x.CustomerChargeID==id && x.UserID==user.Id && x.IsActive);
        if(c==null) return NotFound();
        var paid=c.Payments.Where(p=>p.PaymentStatus=="Paid").Sum(p=>p.Amount);
        var pending=c.Payments.Where(p=>p.PaymentStatus=="Pending").Sum(p=>p.Amount);
        var remaining=c.Amount-paid-pending;
        if(remaining<=0) { TempData["ErrorMessage"]="This charge has already been paid or is awaiting verification."; return RedirectToAction(nameof(Mine)); }
        paymentMethod=(paymentMethod??"EFT").Trim();
        if(paymentMethod!="EFT" && paymentMethod!="Card") { TempData["ErrorMessage"]="Choose EFT or Card."; return RedirectToAction(nameof(Mine)); }
        // This project records card/EFT submissions for officer verification. No raw card or CVV data is persisted.
        var reference=(referenceNumber??"").Trim();
        if(string.IsNullOrWhiteSpace(reference))
            reference="SC"+Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        if(reference.Length>100) reference=reference[..100];
        _db.Payments.Add(new Payment { UserID=user.Id, PaymentTypeID=c.PaymentTypeID, ApplicationID=c.ApplicationID,
            CustomerChargeID=c.CustomerChargeID, Amount=remaining, PaymentStatus="Pending", PaymentMethod=paymentMethod,
            ReferenceNumber=reference, PaymentDate=DateTime.UtcNow, DueDate=c.DueDate,
            Description=$"Customer charge #{c.CustomerChargeID}: {c.Title}" });
        _db.Notifications.Add(new Notification { UserID=user.Id, Title="Payment submitted",
            Message=$"Your payment submission for {c.Title} (R{remaining:N2}) is awaiting Financial Officer verification.", DateCreated=DateTime.UtcNow });
        await _db.SaveChangesAsync();
        TempData["SuccessMessage"]="Payment submitted and is awaiting Financial Officer verification.";
        return RedirectToAction(nameof(Mine));
    }
}
