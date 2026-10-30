using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.RegularExpressions;
using UFSStayConnect.DataAccess;
using UFSStayConnect.Models;

namespace UFSStayConnect.Controllers.Api
{
    // Standalone controller. Shares the "api/account" route prefix with
    // ApiAccountController, but has different actions, so there is no conflict.
    [ApiController]
    [Route("api/account")]
    public class ApiProfileController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IRepositoryWrapper _repository;

        public ApiProfileController(
            UserManager<ApplicationUser> userManager,
            IRepositoryWrapper repository)
        {
            _userManager = userManager;
            _repository = repository;
        }

        // GET: /api/account/ping  (temporary: proves this build is the one running)
        [AllowAnonymous]
        [HttpGet("ping")]
        public IActionResult Ping() => Ok(new { version = "profile-controller-v1" });

        // GET: /api/account/profile
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var user = await CurrentUserAsync();

            if (user == null)
                return Unauthorized(new { message = "Your session is no longer valid. Please sign in again." });

            if (!IsActive(user))
                return Unauthorized(new { message = "This account is not active." });

            return Ok(BuildProfile(user));
        }

        // PUT: /api/account/profile
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] ProfileUpdateDto? request)
        {
            if (request == null)
                return BadRequest(new { message = "No profile details were provided." });

            var firstName = (request.FirstName ?? "").Trim();
            var surname = (request.Surname ?? "").Trim();
            var phone = (request.PhoneNumber ?? "").Trim();

            if (firstName.Length == 0)
                return BadRequest(new { message = "First name is required." });

            if (surname.Length == 0)
                return BadRequest(new { message = "Surname is required." });

            if (firstName.Length > 100 || surname.Length > 100)
                return BadRequest(new { message = "First name and surname must be 100 characters or fewer." });

            if (phone.Length > 0 && !Regex.IsMatch(phone, @"^\+?[0-9 ()\-]{7,20}$"))
                return BadRequest(new { message = "Enter a valid phone number." });

            var user = await CurrentUserAsync();

            if (user == null)
                return Unauthorized(new { message = "Your session is no longer valid. Please sign in again." });

            if (!IsActive(user))
                return Unauthorized(new { message = "This account is not active." });

            user.FirstName = firstName;
            user.Surname = surname;
            user.PhoneNumber = phone.Length == 0 ? null : phone;

            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => e.Description).ToList();
                return BadRequest(new { message = string.Join(" ", errors), errors });
            }

            return Ok(new
            {
                message = "Profile updated successfully.",
                profile = BuildProfile(user)
            });
        }

        // POST: /api/account/change-password
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] PasswordChangeDto? request)
        {
            if (request == null ||
                string.IsNullOrEmpty(request.CurrentPassword) ||
                string.IsNullOrEmpty(request.NewPassword))
            {
                return BadRequest(new { message = "Enter your current password and a new password." });
            }

            if (request.ConfirmNewPassword != null &&
                !string.Equals(request.NewPassword, request.ConfirmNewPassword, StringComparison.Ordinal))
            {
                return BadRequest(new { message = "The new password and confirmation do not match." });
            }

            if (string.Equals(request.CurrentPassword, request.NewPassword, StringComparison.Ordinal))
            {
                return BadRequest(new { message = "Your new password must be different from your current password." });
            }

            var user = await CurrentUserAsync();

            if (user == null)
                return Unauthorized(new { message = "Your session is no longer valid. Please sign in again." });

            if (!IsActive(user))
                return Unauthorized(new { message = "This account is not active." });

            if (await _userManager.IsInRoleAsync(user, "FinancialOfficer"))
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    message = "Financial Officer passwords are managed by an Administrator."
                });

            var result = await _userManager.ChangePasswordAsync(
                user, request.CurrentPassword, request.NewPassword);

            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => e.Description).ToList();
                return BadRequest(new { message = string.Join(" ", errors), errors });
            }

            if (user.PasswordChangeRequired)
            {
                user.PasswordChangeRequired = false;
                await _userManager.UpdateAsync(user);
            }

            return Ok(new { message = "Password changed successfully." });
        }

        // POST: /api/account/password-assistance (anonymous)
        [AllowAnonymous]
        [HttpPost("password-assistance")]
        public async Task<IActionResult> PasswordAssistance([FromBody] PasswordAssistanceDto? request)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.StudentOrStaffNumber))
            {
                return BadRequest(new { message = "Enter your UFS email and your student/staff number." });
            }

            const string unverified = "We could not verify the account details provided.";

            var user = await _userManager.FindByEmailAsync(request.Email.Trim());

            if (user == null)
                return BadRequest(new { message = unverified });

            var userNumber = string.Equals(user.UserType, "Student", StringComparison.OrdinalIgnoreCase)
                ? user.StudentNumber ?? string.Empty
                : user.EmployeeNumber ?? string.Empty;

            if (!string.Equals(userNumber.Trim(), request.StudentOrStaffNumber.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = unverified });
            }

            if (!IsActive(user))
                return BadRequest(new { message = "This account is currently inactive. Please contact the administrator." });

            var existing = await _repository.PasswordResetRequest.FindPendingByUserAsync(user.Id);

            if (existing != null)
                return Ok(new { message = "You already have a pending password assistance request." });

            var reason = string.IsNullOrWhiteSpace(request.Reason)
                ? "Password assistance requested."
                : request.Reason.Trim();

            if (reason.Length > 500)
                reason = reason[..500];

            _repository.PasswordResetRequest.Create(new PasswordResetRequest
            {
                UserID = user.Id,
                Status = "Pending",
                Reason = reason,
                RequestDate = DateTime.UtcNow
            });

            _repository.Save();

            return Ok(new
            {
                message = "Your password assistance request has been submitted. A Financial Officer will review your request."
            });
        }

        // ---------------- helpers ----------------

        private async Task<ApplicationUser?> CurrentUserAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            return string.IsNullOrWhiteSpace(userId)
                ? null
                : await _userManager.FindByIdAsync(userId);
        }

        private static bool IsActive(ApplicationUser user) =>
            string.Equals(user.AccountStatus, "Active", StringComparison.OrdinalIgnoreCase);

        private static object BuildProfile(ApplicationUser user) => new
        {
            id = user.Id,
            firstName = user.FirstName,
            surname = user.Surname,
            fullName = $"{user.FirstName} {user.Surname}".Trim(),
            email = user.Email,
            userType = user.UserType,
            studentNumber = user.StudentNumber,
            employeeNumber = user.EmployeeNumber,
            phoneNumber = user.PhoneNumber,
            accountStatus = user.AccountStatus,
            memberSince = user.DateCreated,
            passwordChangeRequired = user.PasswordChangeRequired
        };
    }

    public class ProfileUpdateDto
    {
        public string? FirstName { get; set; }
        public string? Surname { get; set; }
        public string? PhoneNumber { get; set; }
    }

    public class PasswordChangeDto
    {
        public string? CurrentPassword { get; set; }
        public string? NewPassword { get; set; }
        public string? ConfirmNewPassword { get; set; }
    }

    public class PasswordAssistanceDto
    {
        public string? Email { get; set; }
        public string? StudentOrStaffNumber { get; set; }
        public string? Reason { get; set; }
    }
}
