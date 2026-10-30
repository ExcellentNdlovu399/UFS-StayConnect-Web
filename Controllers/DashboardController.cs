using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using UFSStayConnect.DataAccess;
using UFSStayConnect.Models;
using UFSStayConnect.ViewModels;
using UFSStayConnect.Services;

namespace UFSStayConnect.Controllers
{
    [Authorize(Roles = "Customer")]
    public class DashboardController : Controller
    {
        private readonly IRepositoryWrapper _repository;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(IRepositoryWrapper repository, UserManager<ApplicationUser> userManager, ILogger<DashboardController> logger)
        {
            _repository = repository;
            _userManager = userManager;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return RedirectToAction("Login", "Account");

                var userId = user.Id;
                var applications = _repository.Application.FindByUser(userId).ToList();
                var approvedApplications = applications
                    .Where(a => string.Equals(a.Status, "Approved", StringComparison.OrdinalIgnoreCase) && a.RoomID.HasValue)
                    .OrderByDescending(a => a.ApplicationDate).ToList();
                var latestApproved = approvedApplications.FirstOrDefault();
                var payments = _repository.Payment.FindByUser(userId).ToList();

                var model = new CustomerDashboardViewModel
                {
                    FirstName = user.FirstName,
                    Surname = user.Surname,
                    UserType = user.UserType,
                    StudentNumber = user.StudentNumber,
                    EmployeeNumber = user.EmployeeNumber,
                    TotalApplications = applications.Count,
                    PendingApplications = applications.Count(a => string.Equals(a.Status, "Pending", StringComparison.OrdinalIgnoreCase)),
                    ApprovedApplications = approvedApplications.Count,
                    RejectedApplications = applications.Count(a => string.Equals(a.Status, "Rejected", StringComparison.OrdinalIgnoreCase)),
                    AccommodationStatus = approvedApplications.Count > 0 ? $"{approvedApplications.Count} approved accommodation(s)" : applications.Any(a => string.Equals(a.Status, "Pending", StringComparison.OrdinalIgnoreCase)) ? "Application Pending" : "No Accommodation",
                    AccommodationName = latestApproved?.Accommodation?.Name,
                    AccommodationID = latestApproved?.AccommodationID,
                    AvailableRooms = latestApproved?.Accommodation?.AvailableRooms,
                    RoomNumber = latestApproved?.Room?.RoomNumber,
                    OutstandingBalance = 0m,
                    RecentPaymentAmount = payments.Where(p => p.PaymentStatus == "Paid").OrderByDescending(p => p.PaymentDate).Select(p => (decimal?)p.Amount).FirstOrDefault() ?? 0m,
                    UnreadNotifications = _repository.Notification.FindByUser(userId).Count(n => !n.IsRead),
                    ApprovedAccommodationItems = approvedApplications.Select(a => new CustomerApprovedAccommodationViewModel
                    {
                        ApplicationID = a.ApplicationID,
                        AccommodationID = a.AccommodationID,
                        AccommodationName = a.Accommodation?.Name ?? $"Accommodation #{a.AccommodationID}",
                        RoomNumber = a.Room?.RoomNumber,
                        AvailableRooms = a.Accommodation?.AvailableRooms,
                        Status = a.Status
                    }).ToList()
                };

                var today = DateTime.UtcNow.Date;
                foreach (var application in approvedApplications)
                {
                    var charges = _repository.AccommodationCharge.FindByAccommodation(application.AccommodationID)
                        .Where(c => c.IsActive).ToList();
                    var accommodation = _repository.Accommodation.GetAccommodationById(application.AccommodationID);
                    if (accommodation == null) continue;
                    var parkingFee = ParkingFeeCalculator.ForApprovedApplication(application, accommodation, charges);
                    var applicationPayments = payments.Where(p => p.ApplicationID == application.ApplicationID).ToList();
                    foreach (var charge in charges.Where(c => !ParkingFeeCalculator.IsParkingCharge(c.PaymentType?.Name)))
                    {
                        var due = PaymentDueDateCalculator.GetCurrentCycleDueDate(charge, today);
                        var latestPayment = applicationPayments.Where(p => p.AccommodationChargeID == charge.AccommodationChargeID && p.DueDate.HasValue && p.DueDate.Value.Date == due.Date).OrderByDescending(p => p.PaymentDate).FirstOrDefault();
                        var status = latestPayment?.PaymentStatus ?? (due < today ? "Overdue" : "Unpaid");
                        var lineParking = ParkingFeeCalculator.IsRentCharge(charge.PaymentType?.Name) ? parkingFee : 0m;
                        var amount = charge.Amount + lineParking;
                        if (string.Equals(status, "Paid", StringComparison.OrdinalIgnoreCase)) continue;
                        model.OutstandingBalance += amount;
                        if (string.Equals(status, "Overdue", StringComparison.OrdinalIgnoreCase)) model.OverdueBalance += amount;
                        else if ((due - today).Days <= 7) model.DueSoonBalance += amount;
                        model.PaymentItems.Add(new DashboardPaymentItemViewModel { PaymentTypeName = charge.PaymentType?.Name ?? "Charge", Amount = amount, DueDate = due, Status = status });
                    }
                }
                model.PaymentItems = model.PaymentItems.OrderBy(x => x.DueDate).Take(5).ToList();
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading customer dashboard.");
                TempData["ErrorMessage"] = "Unable to load your dashboard.";
                return RedirectToAction("Index", "Home");
            }
        }
    }
}
