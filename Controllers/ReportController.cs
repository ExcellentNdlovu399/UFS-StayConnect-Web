using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UFSStayConnect.Data;
using UFSStayConnect.Models;

namespace UFSStayConnect.Controllers
{
    [Authorize(Roles = "Admin,FinancialOfficer")]
    public class ReportController : Controller
    {
        private readonly AppDbContext _context;

        public ReportController(AppDbContext context) => _context = context;

        [HttpGet]
        public async Task<IActionResult> Index(string? status, int? paymentTypeId, DateTime? from, DateTime? to)
        {
            var paymentsQuery = _context.Payments
                .Include(p => p.User)
                .Include(p => p.PaymentType)
                .Include(p => p.Application)
                .ThenInclude(a => a!.Accommodation)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status)) paymentsQuery = paymentsQuery.Where(p => p.PaymentStatus == status);
            if (paymentTypeId.HasValue) paymentsQuery = paymentsQuery.Where(p => p.PaymentTypeID == paymentTypeId.Value);
            if (from.HasValue) paymentsQuery = paymentsQuery.Where(p => p.PaymentDate >= from.Value.Date);
            if (to.HasValue) paymentsQuery = paymentsQuery.Where(p => p.PaymentDate < to.Value.Date.AddDays(1));

            var payments = await paymentsQuery.OrderByDescending(p => p.PaymentDate).Take(500).ToListAsync();
            var customers = await _context.ApplicationUsers.CountAsync();
            var approved = await _context.Applications.CountAsync(a => a.Status == "Approved");
            var pendingApplications = await _context.Applications.CountAsync(a => a.Status == "Pending");
            var cancelled = await _context.Applications.CountAsync(a => a.Status == "Cancelled" || a.Status == "Withdrawn");

            ViewBag.Accommodations = await _context.Accommodations.CountAsync();
            ViewBag.AvailableRooms = await _context.Accommodations.Where(a => a.Available).SumAsync(a => (int?)a.AvailableRooms) ?? 0;
            ViewBag.Customers = customers;
            ViewBag.ApprovedApplications = approved;
            ViewBag.PendingApplications = pendingApplications;
            ViewBag.CancelledApplications = cancelled;
            ViewBag.Paid = payments.Where(p => p.PaymentStatus == "Paid").Sum(p => p.Amount);
            ViewBag.Pending = payments.Where(p => p.PaymentStatus == "Pending").Sum(p => p.Amount);
            ViewBag.Rejected = payments.Where(p => p.PaymentStatus == "Rejected").Sum(p => p.Amount);
            ViewBag.PaymentTypes = await _context.PaymentTypes.OrderBy(p => p.Name).ToListAsync();
            ViewBag.Status = status;
            ViewBag.PaymentTypeId = paymentTypeId;
            ViewBag.From = from?.ToString("yyyy-MM-dd");
            ViewBag.To = to?.ToString("yyyy-MM-dd");
            ViewBag.Rows = payments;
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> ExportCsv(string? status, int? paymentTypeId, DateTime? from, DateTime? to)
        {
            var query = _context.Payments.Include(p => p.User).Include(p => p.PaymentType).Include(p => p.Application).ThenInclude(a => a!.Accommodation).AsQueryable();
            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(p => p.PaymentStatus == status);
            if (paymentTypeId.HasValue) query = query.Where(p => p.PaymentTypeID == paymentTypeId.Value);
            if (from.HasValue) query = query.Where(p => p.PaymentDate >= from.Value.Date);
            if (to.HasValue) query = query.Where(p => p.PaymentDate < to.Value.Date.AddDays(1));
            var payments = await query.OrderByDescending(p => p.PaymentDate).ToListAsync();
            var csv = new StringBuilder();
            csv.AppendLine("Reference,Customer,Accommodation,Payment Type,Amount,Method,Status,Payment Date,Due Date");
            foreach (var p in payments)
            {
                static string Q(string? value) => "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
                csv.AppendLine(string.Join(",", Q(p.ReferenceNumber), Q(p.User == null ? "Unknown" : $"{p.User.FirstName} {p.User.Surname}"), Q(p.Application?.Accommodation?.Name), Q(p.PaymentType?.Name), p.Amount.ToString("0.00"), Q(p.PaymentMethod), Q(p.PaymentStatus), p.PaymentDate.ToString("yyyy-MM-dd HH:mm"), p.DueDate?.ToString("yyyy-MM-dd") ?? string.Empty));
            }
            return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"UFSStayConnect-PaymentReport-{DateTime.Now:yyyyMMddHHmm}.csv");
        }
    }
}
