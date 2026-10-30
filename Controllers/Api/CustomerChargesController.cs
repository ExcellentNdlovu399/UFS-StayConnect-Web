using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UFSStayConnect.Data;
using UFSStayConnect.Models;

namespace UFSStayConnect.Controllers.Api;

[ApiController]
[Route("api/customer-charges")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class CustomerChargesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    public CustomerChargesController(AppDbContext db, UserManager<ApplicationUser> users) { _db=db; _users=users; }

    [HttpGet("mine"), Authorize(Roles="Customer")]
    public async Task<IActionResult> Mine()
    {
        var uid=User.FindFirstValue(ClaimTypes.NameIdentifier);
        if(string.IsNullOrWhiteSpace(uid)) return Unauthorized();
        var rows=await _db.CustomerCharges.AsNoTracking().Where(c=>c.UserID==uid && c.IsActive)
            .Include(c=>c.Accommodation).Include(c=>c.PaymentType).Include(c=>c.Payments)
            .OrderBy(c=>c.DueDate).ToListAsync();
        return Ok(rows.Select(c=>{
            var paid=c.Payments.Where(p=>p.PaymentStatus=="Paid").Sum(p=>p.Amount);
            var pending=c.Payments.Where(p=>p.PaymentStatus=="Pending").Sum(p=>p.Amount);
            return new { id=c.CustomerChargeID, title=c.Title, description=c.Description,
                accommodationId=c.AccommodationID, accommodationName=c.Accommodation?.Name,
                paymentType=c.PaymentType?.Name, amount=c.Amount, amountPaid=paid,
                pendingVerification=pending, balance=Math.Max(0,c.Amount-paid-pending), dueDate=c.DueDate,
                assignedAt=c.AssignedAt, status=paid>=c.Amount?"Paid":pending>0?"Pending verification":c.DueDate.Date<DateTime.UtcNow.Date?"Overdue":"Unpaid",
                canPay=paid+pending<c.Amount };
        }));
    }

    public sealed class SubmitRequest { public string PaymentMethod {get;set;}="EFT"; public string? ReferenceNumber {get;set;} }

    [HttpPost("{id:int}/pay"), Authorize(Roles="Customer")]
    public async Task<IActionResult> Pay(int id,[FromBody] SubmitRequest req)
    {
        var uid=User.FindFirstValue(ClaimTypes.NameIdentifier);
        var c=await _db.CustomerCharges.Include(x=>x.Payments).FirstOrDefaultAsync(x=>x.CustomerChargeID==id && x.UserID==uid && x.IsActive);
        if(c==null) return NotFound(new {message="Charge not found."});
        var paid=c.Payments.Where(p=>p.PaymentStatus=="Paid").Sum(p=>p.Amount);
        var pending=c.Payments.Where(p=>p.PaymentStatus=="Pending").Sum(p=>p.Amount);
        var amount=c.Amount-paid-pending;
        if(amount<=0) return Conflict(new {message="This charge is already paid or awaiting verification."});
        var method=(req.PaymentMethod??"").Trim();
        if(method!="EFT" && method!="Card") return BadRequest(new {message="Choose EFT or Card."});
        var reference=(req.ReferenceNumber??"").Trim();
        if(string.IsNullOrWhiteSpace(reference)) reference="SC"+Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        if(reference.Length>100) return BadRequest(new {message="Reference number is too long."});
        _db.Payments.Add(new Payment {UserID=uid!, PaymentTypeID=c.PaymentTypeID, ApplicationID=c.ApplicationID,
            CustomerChargeID=c.CustomerChargeID, Amount=amount, PaymentStatus="Pending", PaymentMethod=method,
            ReferenceNumber=reference, PaymentDate=DateTime.UtcNow, DueDate=c.DueDate,
            Description=$"Customer charge #{c.CustomerChargeID}: {c.Title}"});
        _db.Notifications.Add(new Notification {UserID=uid!, Title="Payment submitted",
            Message=$"Payment for {c.Title} (R{amount:N2}) is awaiting verification.", DateCreated=DateTime.UtcNow});
        await _db.SaveChangesAsync();
        return Ok(new {message="Payment submitted for verification.", referenceNumber=reference, amount});
    }

    [HttpGet("admin/options"), Authorize(Roles="Admin")]
    public async Task<IActionResult> AdminOptions()
    {
        var customers=await _users.Users.OrderBy(u=>u.FirstName).Select(u=>new {id=u.Id,name=(u.FirstName+" "+u.Surname).Trim(),email=u.Email}).ToListAsync();
        var applications=await _db.Applications.AsNoTracking().Where(a=>a.Status=="Approved" && a.RoomID!=null)
            .Include(a=>a.User).Include(a=>a.Accommodation).OrderBy(a=>a.User!.FirstName)
            .Select(a=>new {id=a.ApplicationID,userId=a.UserID,customerName=(a.User!.FirstName+" "+a.User.Surname).Trim(),accommodationName=a.Accommodation!.Name})
            .ToListAsync();
        var types=await _db.PaymentTypes.AsNoTracking().OrderBy(t=>t.Name).Select(t=>new {id=t.PaymentTypeID,name=t.Name}).ToListAsync();
        return Ok(new {customers,applications,paymentTypes=types});
    }

    [HttpGet("admin"), Authorize(Roles="Admin")]
    public async Task<IActionResult> AdminList()
    {
        var rows=await _db.CustomerCharges.AsNoTracking().Include(c=>c.User).Include(c=>c.Accommodation)
            .Include(c=>c.PaymentType).Include(c=>c.Payments).OrderByDescending(c=>c.AssignedAt).ToListAsync();
        return Ok(rows.Select(c=>new {id=c.CustomerChargeID, customerId=c.UserID,
            customerName=(c.User!.FirstName+" "+c.User.Surname).Trim(), title=c.Title,
            accommodation=c.Accommodation!.Name, amount=c.Amount, dueDate=c.DueDate, active=c.IsActive,
            paid=c.Payments.Where(p=>p.PaymentStatus=="Paid").Sum(p=>p.Amount),
            pending=c.Payments.Where(p=>p.PaymentStatus=="Pending").Sum(p=>p.Amount)}));
    }

    public sealed class CreateRequest {
        public string UserId {get;set;}=""; public int ApplicationId {get;set;}
        public int PaymentTypeId {get;set;} public string Title {get;set;}="";
        public string? Description {get;set;} public decimal Amount {get;set;}
        public DateTime DueDate {get;set;} public string? AdminNote {get;set;}
    }

    [HttpPost, Authorize(Roles="Admin")]
    public async Task<IActionResult> Create([FromBody] CreateRequest r)
    {
        var app=await _db.Applications.Include(a=>a.Accommodation).FirstOrDefaultAsync(a=>a.ApplicationID==r.ApplicationId);
        var type=await _db.PaymentTypes.FindAsync(r.PaymentTypeId);
        var user=await _users.FindByIdAsync(r.UserId);
        if(user==null || (user!=null && !await _users.IsInRoleAsync(user, "Customer")) ||
           app==null || app.UserID!=r.UserId || app.Status!="Approved" || app.RoomID==null ||
           type==null || r.Amount<=0 || string.IsNullOrWhiteSpace(r.Title) || r.DueDate==default)
            return BadRequest(new {message="Choose a customer with an approved accommodation and provide valid charge details."});
        var c=new CustomerCharge {UserID=r.UserId, ApplicationID=app.ApplicationID, AccommodationID=app.AccommodationID,
            PaymentTypeID=r.PaymentTypeId, Title=r.Title.Trim(), Description=r.Description?.Trim(),
            Amount=decimal.Round(r.Amount,2), DueDate=r.DueDate.Date, AdminNote=r.AdminNote?.Trim()};
        _db.CustomerCharges.Add(c);
        _db.Notifications.Add(new Notification {UserID=r.UserId, Title="New charge assigned",
            Message=$"{c.Title}: R{c.Amount:N2} is due {c.DueDate:dd MMM yyyy}.", DateCreated=DateTime.UtcNow});
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(AdminList), new {id=c.CustomerChargeID}, new {id=c.CustomerChargeID,message="Charge assigned."});
    }
}
