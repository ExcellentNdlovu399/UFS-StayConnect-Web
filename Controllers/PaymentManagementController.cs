using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UFSStayConnect.DataAccess;
using UFSStayConnect.Models;
using UFSStayConnect.ViewModels;

namespace UFSStayConnect.Controllers
{
    [Authorize(Roles = "Admin,FinancialOfficer")]
    public class PaymentManagementController : Controller
    {
        private readonly IRepositoryWrapper _repository;
        private readonly ILogger<PaymentManagementController> _logger;

        public PaymentManagementController(IRepositoryWrapper repository, ILogger<PaymentManagementController> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Index(string? status)
        {
            var payments = _repository.Payment.FindAllPayments().AsEnumerable();
            if (!string.IsNullOrWhiteSpace(status)) payments = payments.Where(p => p.PaymentStatus.Equals(status, StringComparison.OrdinalIgnoreCase));
            var model = payments.Select(p => new AdminPaymentViewModel
            {
                PaymentID = p.PaymentID,
                CustomerName = p.User == null ? "Unknown" : $"{p.User.FirstName} {p.User.Surname}".Trim(),
                StudentNumber = p.User?.StudentNumber,
                PaymentType = p.PaymentType?.Name ?? "Unknown",
                Amount = p.Amount,
                Status = p.PaymentStatus,
                ReferenceNumber = p.ReferenceNumber,
                PaymentMethod = p.PaymentMethod,
                PaymentDate = p.PaymentDate,
                DueDate = p.DueDate,
                AccommodationName = p.Application?.Accommodation?.Name
            }).ToList();
            ViewBag.Status = status;
            ViewBag.Pending = _repository.Payment.FindAllPayments().Count(p => p.PaymentStatus == "Pending");
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Approve(int id)
        {
            var payment = _repository.Payment.GetPaymentById(id);
            if (payment == null) return NotFound();
            if (payment.PaymentStatus != "Pending")
            {
                TempData["ErrorMessage"] = "Only pending payments can be approved.";
                return RedirectToAction(nameof(Index));
            }

            payment.PaymentStatus = "Paid";
            _repository.Payment.UpdatePayment(payment);
            _repository.Notification.CreateNotification(new Notification
            {
                UserID = payment.UserID,
                Title = "Payment Approved",
                Message = $"Your {payment.PaymentType?.Name ?? "payment"} payment of R{payment.Amount:N2} has been approved.",
                DateCreated = DateTime.UtcNow
            });
            _repository.Save();
            TempData["SuccessMessage"] = "Payment approved.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Reject(PaymentRejectionViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Select a rejection reason before rejecting the payment.";
                return RedirectToAction(nameof(Index));
            }

            var payment = _repository.Payment.GetPaymentById(model.PaymentID);
            if (payment == null) return NotFound();
            if (payment.PaymentStatus != "Pending")
            {
                TempData["ErrorMessage"] = "Only pending payments can be rejected.";
                return RedirectToAction(nameof(Index));
            }

            var reason = model.Reason.Trim();
            if (!string.IsNullOrWhiteSpace(model.Explanation)) reason += $" - {model.Explanation.Trim()}";
            if (reason.Length > 500) reason = reason[..500];

            payment.PaymentStatus = "Rejected";
            payment.Description = $"Rejected: {reason}";
            _repository.Payment.UpdatePayment(payment);
            _repository.Notification.CreateNotification(new Notification
            {
                UserID = payment.UserID,
                Title = "Payment Rejected",
                Message = $"Your payment of R{payment.Amount:N2} was rejected. Reason: {reason}",
                DateCreated = DateTime.UtcNow
            });
            _repository.Save();
            TempData["SuccessMessage"] = "Payment rejected.";
            return RedirectToAction(nameof(Index));
        }
    }
}
