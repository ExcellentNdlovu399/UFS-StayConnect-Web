using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UFSStayConnect.Data;
using UFSStayConnect.Models;
using UFSStayConnect.ViewModels;
using UFSStayConnect.Services;

namespace UFSStayConnect.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminController(
            AppDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            // =========================================
            // USER STATISTICS
            // =========================================

            var users = await _userManager.Users.ToListAsync();

            var totalUsers = users.Count;

            var totalStudents = users.Count(u =>
                u.UserType == "Student");

            var totalStaff = users.Count(u =>
                u.UserType == "Staff");

            var financialOfficers = 0;

            foreach (var user in users)
            {
                if (await _userManager.IsInRoleAsync(user, "FinancialOfficer"))
                {
                    financialOfficers++;
                }
            }


            // =========================================
            // ACCOMMODATION / ROOM STATISTICS
            // =========================================

            var accommodations = await _context.Accommodations
                .Include(a => a.Rooms)
                .ToListAsync();

            var totalRooms = accommodations.Sum(a =>
                a.TotalRooms > 0
                    ? a.TotalRooms
                    : a.Rooms.Count);

            var availableRooms = accommodations.Sum(a =>
                a.AvailableRooms);

            // Make sure occupied rooms can never become negative.
            var occupiedRooms = Math.Max(
                0,
                totalRooms - availableRooms);


            // =========================================
            // APPLICATION STATISTICS
            // =========================================

            var totalApplications =
                await _context.Applications.CountAsync();

            var pendingApplications =
                await _context.Applications
                    .CountAsync(a => a.Status == "Pending");


            // =========================================
            // PAYMENT STATISTICS
            // =========================================

            var totalPayments =
                await _context.Payments
                    .Where(p => p.PaymentStatus == "Paid")
                    .SumAsync(p => (decimal?)p.Amount) ?? 0;

            var outstandingPayments =
                await _context.Payments
                    .Where(p => p.PaymentStatus == "Pending")
                    .SumAsync(p => (decimal?)p.Amount) ?? 0;


            // =========================================
            // RECENT APPLICATIONS
            // =========================================

            var recentApplications =
                await _context.Applications
                    .Include(a => a.User)
                    .Include(a => a.Accommodation)
                    .OrderByDescending(a => a.ApplicationDate)
                    .Take(5)
                    .Select(a => new AdminApplicationItemViewModel
                    {
                        ApplicationID = a.ApplicationID,

                        ApplicantName =
                            a.User != null
                                ? a.User.FirstName + " " + a.User.Surname
                                : "Unknown",

                        AccommodationName =
                            a.Accommodation != null
                                ? a.Accommodation.Name
                                : "Unknown",

                        Status = a.Status,

                        ApplicationDate = a.ApplicationDate
                    })
                    .ToListAsync();


            // =========================================
            // RECENT PAYMENTS
            // =========================================

            var recentPayments =
                await _context.Payments
                    .Include(p => p.User)
                    .Include(p => p.PaymentType)
                    .Include(p => p.Application)
                        .ThenInclude(a => a!.Accommodation)
                    .OrderByDescending(p => p.PaymentDate)
                    .Take(5)
                    .Select(p => new AdminPaymentItemViewModel
                    {
                        PaymentID = p.PaymentID,

                        CustomerName =
                            p.User == null
                                ? "Unknown"
                                : $"{p.User.FirstName} {p.User.Surname}".Trim(),

                        PaymentType =
                            p.PaymentType != null
                                ? p.PaymentType.Name
                                : "Unknown",

                        Amount = p.Amount,

                        Status = p.PaymentStatus,

                        PaymentDate = p.PaymentDate
                    })
                    .ToListAsync();


            // =========================================
            // BUILD DASHBOARD MODEL
            // =========================================

            var model = new AdminDashboardViewModel
            {
                // User statistics
                TotalUsers = totalUsers,
                TotalStudents = totalStudents,
                TotalStaff = totalStaff,
                TotalFinancialOfficers = financialOfficers,

                // Accommodation statistics
                TotalRooms = totalRooms,
                OccupiedRooms = occupiedRooms,
                AvailableAccommodation = availableRooms,

                // Application statistics
                TotalApplications = totalApplications,
                PendingApplications = pendingApplications,

                // Payment statistics
                TotalPayments = totalPayments,
                OutstandingPayments = outstandingPayments,

                // Recent activity
                RecentApplications = recentApplications,
                RecentPayments = recentPayments
            };

            return View(model);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleUserStatus(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest();
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();
            if (await _userManager.IsInRoleAsync(user, "Admin"))
            {
                TempData["ErrorMessage"] = "Administrator accounts cannot be deactivated here.";
                return RedirectToAction(nameof(Users));
            }
            user.AccountStatus = user.AccountStatus == "Active" ? "Inactive" : "Active";
            await _userManager.UpdateAsync(user);
            TempData["SuccessMessage"] = user.AccountStatus == "Active" ? "Account activated." : "Account deactivated.";
            return RedirectToAction(nameof(Users));
        }


        [HttpGet]
        public IActionResult CreateCustomer() => View("~/Views/FinancialOfficer/CreateCustomer.cshtml", new CustomerAccountViewModel());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCustomer(CustomerAccountViewModel model)
        {
            ValidateCustomerModel(model, requirePassword: true);
            if (!ModelState.IsValid) return View("~/Views/FinancialOfficer/CreateCustomer.cshtml", model);

            var email = model.Email.Trim().ToLowerInvariant();
            if (!SeedIdentityData.IsAllowedEmail(email))
            {
                ModelState.AddModelError(nameof(model.Email), "Only approved UFS email domains are allowed.");
                return View("~/Views/FinancialOfficer/CreateCustomer.cshtml", model);
            }
            if (await _userManager.FindByEmailAsync(email) != null)
            {
                ModelState.AddModelError(nameof(model.Email), "That email address is already registered.");
                return View("~/Views/FinancialOfficer/CreateCustomer.cshtml", model);
            }
            var number = model.UserType == "Student" ? model.StudentNumber?.Trim() : model.EmployeeNumber?.Trim();
            if (!string.IsNullOrWhiteSpace(number) && await _userManager.Users.AnyAsync(u => u.StudentNumber == number || u.EmployeeNumber == number))
            {
                ModelState.AddModelError(model.UserType == "Student" ? nameof(model.StudentNumber) : nameof(model.EmployeeNumber), "That number is already registered.");
                return View("~/Views/FinancialOfficer/CreateCustomer.cshtml", model);
            }
            var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, FirstName = model.FirstName.Trim(), Surname = model.Surname.Trim(), UserType = model.UserType, StudentNumber = model.UserType == "Student" ? number : null, EmployeeNumber = model.UserType == "Staff" ? number : null, PhoneNumber = model.PhoneNumber?.Trim(), AccountStatus = model.AccountStatus == "Inactive" ? "Inactive" : "Active", DateCreated = DateTime.UtcNow };
            var result = await _userManager.CreateAsync(user, model.Password!);
            if (!result.Succeeded) { foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description); return View("~/Views/FinancialOfficer/CreateCustomer.cshtml", model); }
            await _userManager.AddToRoleAsync(user, "Customer");
            TempData["SuccessMessage"] = "Customer account created successfully.";
            return RedirectToAction(nameof(Users));
        }

        [HttpGet]
        public async Task<IActionResult> EditCustomer(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null || !(user.UserType == "Student" || user.UserType == "Staff")) return NotFound();
            return View("~/Views/FinancialOfficer/EditCustomer.cshtml", new CustomerAccountViewModel { Id = user.Id, FirstName = user.FirstName, Surname = user.Surname, Email = user.Email ?? string.Empty, UserType = user.UserType, StudentNumber = user.StudentNumber, EmployeeNumber = user.EmployeeNumber, PhoneNumber = user.PhoneNumber, AccountStatus = user.AccountStatus });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCustomer(CustomerAccountViewModel model)
        {
            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null || !(user.UserType == "Student" || user.UserType == "Staff")) return NotFound();
            ModelState.Remove(nameof(model.Password)); ModelState.Remove(nameof(model.ConfirmPassword));
            ValidateCustomerModel(model, requirePassword: false);
            if (!ModelState.IsValid) return View("~/Views/FinancialOfficer/EditCustomer.cshtml", model);
            var email = model.Email.Trim().ToLowerInvariant();
            if (!SeedIdentityData.IsAllowedEmail(email)) { ModelState.AddModelError(nameof(model.Email), "Only approved UFS email domains are allowed."); return View("~/Views/FinancialOfficer/EditCustomer.cshtml", model); }
            var existing = await _userManager.FindByEmailAsync(email);
            if (existing != null && existing.Id != user.Id) { ModelState.AddModelError(nameof(model.Email), "That email address is already registered."); return View("~/Views/FinancialOfficer/EditCustomer.cshtml", model); }
            var number = model.UserType == "Student" ? model.StudentNumber?.Trim() : model.EmployeeNumber?.Trim();
            if (!string.IsNullOrWhiteSpace(number) && await _userManager.Users.AnyAsync(u => u.Id != user.Id && (u.StudentNumber == number || u.EmployeeNumber == number))) { ModelState.AddModelError(model.UserType == "Student" ? nameof(model.StudentNumber) : nameof(model.EmployeeNumber), "That number is already registered."); return View("~/Views/FinancialOfficer/EditCustomer.cshtml", model); }
            user.FirstName = model.FirstName.Trim(); user.Surname = model.Surname.Trim(); user.Email = email; user.UserName = email; user.UserType = model.UserType; user.StudentNumber = model.UserType == "Student" ? number : null; user.EmployeeNumber = model.UserType == "Staff" ? number : null; user.PhoneNumber = model.PhoneNumber?.Trim(); user.AccountStatus = model.AccountStatus == "Inactive" ? "Inactive" : "Active";
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded) { foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description); return View("~/Views/FinancialOfficer/EditCustomer.cshtml", model); }
            TempData["SuccessMessage"] = "Customer details updated successfully.";
            return RedirectToAction(nameof(Users));
        }

        [HttpGet]
        public async Task<IActionResult> CustomerDetails(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null || !(user.UserType == "Student" || user.UserType == "Staff")) return NotFound();
            ViewBag.ApplicationCount = await _context.Applications.CountAsync(a => a.UserID == id);
            ViewBag.PaymentCount = await _context.Payments.CountAsync(p => p.UserID == id);
            ViewBag.PaidAmount = await _context.Payments.Where(p => p.UserID == id && p.PaymentStatus == "Paid").SumAsync(p => (decimal?)p.Amount) ?? 0;
            return View("~/Views/FinancialOfficer/CustomerDetails.cshtml", user);
        }

        [HttpGet]
        public async Task<IActionResult> ResetCustomerPassword(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null || !(user.UserType == "Student" || user.UserType == "Staff")) return NotFound();
            return View("~/Views/FinancialOfficer/ResetCustomerPassword.cshtml", new ResetCustomerPasswordViewModel { CustomerId = id, CustomerName = $"{user.FirstName} {user.Surname}".Trim() });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetCustomerPassword(ResetCustomerPasswordViewModel model)
        {
            var user = await _userManager.FindByIdAsync(model.CustomerId);
            if (user == null || !(user.UserType == "Student" || user.UserType == "Staff")) return NotFound();
            if (!ModelState.IsValid) { model.CustomerName = $"{user.FirstName} {user.Surname}".Trim(); return View("~/Views/FinancialOfficer/ResetCustomerPassword.cshtml", model); }
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);
            if (!result.Succeeded) { foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description); model.CustomerName = $"{user.FirstName} {user.Surname}".Trim(); return View("~/Views/FinancialOfficer/ResetCustomerPassword.cshtml", model); }
            TempData["SuccessMessage"] = "Customer password reset successfully. Give the temporary password to the customer securely.";
            return RedirectToAction(nameof(CustomerDetails), new { id = user.Id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCustomer(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null || !(user.UserType == "Student" || user.UserType == "Staff")) return NotFound();
            var hasHistory = await _context.Applications.AnyAsync(a => a.UserID == id) || await _context.Payments.AnyAsync(p => p.UserID == id) || await _context.Reviews.AnyAsync(r => r.UserID == id) || await _context.Notifications.AnyAsync(n => n.UserID == id);
            if (hasHistory) { TempData["ErrorMessage"] = "This customer has history. Deactivate the account instead of deleting it so records remain intact."; return RedirectToAction(nameof(Users)); }
            var result = await _userManager.DeleteAsync(user);
            TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] = result.Succeeded ? "Customer account deleted." : "Customer account could not be deleted.";
            return RedirectToAction(nameof(Users));
        }

        private void ValidateCustomerModel(CustomerAccountViewModel model, bool requirePassword)
        {
            if (model.UserType != "Student" && model.UserType != "Staff") ModelState.AddModelError(nameof(model.UserType), "Customer type must be Student or Staff.");
            if (model.UserType == "Student" && string.IsNullOrWhiteSpace(model.StudentNumber)) ModelState.AddModelError(nameof(model.StudentNumber), "Student number is required for students.");
            if (model.UserType == "Staff" && string.IsNullOrWhiteSpace(model.EmployeeNumber)) ModelState.AddModelError(nameof(model.EmployeeNumber), "Employee number is required for staff.");
            if (requirePassword && string.IsNullOrWhiteSpace(model.Password)) ModelState.AddModelError(nameof(model.Password), "A temporary password is required.");
        }

        [HttpGet]
        public async Task<IActionResult> Advice()
        {
            return View("~/Views/Advice/AdminIndex.cshtml", await _context.FinancialAdvices.Include(a => a.Officer).OrderByDescending(a => a.DateCreated).ToListAsync());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ReviewAdvice(int id, string? response)
        {
            var advice = await _context.FinancialAdvices.FindAsync(id);
            if (advice == null) return NotFound();
            advice.Status = "Reviewed"; advice.AdminResponse = string.IsNullOrWhiteSpace(response) ? null : response.Trim(); advice.DateReviewed = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            if (!string.IsNullOrWhiteSpace(advice.OfficerUserID))
            {
                _context.Notifications.Add(new Notification { UserID = advice.OfficerUserID, Title = "Financial Advice Reviewed", Message = $"Your advice/input '{advice.Title}' was reviewed by the administrator.", DateCreated = DateTime.UtcNow });
                await _context.SaveChangesAsync();
            }
            TempData["SuccessMessage"] = "Financial advice marked as reviewed.";
            return RedirectToAction(nameof(Advice));
        }

        [HttpGet]
        public async Task<IActionResult> Users()
        {
            var users = await _userManager.Users.OrderBy(u => u.Surname).ThenBy(u => u.FirstName).ToListAsync();
            var model = new List<AdminUserViewModel>();
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var paymentStatus = "No allocation";
                decimal outstanding = 0;
                if (user.UserType == "Student" || user.UserType == "Staff")
                {
                    var approved = await _context.Applications.FirstOrDefaultAsync(a => a.UserID == user.Id && a.Status == "Approved");
                    if (approved != null)
                    {
                        var charges = await _context.AccommodationCharges.Where(c => c.AccommodationID == approved.AccommodationID && c.IsActive).Include(c => c.PaymentType).ToListAsync();
                        var payments = await _context.Payments.Where(p => p.UserID == user.Id && p.ApplicationID == approved.ApplicationID).ToListAsync();
                        foreach (var charge in charges)
                        {
                            var due = PaymentDueDateCalculator.GetCurrentCycleDueDate(charge);
                            var latest = payments.Where(p => p.AccommodationChargeID == charge.AccommodationChargeID && p.DueDate.HasValue && p.DueDate.Value.Date == due.Date).OrderByDescending(p => p.PaymentDate).FirstOrDefault();
                            if (latest?.PaymentStatus != "Paid") outstanding += charge.Amount;
                        }
                        paymentStatus = outstanding > 0 ? "Outstanding" : "Up to date";
                    }
                }
                model.Add(new AdminUserViewModel
                {
                    Id = user.Id, Name = $"{user.FirstName} {user.Surname}".Trim(), Email = user.Email ?? string.Empty,
                    UserType = user.UserType, Number = user.StudentNumber ?? user.EmployeeNumber ?? string.Empty,
                    Role = roles.FirstOrDefault() ?? "Customer", AccountStatus = user.AccountStatus, PaymentStatus = paymentStatus, OutstandingAmount = outstanding
                });
            }
            return View(model);
        }

    }
}