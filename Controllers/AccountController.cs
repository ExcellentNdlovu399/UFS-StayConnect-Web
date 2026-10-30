using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using UFSStayConnect.Data;
using UFSStayConnect.DataAccess;
using UFSStayConnect.Models;
using UFSStayConnect.ViewModels;

namespace UFSStayConnect.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IRepositoryWrapper _repository;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IRepositoryWrapper repository)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _repository = repository;
        }


        // ============================================================
        // REGISTER
        // ============================================================

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(
            RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }


            // --------------------------------------------------------
            // STUDENT VALIDATION
            // --------------------------------------------------------

            if (model.UserType == "Student" &&
                string.IsNullOrWhiteSpace(model.StudentNumber))
            {
                ModelState.AddModelError(
                    "StudentNumber",
                    "Student number is required.");

                return View(model);
            }


            // --------------------------------------------------------
            // STAFF VALIDATION
            // --------------------------------------------------------

            if (model.UserType == "Staff" &&
                string.IsNullOrWhiteSpace(model.EmployeeNumber))
            {
                ModelState.AddModelError(
                    "EmployeeNumber",
                    "Employee number is required.");

                return View(model);
            }


            // --------------------------------------------------------
            // UFS EMAIL VALIDATION
            // --------------------------------------------------------

            if (!SeedIdentityData.IsAllowedEmail(model.Email))
            {
                ModelState.AddModelError(
                    "Email",
                    "Only UFS email addresses ending in @ufs.ac.za or @ufs4life.ac.za are allowed.");

                return View(model);
            }


            // --------------------------------------------------------
            // CHECK WHETHER EMAIL ALREADY EXISTS
            // --------------------------------------------------------

            var existingUser =
                await _userManager.FindByEmailAsync(model.Email);

            if (existingUser != null)
            {
                ModelState.AddModelError(
                    "Email",
                    "An account with this email address already exists.");

                return View(model);
            }


            // --------------------------------------------------------
            // CREATE USER
            // --------------------------------------------------------

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,

                FirstName = model.FirstName,
                Surname = model.Surname,

                UserType = model.UserType,

                StudentNumber = model.StudentNumber,
                EmployeeNumber = model.EmployeeNumber,

                AccountStatus = "Active",

                DateCreated = DateTime.UtcNow
            };


            var result =
                await _userManager.CreateAsync(
                    user,
                    model.Password);


            if (result.Succeeded)
            {
                // ----------------------------------------------------
                // STUDENTS AND STAFF ARE CUSTOMERS
                // ----------------------------------------------------

                await _userManager.AddToRoleAsync(
                    user,
                    "Customer");


                // ----------------------------------------------------
                // AUTOMATIC LOGIN AFTER REGISTRATION
                // ----------------------------------------------------

                await _signInManager.SignInAsync(
                    user,
                    isPersistent: false);


                // ----------------------------------------------------
                // CUSTOMER DASHBOARD
                // ----------------------------------------------------

                return RedirectToAction(
                    "Index",
                    "Dashboard");
            }


            // --------------------------------------------------------
            // IDENTITY ERRORS
            // --------------------------------------------------------

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(
                    "",
                    error.Description);
            }

            return View(model);
        }



        // ============================================================
        // LOGIN
        // ============================================================

        // ============================================================
        // LOGIN
        // ============================================================

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;

            return View();
        }


        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            LoginViewModel model,
            string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await _signInManager.PasswordSignInAsync(
                model.Email,
                model.Password,
                model.RememberMe,
                lockoutOnFailure: false);

            if (result.Succeeded)
            {
                var user =
                    await _userManager.FindByEmailAsync(model.Email);

                if (user == null)
                {
                    await _signInManager.SignOutAsync();

                    ModelState.AddModelError(
                        "",
                        "Unable to find your account.");

                    return View(model);
                }

                if (!string.Equals(user.AccountStatus, "Active", StringComparison.OrdinalIgnoreCase))
                {
                    await _signInManager.SignOutAsync();
                    ModelState.AddModelError("", "Your account is inactive. Please contact the administrator.");
                    return View(model);
                }

                // =====================================================
                // ADMIN
                // =====================================================

                if (await _userManager.IsInRoleAsync(user, "Admin"))
                {
                    return RedirectToAction(
                        "Index",
                        "Admin");
                }

                // =====================================================
                // FINANCIAL OFFICER
                // =====================================================

                if (await _userManager.IsInRoleAsync(
                        user,
                        "FinancialOfficer"))
                {
                    return RedirectToAction(
                        "Index",
                        "FinancialOfficer");
                }

                // =====================================================
                // CUSTOMER
                // =====================================================

                if (await _userManager.IsInRoleAsync(
                        user,
                        "Customer"))
                {
                    return RedirectToAction(
                        "Index",
                        "Dashboard");
                }

                // =====================================================
                // UNKNOWN ROLE
                // =====================================================

                await _signInManager.SignOutAsync();

                ModelState.AddModelError(
                    "",
                    "Your account does not have a valid system role.");

                return View(model);
            }

            if (result.IsLockedOut)
            {
                ModelState.AddModelError(
                    "",
                    "Your account has been locked. Please contact the administrator.");

                return View(model);
            }

            ModelState.AddModelError(
                "",
                "Invalid email or password.");

            return View(model);
        }

        // ============================================================
        // PASSWORD ASSISTANCE - GET
        // ============================================================

        [AllowAnonymous]
        [HttpGet]
        public IActionResult PasswordAssistance()
        {
            return View();
        }


        // ============================================================
        // PASSWORD ASSISTANCE - POST
        // ============================================================

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PasswordAssistance(
            PasswordAssistanceViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);

            if (user == null)
            {
                ModelState.AddModelError(
                    "",
                    "The email address and student/staff number could not be verified.");

                return View(model);
            }

            string userNumber;

            if (string.Equals(
                    user.UserType,
                    "Student",
                    StringComparison.OrdinalIgnoreCase))
            {
                userNumber = user.StudentNumber ?? string.Empty;
            }
            else
            {
                userNumber = user.EmployeeNumber ?? string.Empty;
            }

            if (!string.Equals(
                    userNumber.Trim(),
                    model.StudentOrStaffNumber.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(
                    "",
                    "We could not verify the account details provided.");

                return View(model);
            }

            if (!string.Equals(
                    user.AccountStatus,
                    "Active",
                    StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(
                    "",
                    "This account is currently inactive. Please contact the administrator.");

                return View(model);
            }

            var existingRequest = await _repository.PasswordResetRequest
                .FindPendingByUserAsync(user.Id);

            if (existingRequest != null)
            {
                TempData["Success"] =
                    "You already have a pending password assistance request.";

                return RedirectToAction(nameof(PasswordAssistanceSubmitted));
            }

            var request = new PasswordResetRequest
            {
                UserID = user.Id,
                Status = "Pending",
                Reason = string.IsNullOrWhiteSpace(model.Reason)
                    ? "Password assistance requested."
                    : model.Reason.Trim(),
                RequestDate = DateTime.UtcNow
            };

            _repository.PasswordResetRequest.Create(request);
            _repository.Save();

            TempData["Success"] =
                "Your password assistance request has been submitted. " +
                "A Financial Officer will review your request.";

            return RedirectToAction(nameof(PasswordAssistanceSubmitted));
        }


        // ============================================================
        // PASSWORD ASSISTANCE SUBMITTED
        // ============================================================

        [AllowAnonymous]
        [HttpGet]
        public IActionResult PasswordAssistanceSubmitted()
        {
            return View();
        }

        [Authorize]
        [HttpGet]
        public IActionResult AccessDenied(string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;

            return View();
        }


        // ============================================================
        // LOGOUT
        // ============================================================

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();

            return RedirectToAction(
                "Index",
                "Home");
        }



        // ============================================================
        // PROFILE - GET
        // ============================================================

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var user =
                await _userManager.GetUserAsync(
                    User);

            if (user == null)
            {
                return NotFound();
            }


            var model = new ProfileViewModel
            {
                FirstName = user.FirstName,
                Surname = user.Surname,

                Email = user.Email ?? "",

                UserType = user.UserType,

                StudentNumber = user.StudentNumber,

                EmployeeNumber = user.EmployeeNumber,

                PhoneNumber = user.PhoneNumber
            };


            return View(model);
        }



        // ============================================================
        // PROFILE - POST
        // ============================================================

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(
            ProfileViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }


            var user =
                await _userManager.GetUserAsync(
                    User);

            if (user == null)
            {
                return NotFound();
            }


            user.FirstName = model.FirstName;

            user.Surname = model.Surname;

            user.PhoneNumber = model.PhoneNumber;


            var result =
                await _userManager.UpdateAsync(
                    user);


            if (result.Succeeded)
            {
                TempData["Success"] =
                    "Profile updated successfully.";

                return RedirectToAction(
                    nameof(Profile));
            }


            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(
                    "",
                    error.Description);
            }


            return View(model);
        }



        // ============================================================
        // CHANGE PASSWORD - GET
        // ============================================================

        [Authorize]
        [HttpGet]
        public IActionResult ChangePassword()
        {
            if (User.IsInRole("FinancialOfficer"))
            {
                TempData["ErrorMessage"] = "Financial Officer passwords are managed by an Administrator.";
                return RedirectToAction(nameof(Profile));
            }

            return View();
        }



        // ============================================================
        // CHANGE PASSWORD - POST
        // ============================================================

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(
            ChangePasswordViewModel model)
        {
            if (User.IsInRole("FinancialOfficer"))
            {
                TempData["ErrorMessage"] = "Financial Officer passwords are managed by an Administrator.";
                return RedirectToAction(nameof(Profile));
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }


            var user =
                await _userManager.GetUserAsync(
                    User);

            if (user == null)
            {
                return NotFound();
            }

            if (await _userManager.IsInRoleAsync(user, "FinancialOfficer"))
            {
                TempData["ErrorMessage"] = "Financial Officer passwords are managed by an Administrator.";
                return RedirectToAction(nameof(Profile));
            }

            var result =
                await _userManager.ChangePasswordAsync(
                    user,
                    model.CurrentPassword,
                    model.NewPassword);


            if (result.Succeeded)
            {
                await _signInManager.RefreshSignInAsync(
                    user);

                TempData["Success"] =
                    "Password changed successfully.";

                return RedirectToAction(
                    nameof(Profile));
            }


            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(
                    "",
                    error.Description);
            }


            return View(model);
        }
    }
}