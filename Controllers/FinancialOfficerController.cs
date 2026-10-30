using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UFSStayConnect.Data;
using UFSStayConnect.Models;
using UFSStayConnect.ViewModels;

namespace UFSStayConnect.Controllers
{
    [Authorize(Roles = "Admin,FinancialOfficer")]
    public class FinancialOfficerController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ILogger<FinancialOfficerController> _logger;

        public FinancialOfficerController(
            AppDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ILogger<FinancialOfficerController> logger)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            if (User.IsInRole("Admin"))
            {
                var officers = new List<ApplicationUser>();
                foreach (var user in await _userManager.Users.OrderBy(u => u.Surname).ThenBy(u => u.FirstName).ToListAsync())
                    if (await _userManager.IsInRoleAsync(user, "FinancialOfficer")) officers.Add(user);
                return View("AdminIndex", officers);
            }

            ViewBag.Customers = await _userManager.Users.CountAsync(u => u.UserType == "Student" || u.UserType == "Staff");
            ViewBag.Pending = await _context.Payments.CountAsync(p => p.PaymentStatus == "Pending");
            ViewBag.Today = await _context.Payments.Where(p => p.PaymentStatus == "Paid" && p.PaymentDate.Date == DateTime.UtcNow.Date).SumAsync(p => (decimal?)p.Amount) ?? 0;
            ViewBag.Outstanding = await _context.Payments.Where(p => p.PaymentStatus == "Pending").SumAsync(p => (decimal?)p.Amount) ?? 0;
            ViewBag.Recent = await _context.Payments.Include(p => p.User).Include(p => p.PaymentType).OrderByDescending(p => p.PaymentDate).Take(8).ToListAsync();
            return View();
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public IActionResult Create() => View(new CreateOfficerViewModel());

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateOfficerViewModel model)
        {
            var email = model.Email?.Trim() ?? string.Empty;
            var employeeNumber = model.EmployeeNumber?.Trim() ?? string.Empty;

            if (!ModelState.IsValid)
                return View(model);

            if (!SeedIdentityData.IsAllowedEmail(email))
            {
                ModelState.AddModelError(nameof(model.Email), "Only approved UFS email domains (@ufs.ac.za or @ufs4life.ac.za) are allowed.");
                return View(model);
            }

            if (!await _roleManager.RoleExistsAsync("FinancialOfficer"))
            {
                var roleResult = await _roleManager.CreateAsync(new IdentityRole("FinancialOfficer"));
                if (!roleResult.Succeeded)
                {
                    foreach (var error in roleResult.Errors) ModelState.AddModelError(string.Empty, error.Description);
                    return View(model);
                }
            }

            if (await _userManager.FindByEmailAsync(email) != null)
            {
                ModelState.AddModelError(nameof(model.Email), "That email address is already registered.");
                return View(model);
            }

            if (!string.IsNullOrWhiteSpace(employeeNumber) && await _userManager.Users.AnyAsync(u => u.EmployeeNumber == employeeNumber))
            {
                ModelState.AddModelError(nameof(model.EmployeeNumber), "That employee number is already registered.");
                return View(model);
            }

            try
            {
                var user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    FirstName = model.FirstName.Trim(),
                    Surname = model.Surname.Trim(),
                    UserType = "Staff",
                    EmployeeNumber = employeeNumber,
                    AccountStatus = "Active",
                    DateCreated = DateTime.UtcNow
                };

                var result = await _userManager.CreateAsync(user, model.Password);
                if (!result.Succeeded)
                {
                    foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
                    return View(model);
                }

                var roleResult = await _userManager.AddToRoleAsync(user, "FinancialOfficer");
                if (!roleResult.Succeeded)
                {
                    foreach (var error in roleResult.Errors) ModelState.AddModelError(string.Empty, error.Description);
                    await _userManager.DeleteAsync(user);
                    return View(model);
                }

                TempData["SuccessMessage"] = $"Financial Officer {user.FirstName} {user.Surname} was created successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating Financial Officer account for {Email}.", email);
                ModelState.AddModelError(string.Empty, "The Financial Officer could not be created. Please check the details and try again.");
                return View(model);
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleOfficerStatus(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null || !await _userManager.IsInRoleAsync(user, "FinancialOfficer")) return NotFound();
            user.AccountStatus = user.AccountStatus == "Active" ? "Inactive" : "Active";
            await _userManager.UpdateAsync(user);
            TempData["SuccessMessage"] = user.AccountStatus == "Active" ? "Financial Officer activated." : "Financial Officer deactivated.";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin")]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteOfficer(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null || !await _userManager.IsInRoleAsync(user, "FinancialOfficer")) return NotFound();
            if (await _context.Notifications.AnyAsync(n => n.UserID == id))
            {
                TempData["ErrorMessage"] = "This officer has notification history. Deactivate the account instead so audit history is preserved.";
                return RedirectToAction(nameof(Index));
            }
            var result = await _userManager.DeleteAsync(user);
            TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] = result.Succeeded ? "Financial Officer account deleted." : "Financial Officer account could not be deleted.";
            return RedirectToAction(nameof(Index));
        }

        // Administrator-only reset of a Financial Officer's password. Officers cannot
        // change their own passwords; the Administrator sets a temporary password.
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> ResetOfficerPassword(string id)
        {
            var officer = await _userManager.FindByIdAsync(id);
            if (officer == null || !await _userManager.IsInRoleAsync(officer, "FinancialOfficer"))
                return NotFound();

            return View(new ResetOfficerPasswordViewModel
            {
                OfficerId = officer.Id,
                OfficerName = $"{officer.FirstName} {officer.Surname}".Trim()
            });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetOfficerPassword(ResetOfficerPasswordViewModel model)
        {
            var officer = await _userManager.FindByIdAsync(model.OfficerId);
            if (officer == null || !await _userManager.IsInRoleAsync(officer, "FinancialOfficer"))
                return NotFound();

            model.OfficerName = $"{officer.FirstName} {officer.Surname}".Trim();
            if (!ModelState.IsValid)
                return View(model);

            var token = await _userManager.GeneratePasswordResetTokenAsync(officer);
            var result = await _userManager.ResetPasswordAsync(officer, token, model.NewPassword);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                return View(model);
            }

            TempData["SuccessMessage"] = $"Password reset for {model.OfficerName}. Share the temporary password securely with the officer.";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "FinancialOfficer")]
        [HttpGet]
        public async Task<IActionResult> Advice()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();
            return View("~/Views/Advice/OfficerIndex.cshtml", await _context.FinancialAdvices.Where(a => a.OfficerUserID == user.Id).OrderByDescending(a => a.DateCreated).ToListAsync());
        }

        [Authorize(Roles = "FinancialOfficer")]
        [HttpGet]
        public IActionResult CreateAdvice() => View("~/Views/Advice/Create.cshtml", new FinancialAdviceViewModel());

        [Authorize(Roles = "FinancialOfficer")]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAdvice(FinancialAdviceViewModel model)
        {
            if (!ModelState.IsValid) return View("~/Views/Advice/Create.cshtml", model);
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();
            _context.FinancialAdvices.Add(new FinancialAdvice { OfficerUserID = user.Id, Title = model.Title.Trim(), Advice = model.Advice.Trim(), Status = "Open", DateCreated = DateTime.UtcNow });
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Financial advice/input was submitted to the administrator.";
            return RedirectToAction(nameof(Advice));
        }

        [Authorize(Roles = "FinancialOfficer")]
        [HttpGet]
        public async Task<IActionResult> Customers(string? search, string? status, string? type)
        {
            var query = _userManager.Users
                .Where(u => u.UserType == "Student" || u.UserType == "Staff");

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                query = query.Where(u =>
                    u.FirstName.Contains(search) ||
                    u.Surname.Contains(search) ||
                    (u.Email != null && u.Email.Contains(search)) ||
                    (u.StudentNumber != null && u.StudentNumber.Contains(search)) ||
                    (u.EmployeeNumber != null && u.EmployeeNumber.Contains(search)));
            }

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(u => u.AccountStatus == status);

            if (!string.IsNullOrWhiteSpace(type))
                query = query.Where(u => u.UserType == type);

            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.Type = type;

            return View(await query.OrderBy(u => u.Surname).ThenBy(u => u.FirstName).ToListAsync());
        }

        [Authorize(Roles = "FinancialOfficer")]
        [HttpGet]
        public IActionResult CreateCustomer() => View(new CustomerAccountViewModel());

        [Authorize(Roles = "FinancialOfficer")]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCustomer(CustomerAccountViewModel model)
        {
            if (model.UserType != "Student" && model.UserType != "Staff")
                ModelState.AddModelError(nameof(model.UserType), "Customer type must be Student or Staff.");

            if (model.UserType == "Student" && string.IsNullOrWhiteSpace(model.StudentNumber))
                ModelState.AddModelError(nameof(model.StudentNumber), "Student number is required for students.");

            if (model.UserType == "Staff" && string.IsNullOrWhiteSpace(model.EmployeeNumber))
                ModelState.AddModelError(nameof(model.EmployeeNumber), "Employee number is required for staff.");

            if (string.IsNullOrWhiteSpace(model.Password))
                ModelState.AddModelError(nameof(model.Password), "A temporary password is required.");

            if (!ModelState.IsValid) return View(model);

            var email = model.Email.Trim().ToLowerInvariant();
            if (!SeedIdentityData.IsAllowedEmail(email))
            {
                ModelState.AddModelError(nameof(model.Email), "Only approved UFS email domains are allowed.");
                return View(model);
            }

            if (await _userManager.FindByEmailAsync(email) != null)
            {
                ModelState.AddModelError(nameof(model.Email), "That email address is already registered.");
                return View(model);
            }

            var number = model.UserType == "Student" ? model.StudentNumber?.Trim() : model.EmployeeNumber?.Trim();
            if (!string.IsNullOrWhiteSpace(number) && await _userManager.Users.AnyAsync(u => u.StudentNumber == number || u.EmployeeNumber == number))
            {
                ModelState.AddModelError(model.UserType == "Student" ? nameof(model.StudentNumber) : nameof(model.EmployeeNumber), "That number is already registered.");
                return View(model);
            }

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = model.FirstName.Trim(),
                Surname = model.Surname.Trim(),
                UserType = model.UserType,
                StudentNumber = model.UserType == "Student" ? number : null,
                EmployeeNumber = model.UserType == "Staff" ? number : null,
                PhoneNumber = model.PhoneNumber?.Trim(),
                AccountStatus = model.AccountStatus == "Inactive" ? "Inactive" : "Active",
                DateCreated = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, model.Password!);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
                return View(model);
            }

            await _userManager.AddToRoleAsync(user, "Customer");
            TempData["SuccessMessage"] = $"Customer {user.FirstName} {user.Surname} was created successfully.";
            return RedirectToAction(nameof(Customers));
        }

        [Authorize(Roles = "FinancialOfficer")]
        [HttpGet]
        public async Task<IActionResult> EditCustomer(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null || !(user.UserType == "Student" || user.UserType == "Staff")) return NotFound();
            return View(new CustomerAccountViewModel
            {
                Id = user.Id,
                FirstName = user.FirstName,
                Surname = user.Surname,
                Email = user.Email ?? string.Empty,
                UserType = user.UserType,
                StudentNumber = user.StudentNumber,
                EmployeeNumber = user.EmployeeNumber,
                PhoneNumber = user.PhoneNumber,
                AccountStatus = user.AccountStatus
            });
        }

        [Authorize(Roles = "FinancialOfficer")]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCustomer(CustomerAccountViewModel model)
        {
            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null || !(user.UserType == "Student" || user.UserType == "Staff")) return NotFound();
            ModelState.Remove(nameof(model.Password));
            ModelState.Remove(nameof(model.ConfirmPassword));

            if (model.UserType != "Student" && model.UserType != "Staff") ModelState.AddModelError(nameof(model.UserType), "Customer type must be Student or Staff.");
            if (model.UserType == "Student" && string.IsNullOrWhiteSpace(model.StudentNumber)) ModelState.AddModelError(nameof(model.StudentNumber), "Student number is required.");
            if (model.UserType == "Staff" && string.IsNullOrWhiteSpace(model.EmployeeNumber)) ModelState.AddModelError(nameof(model.EmployeeNumber), "Employee number is required.");
            if (!ModelState.IsValid) return View(model);

            var email = model.Email.Trim().ToLowerInvariant();
            var existing = await _userManager.FindByEmailAsync(email);
            if (existing != null && existing.Id != user.Id)
            {
                ModelState.AddModelError(nameof(model.Email), "That email address is already registered.");
                return View(model);
            }

            var number = model.UserType == "Student" ? model.StudentNumber?.Trim() : model.EmployeeNumber?.Trim();
            if (!string.IsNullOrWhiteSpace(number) && await _userManager.Users.AnyAsync(u => u.Id != user.Id && (u.StudentNumber == number || u.EmployeeNumber == number)))
            {
                ModelState.AddModelError(model.UserType == "Student" ? nameof(model.StudentNumber) : nameof(model.EmployeeNumber), "That number is already registered.");
                return View(model);
            }

            user.FirstName = model.FirstName.Trim(); user.Surname = model.Surname.Trim(); user.Email = email; user.UserName = email;
            user.UserType = model.UserType; user.StudentNumber = model.UserType == "Student" ? number : null; user.EmployeeNumber = model.UserType == "Staff" ? number : null;
            user.PhoneNumber = model.PhoneNumber?.Trim(); user.AccountStatus = model.AccountStatus == "Inactive" ? "Inactive" : "Active";
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded) { foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description); return View(model); }
            TempData["SuccessMessage"] = "Customer details updated successfully.";
            return RedirectToAction(nameof(Customers));
        }

        [Authorize(Roles = "FinancialOfficer")]
        [HttpGet]
        public async Task<IActionResult> CustomerDetails(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null || !(user.UserType == "Student" || user.UserType == "Staff")) return NotFound();
            ViewBag.ApplicationCount = await _context.Applications.CountAsync(a => a.UserID == user.Id);
            ViewBag.PaymentCount = await _context.Payments.CountAsync(p => p.UserID == user.Id);
            ViewBag.PaidAmount = await _context.Payments.Where(p => p.UserID == user.Id && p.PaymentStatus == "Paid").SumAsync(p => (decimal?)p.Amount) ?? 0;
            return View(user);
        }

        [Authorize(Roles = "FinancialOfficer")]
        [HttpGet]
        public async Task<IActionResult> ResetCustomerPassword(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null || !(user.UserType == "Student" || user.UserType == "Staff")) return NotFound();
            return View(new ResetCustomerPasswordViewModel { CustomerId = user.Id, CustomerName = $"{user.FirstName} {user.Surname}".Trim() });
        }

        [Authorize(Roles = "FinancialOfficer")]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetCustomerPassword(ResetCustomerPasswordViewModel model)
        {
            var user = await _userManager.FindByIdAsync(model.CustomerId);
            if (user == null || !(user.UserType == "Student" || user.UserType == "Staff")) return NotFound();
            if (!ModelState.IsValid) { model.CustomerName = $"{user.FirstName} {user.Surname}".Trim(); return View(model); }
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);
            if (!result.Succeeded) { foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description); model.CustomerName = $"{user.FirstName} {user.Surname}".Trim(); return View(model); }
            TempData["SuccessMessage"] = "Customer password was reset successfully. Give the temporary password to the customer securely.";
            return RedirectToAction(nameof(CustomerDetails), new { id = user.Id });
        }

        [Authorize(Roles = "FinancialOfficer")]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleCustomerStatus(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null || !(user.UserType == "Student" || user.UserType == "Staff")) return NotFound();
            user.AccountStatus = user.AccountStatus == "Active" ? "Inactive" : "Active";
            await _userManager.UpdateAsync(user);
            TempData["SuccessMessage"] = user.AccountStatus == "Active" ? "Customer account activated." : "Customer account deactivated.";
            return RedirectToAction(nameof(Customers));
        }

        [Authorize(Roles = "FinancialOfficer")]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCustomer(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null || !(user.UserType == "Student" || user.UserType == "Staff")) return NotFound();
            var hasHistory = await _context.Applications.AnyAsync(a => a.UserID == id) || await _context.Payments.AnyAsync(p => p.UserID == id) || await _context.Reviews.AnyAsync(r => r.UserID == id);
            if (hasHistory)
            {
                TempData["ErrorMessage"] = "This customer has application, payment or review history. The account was not deleted; deactivate it instead so financial records remain intact.";
                return RedirectToAction(nameof(Customers));
            }
            var result = await _userManager.DeleteAsync(user);
            TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] = result.Succeeded ? "Customer account deleted." : "Customer account could not be deleted.";
            return RedirectToAction(nameof(Customers));
        }
    }
}
