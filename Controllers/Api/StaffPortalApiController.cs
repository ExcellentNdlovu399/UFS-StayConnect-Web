using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using UFSStayConnect.Data;
using UFSStayConnect.Models;
using UFSStayConnect.Services;

namespace UFSStayConnect.Controllers.Api;

/// <summary>
/// JWT-authenticated endpoints for the Administrator and Financial Officer Android workspaces.
/// The API applies the same role boundaries and business rules as the MVC controllers.
/// </summary>
[ApiController]
[Route("api/staff")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin,FinancialOfficer")]
public class StaffPortalApiController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _users;
    private readonly RoleManager<IdentityRole> _roles;
    private readonly ILogger<StaffPortalApiController> _logger;
    private readonly IWebHostEnvironment _environment;

    public StaffPortalApiController(AppDbContext context, UserManager<ApplicationUser> users,
        RoleManager<IdentityRole> roles, ILogger<StaffPortalApiController> logger, IWebHostEnvironment environment)
    {
        _context = context;
        _users = users;
        _roles = roles;
        _logger = logger;
        _environment = environment;
    }

    private bool IsAdmin => User.IsInRole("Admin");
    private async Task<ApplicationUser?> CurrentUserAsync()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return string.IsNullOrWhiteSpace(id) ? await _users.GetUserAsync(User) : await _users.FindByIdAsync(id);
    }
    private static bool UfsEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email)) return false;
        var domain = email.Trim().Split('@').LastOrDefault()?.ToLowerInvariant();
        return domain == "ufs.ac.za" || domain == "ufs4life.ac.za";
    }
    private static string Name(ApplicationUser? user) => user == null ? "Unknown" : $"{user.FirstName} {user.Surname}".Trim();
    private static object CustomerDto(ApplicationUser u) => new
    {
        id = u.Id,
        firstName = u.FirstName,
        surname = u.Surname,
        fullName = Name(u),
        email = u.Email ?? "",
        userType = u.UserType,
        studentNumber = u.StudentNumber ?? "",
        employeeNumber = u.EmployeeNumber ?? "",
        number = u.StudentNumber ?? u.EmployeeNumber ?? "",
        phoneNumber = u.PhoneNumber ?? "",
        accountStatus = u.AccountStatus,
        dateCreated = u.DateCreated
    };

    // ---------------------------- dashboards ----------------------------
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        var customers = await _users.Users.CountAsync(u => u.UserType == "Student" || u.UserType == "Staff");
        var students = await _users.Users.CountAsync(u => u.UserType == "Student");
        var staff = await _users.Users.CountAsync(u => u.UserType == "Staff");
        var officers = (await _users.GetUsersInRoleAsync("FinancialOfficer")).Count;
        var allPayments = _context.Payments.AsNoTracking();
        var allApplications = _context.Applications.AsNoTracking();
        var pendingPayments = await allPayments.CountAsync(p => p.PaymentStatus == "Pending");
        var paidTotal = await allPayments.Where(p => p.PaymentStatus == "Paid").SumAsync(p => (decimal?)p.Amount) ?? 0m;
        var pendingTotal = await allPayments.Where(p => p.PaymentStatus == "Pending").SumAsync(p => (decimal?)p.Amount) ?? 0m;
        var todayTotal = await allPayments.Where(p => p.PaymentStatus == "Paid" && p.PaymentDate.Date == DateTime.UtcNow.Date).SumAsync(p => (decimal?)p.Amount) ?? 0m;
        var totalAccommodations = await _context.Accommodations.CountAsync();
        var totalRooms = await _context.Rooms.CountAsync();
        var availableRooms = await _context.Rooms.CountAsync(r => r.Status == "Available");
        var occupiedRooms = await _context.Rooms.CountAsync(r => r.Status == "Occupied");
        var pendingApplications = await allApplications.CountAsync(a => a.Status == "Pending");
        var recentPayments = await _context.Payments.AsNoTracking().Include(p => p.User).Include(p => p.PaymentType)
            .OrderByDescending(p => p.PaymentDate).Take(6)
            .Select(p => new {
                paymentId = p.PaymentID,
                customerName = p.User == null ? "Unknown" : p.User.FirstName + " " + p.User.Surname,
                paymentType = p.PaymentType == null ? "Payment" : p.PaymentType.Name,
                amount = p.Amount,
                status = p.PaymentStatus,
                paymentDate = p.PaymentDate,
                reference = p.ReferenceNumber
            }).ToListAsync();
        var recentApplications = await _context.Applications.AsNoTracking().Include(a => a.User).Include(a => a.Accommodation)
            .OrderByDescending(a => a.ApplicationDate).Take(5)
            .Select(a => new {
                applicationId = a.ApplicationID,
                customerName = a.User == null ? "Unknown" : a.User.FirstName + " " + a.User.Surname,
                accommodation = a.Accommodation == null ? "Residence" : a.Accommodation.Name,
                status = a.Status,
                applicationDate = a.ApplicationDate
            }).ToListAsync();

        return Ok(new
        {
            role = IsAdmin ? "Admin" : "FinancialOfficer",
            totalUsers = customers,
            totalCustomers = customers,
            totalStudents = students,
            totalStaff = staff,
            totalFinancialOfficers = officers,
            totalAccommodations,
            totalRooms,
            availableRooms,
            occupiedRooms,
            totalApplications = await allApplications.CountAsync(),
            pendingApplications,
            totalPayments = paidTotal,
            paidAmount = paidTotal,
            outstandingPayments = pendingTotal,
            pendingPaymentCount = pendingPayments,
            todayPaid = todayTotal,
            recentPayments,
            recentApplications,
            pendingAdvice = IsAdmin ? await _context.FinancialAdvices.CountAsync(a => a.Status != "Reviewed") : 0,
            totalReviews = await _context.Reviews.CountAsync()
        });
    }

    // ---------------------------- customers ----------------------------
    [HttpGet("customers")]
    public async Task<IActionResult> Customers([FromQuery] string? search, [FromQuery] string? status, [FromQuery] string? type)
    {
        var query = _users.Users.AsNoTracking().Where(u => u.UserType == "Student" || u.UserType == "Staff");
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(u => u.FirstName.Contains(s) || u.Surname.Contains(s) || (u.Email != null && u.Email.Contains(s))
                || (u.StudentNumber != null && u.StudentNumber.Contains(s)) || (u.EmployeeNumber != null && u.EmployeeNumber.Contains(s)));
        }
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(u => u.AccountStatus == status);
        if (!string.IsNullOrWhiteSpace(type)) query = query.Where(u => u.UserType == type);
        var result = await query.OrderBy(u => u.Surname).ThenBy(u => u.FirstName).Take(500).ToListAsync();
        var items = result.Select(CustomerDto).ToList();
        return Ok(new { total = items.Count, customers = items });
    }

    [HttpGet("customers/{id}")]
    public async Task<IActionResult> CustomerDetails(string id)
    {
        var user = await _users.FindByIdAsync(id);
        if (user == null || (user.UserType != "Student" && user.UserType != "Staff")) return NotFound(new { message = "Customer not found." });
        var applications = await _context.Applications.CountAsync(a => a.UserID == id);
        var payments = await _context.Payments.CountAsync(p => p.UserID == id);
        var paid = await _context.Payments.Where(p => p.UserID == id && p.PaymentStatus == "Paid").SumAsync(p => (decimal?)p.Amount) ?? 0m;
        var recent = await _context.Payments.AsNoTracking().Include(p => p.PaymentType).Where(p => p.UserID == id)
            .OrderByDescending(p => p.PaymentDate).Take(12).Select(p => new {
                paymentId = p.PaymentID,
                paymentType = p.PaymentType == null ? "Payment" : p.PaymentType.Name,
                amount = p.Amount,
                status = p.PaymentStatus,
                paymentDate = p.PaymentDate,
                reference = p.ReferenceNumber
            }).ToListAsync();
        return Ok(new { customer = CustomerDto(user), applicationCount = applications, paymentCount = payments, paidAmount = paid, recentPayments = recent });
    }

    public sealed class CustomerRequest
    {
        public string? FirstName { get; set; }
        public string? Surname { get; set; }
        public string? Email { get; set; }
        public string? UserType { get; set; }
        public string? StudentNumber { get; set; }
        public string? EmployeeNumber { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Password { get; set; }
        public string? AccountStatus { get; set; }
    }
    [HttpPost("customers")]
    public async Task<IActionResult> CreateCustomer([FromBody] CustomerRequest request)
    {
        if (request == null) return BadRequest(new { message = "Customer details are required." });
        var first = request.FirstName?.Trim() ?? ""; var surname = request.Surname?.Trim() ?? "";
        var email = request.Email?.Trim().ToLowerInvariant() ?? ""; var type = request.UserType?.Trim() ?? "";
        var number = type == "Student" ? request.StudentNumber?.Trim() : request.EmployeeNumber?.Trim();
        if (first.Length == 0 || surname.Length == 0) return BadRequest(new { message = "First name and surname are required." });
        if (!UfsEmail(email)) return BadRequest(new { message = "Only approved UFS email domains (@ufs.ac.za or @ufs4life.ac.za) are allowed." });
        if (type != "Student" && type != "Staff") return BadRequest(new { message = "Customer type must be Student or Staff." });
        if (string.IsNullOrWhiteSpace(number)) return BadRequest(new { message = type == "Student" ? "Student number is required." : "Employee number is required." });
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 12) return BadRequest(new { message = "Temporary password must be at least 12 characters." });
        if (await _users.FindByEmailAsync(email) != null) return Conflict(new { message = "That email address is already registered." });
        if (await _users.Users.AnyAsync(u => u.StudentNumber == number || u.EmployeeNumber == number)) return Conflict(new { message = "That student/employee number is already registered." });
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = first,
            Surname = surname,
            UserType = type,
            StudentNumber = type == "Student" ? number : null,
            EmployeeNumber = type == "Staff" ? number : null,
            PhoneNumber = request.PhoneNumber?.Trim(),
            AccountStatus = request.AccountStatus == "Inactive" ? "Inactive" : "Active",
            DateCreated = DateTime.UtcNow
        };
        var created = await _users.CreateAsync(user, request.Password);
        if (!created.Succeeded) return BadRequest(new { message = string.Join(" ", created.Errors.Select(e => e.Description)) });
        var role = await _users.AddToRoleAsync(user, "Customer");
        if (!role.Succeeded) { await _users.DeleteAsync(user); return StatusCode(500, new { message = "Could not assign the Customer role." }); }
        return CreatedAtAction(nameof(CustomerDetails), new { id = user.Id }, new { message = "Customer created successfully.", customer = CustomerDto(user) });
    }

    [HttpPut("customers/{id}")]
    public async Task<IActionResult> UpdateCustomer(string id, [FromBody] CustomerRequest request)
    {
        var user = await _users.FindByIdAsync(id);
        if (user == null || (user.UserType != "Student" && user.UserType != "Staff")) return NotFound(new { message = "Customer not found." });
        var first = request.FirstName?.Trim() ?? ""; var surname = request.Surname?.Trim() ?? "";
        var email = request.Email?.Trim().ToLowerInvariant() ?? ""; var type = request.UserType?.Trim() ?? "";
        var number = type == "Student" ? request.StudentNumber?.Trim() : request.EmployeeNumber?.Trim();
        if (first.Length == 0 || surname.Length == 0 || !UfsEmail(email)) return BadRequest(new { message = "Enter valid names and a UFS email address." });
        if (type != "Student" && type != "Staff") return BadRequest(new { message = "Customer type must be Student or Staff." });
        if (string.IsNullOrWhiteSpace(number)) return BadRequest(new { message = type == "Student" ? "Student number is required." : "Employee number is required." });
        var otherEmail = await _users.FindByEmailAsync(email);
        if (otherEmail != null && otherEmail.Id != id) return Conflict(new { message = "That email address is already registered." });
        if (await _users.Users.AnyAsync(u => u.Id != id && (u.StudentNumber == number || u.EmployeeNumber == number))) return Conflict(new { message = "That student/employee number is already registered." });
        user.FirstName = first; user.Surname = surname; user.Email = email; user.UserName = email; user.UserType = type;
        user.StudentNumber = type == "Student" ? number : null; user.EmployeeNumber = type == "Staff" ? number : null;
        user.PhoneNumber = request.PhoneNumber?.Trim(); user.AccountStatus = request.AccountStatus == "Inactive" ? "Inactive" : "Active";
        var updated = await _users.UpdateAsync(user);
        if (!updated.Succeeded) return BadRequest(new { message = string.Join(" ", updated.Errors.Select(e => e.Description)) });
        return Ok(new { message = "Customer details updated successfully.", customer = CustomerDto(user) });
    }

    [HttpPost("customers/{id}/status")]
    public async Task<IActionResult> ToggleCustomerStatus(string id)
    {
        var user = await _users.FindByIdAsync(id);
        if (user == null) return NotFound(new { message = "User not found." });
        if (await _users.IsInRoleAsync(user, "Admin")) return BadRequest(new { message = "Administrator accounts cannot be deactivated here." });
        if (user.UserType != "Student" && user.UserType != "Staff") return BadRequest(new { message = "Only customer accounts can be changed here." });
        user.AccountStatus = string.Equals(user.AccountStatus, "Active", StringComparison.OrdinalIgnoreCase) ? "Inactive" : "Active";
        var result = await _users.UpdateAsync(user);
        return result.Succeeded ? Ok(new { message = $"Customer account {user.AccountStatus.ToLowerInvariant()}.", accountStatus = user.AccountStatus }) : BadRequest(new { message = string.Join(" ", result.Errors.Select(e => e.Description)) });
    }

    [HttpPost("customers/{id}/reset-password")]
    public async Task<IActionResult> ResetCustomerPassword(string id, [FromBody] PasswordRequest request)
    {
        var user = await _users.FindByIdAsync(id);
        if (user == null || (user.UserType != "Student" && user.UserType != "Staff")) return NotFound(new { message = "Customer not found." });
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 12 || request.Password != request.ConfirmPassword)
            return BadRequest(new { message = "Enter matching passwords with at least 12 characters." });
        var token = await _users.GeneratePasswordResetTokenAsync(user);
        var result = await _users.ResetPasswordAsync(user, token, request.Password);
        if (!result.Succeeded) return BadRequest(new { message = string.Join(" ", result.Errors.Select(e => e.Description)) });
        return Ok(new { message = "Customer password reset successfully." });
    }

    [HttpDelete("customers/{id}")]
    public async Task<IActionResult> DeleteCustomer(string id)
    {
        var user = await _users.FindByIdAsync(id);
        if (user == null || (user.UserType != "Student" && user.UserType != "Staff")) return NotFound(new { message = "Customer not found." });
        var hasHistory = await _context.Applications.AnyAsync(a => a.UserID == id)
            || await _context.Payments.AnyAsync(p => p.UserID == id)
            || await _context.Reviews.AnyAsync(r => r.UserID == id)
            || await _context.Notifications.AnyAsync(n => n.UserID == id);
        if (hasHistory) return Conflict(new { message = "This customer has application, payment, review or notification history. Deactivate the account instead so records remain intact." });
        var result = await _users.DeleteAsync(user);
        return result.Succeeded ? Ok(new { message = "Customer account deleted." }) : BadRequest(new { message = string.Join(" ", result.Errors.Select(e => e.Description)) });
    }

    public sealed class PasswordRequest { public string? Password { get; set; } public string? ConfirmPassword { get; set; } }

    // ---------------------------- financial officers ----------------------------
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
    [HttpGet("officers")]
    public async Task<IActionResult> Officers()
    {
        var officers = await _users.GetUsersInRoleAsync("FinancialOfficer");
        return Ok(new { total = officers.Count, officers = officers.OrderBy(u => u.Surname).Select(CustomerDto).ToList() });
    }

    public sealed class OfficerRequest { public string? FirstName { get; set; } public string? Surname { get; set; } public string? Email { get; set; } public string? EmployeeNumber { get; set; } public string? Password { get; set; } public string? ConfirmPassword { get; set; } }
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
    [HttpPost("officers")]
    public async Task<IActionResult> CreateOfficer([FromBody] OfficerRequest request)
    {
        var first = request.FirstName?.Trim() ?? ""; var surname = request.Surname?.Trim() ?? ""; var email = request.Email?.Trim().ToLowerInvariant() ?? ""; var employee = request.EmployeeNumber?.Trim() ?? "";
        if (first.Length == 0 || surname.Length == 0 || employee.Length == 0) return BadRequest(new { message = "First name, surname and employee number are required." });
        if (!UfsEmail(email)) return BadRequest(new { message = "Only approved UFS email domains are allowed." });
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 12 || request.Password != request.ConfirmPassword) return BadRequest(new { message = "Enter matching passwords with at least 12 characters." });
        if (await _users.FindByEmailAsync(email) != null) return Conflict(new { message = "That email address is already registered." });
        if (await _users.Users.AnyAsync(u => u.EmployeeNumber == employee)) return Conflict(new { message = "That employee number is already registered." });
        if (!await _roles.RoleExistsAsync("FinancialOfficer")) { var rr = await _roles.CreateAsync(new IdentityRole("FinancialOfficer")); if (!rr.Succeeded) return StatusCode(500, new { message = "Could not initialise the Financial Officer role." }); }
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, FirstName = first, Surname = surname, UserType = "Staff", EmployeeNumber = employee, AccountStatus = "Active", DateCreated = DateTime.UtcNow };
        var created = await _users.CreateAsync(user, request.Password);
        if (!created.Succeeded) return BadRequest(new { message = string.Join(" ", created.Errors.Select(e => e.Description)) });
        var assigned = await _users.AddToRoleAsync(user, "FinancialOfficer");
        if (!assigned.Succeeded) { await _users.DeleteAsync(user); return StatusCode(500, new { message = "Could not assign the Financial Officer role." }); }
        return Ok(new { message = "Financial Officer created and authorised successfully.", officer = CustomerDto(user) });
    }

    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
    [HttpPost("officers/{id}/reset-password")]
    public async Task<IActionResult> ResetOfficerPassword(string id, [FromBody] PasswordRequest request)
    {
        var officer = await _users.FindByIdAsync(id);
        if (officer == null || !await _users.IsInRoleAsync(officer, "FinancialOfficer"))
            return NotFound(new { message = "Financial Officer not found." });

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 12 ||
            !string.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal))
            return BadRequest(new { message = "Enter matching passwords with at least 12 characters." });

        var token = await _users.GeneratePasswordResetTokenAsync(officer);
        var result = await _users.ResetPasswordAsync(officer, token, request.Password);
        if (!result.Succeeded)
            return BadRequest(new { message = string.Join(" ", result.Errors.Select(e => e.Description)) });

        return Ok(new { message = "Financial Officer password reset successfully. Share the temporary password securely." });
    }

    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
    [HttpPost("officers/{id}/status")]
    public async Task<IActionResult> ToggleOfficerStatus(string id)
    {
        var user = await _users.FindByIdAsync(id);
        if (user == null || !await _users.IsInRoleAsync(user, "FinancialOfficer")) return NotFound(new { message = "Financial Officer not found." });
        user.AccountStatus = user.AccountStatus == "Active" ? "Inactive" : "Active";
        var result = await _users.UpdateAsync(user);
        return result.Succeeded ? Ok(new { message = $"Financial Officer {user.AccountStatus.ToLowerInvariant()}.", accountStatus = user.AccountStatus }) : BadRequest(new { message = string.Join(" ", result.Errors.Select(e => e.Description)) });
    }

    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
    [HttpDelete("officers/{id}")]
    public async Task<IActionResult> DeleteOfficer(string id)
    {
        var user = await _users.FindByIdAsync(id);
        if (user == null || !await _users.IsInRoleAsync(user, "FinancialOfficer")) return NotFound(new { message = "Financial Officer not found." });
        if (await _context.Notifications.AnyAsync(n => n.UserID == id)
            || await _context.FinancialAdvices.AnyAsync(a => a.OfficerUserID == id)
            || await _context.ServiceUsageRecords.AnyAsync(r => r.RecordedByUserID == id))
            return Conflict(new { message = "This officer has advice, service-usage or notification history. Deactivate the account instead so audit history is preserved." });
        var result = await _users.DeleteAsync(user);
        return result.Succeeded ? Ok(new { message = "Financial Officer account deleted." }) : BadRequest(new { message = string.Join(" ", result.Errors.Select(e => e.Description)) });
    }

    // ---------------------------- payments ----------------------------
    [HttpGet("payments/{id:int}/proof")]
    public async Task<IActionResult> PaymentProof(int id)
    {
        var payment = await _context.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.PaymentID == id);
        if (payment == null) return NotFound(new { message = "Payment not found." });
        if (string.IsNullOrWhiteSpace(payment.ProofOfPaymentPath)) return NotFound(new { message = "No proof of payment was uploaded for this transaction." });

        var webRoot = Path.GetFullPath(_environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"));
        var raw = payment.ProofOfPaymentPath.Trim().Replace('\\', '/');
        if (Uri.TryCreate(raw, UriKind.Absolute, out var absolute) && absolute.IsFile) raw = absolute.LocalPath.Replace('\\', '/');
        raw = raw.TrimStart('~', '/');
        if (raw.StartsWith("wwwroot/", StringComparison.OrdinalIgnoreCase)) raw = raw.Substring("wwwroot/".Length);
        var fullPath = Path.GetFullPath(Path.Combine(webRoot, raw.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = webRoot.EndsWith(Path.DirectorySeparatorChar) ? webRoot : webRoot + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return BadRequest(new { message = "Invalid proof document path." });
        if (!System.IO.File.Exists(fullPath)) return NotFound(new { message = "The proof file is not available on the server." });

        var extension = Path.GetExtension(fullPath).ToLowerInvariant();
        var contentType = extension switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
        return PhysicalFile(fullPath, contentType, Path.GetFileName(fullPath));
    }

    [HttpGet("payments")]
    public async Task<IActionResult> Payments([FromQuery] string? status, [FromQuery] string? search)
    {
        var query = _context.Payments.AsNoTracking().Include(p => p.User).Include(p => p.PaymentType).Include(p => p.Application).ThenInclude(a => a!.Accommodation).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(p => p.PaymentStatus == status);
        if (!string.IsNullOrWhiteSpace(search)) { var s = search.Trim(); query = query.Where(p => p.ReferenceNumber.Contains(s) || (p.User != null && (p.User.FirstName.Contains(s) || p.User.Surname.Contains(s) || (p.User.Email != null && p.User.Email.Contains(s))))); }
        var rows = await query.OrderByDescending(p => p.PaymentDate).Take(500).Select(p => new {
            paymentId = p.PaymentID,
            customerId = p.UserID,
            customerName = p.User == null ? "Unknown" : p.User.FirstName + " " + p.User.Surname,
            studentNumber = p.User == null ? "" : (p.User.StudentNumber ?? p.User.EmployeeNumber ?? ""),
            email = p.User == null ? "" : p.User.Email,
            paymentType = p.PaymentType == null ? "Payment" : p.PaymentType.Name,
            accommodationName = p.Application == null || p.Application.Accommodation == null ? "" : p.Application.Accommodation.Name,
            amount = p.Amount,
            status = p.PaymentStatus,
            reference = p.ReferenceNumber,
            method = p.PaymentMethod,
            description = p.Description ?? "",
            proofOfPaymentPath = p.ProofOfPaymentPath ?? "",
            paymentDate = p.PaymentDate,
            dueDate = p.DueDate
        }).ToListAsync();
        return Ok(new { total = rows.Count, pendingCount = rows.Count(x => x.status == "Pending"), payments = rows });
    }

    [HttpPost("payments/{id:int}/approve")]
    public async Task<IActionResult> ApprovePayment(int id)
    {
        var p = await _context.Payments.Include(x => x.PaymentType).FirstOrDefaultAsync(x => x.PaymentID == id);
        if (p == null) return NotFound(new { message = "Payment not found." });
        if (!string.Equals(p.PaymentStatus, "Pending", StringComparison.OrdinalIgnoreCase)) return Conflict(new { message = "Only pending payments can be approved." });
        p.PaymentStatus = "Paid";
        _context.Notifications.Add(new Notification { UserID = p.UserID, Title = "Payment Approved", Message = $"Your {p.PaymentType?.Name ?? "payment"} payment of R{p.Amount:N2} has been approved.", DateCreated = DateTime.UtcNow });
        await _context.SaveChangesAsync();
        return Ok(new { message = "Payment approved.", status = p.PaymentStatus });
    }

    public sealed class RejectPaymentRequest { public string? Reason { get; set; } public string? Explanation { get; set; } }
    [HttpPost("payments/{id:int}/reject")]
    public async Task<IActionResult> RejectPayment(int id, [FromBody] RejectPaymentRequest request)
    {
        var p = await _context.Payments.Include(x => x.PaymentType).FirstOrDefaultAsync(x => x.PaymentID == id);
        if (p == null) return NotFound(new { message = "Payment not found." });
        if (!string.Equals(p.PaymentStatus, "Pending", StringComparison.OrdinalIgnoreCase)) return Conflict(new { message = "Only pending payments can be rejected." });
        var reason = request.Reason?.Trim() ?? "";
        if (reason.Length == 0) return BadRequest(new { message = "A rejection reason is required." });
        if (!string.IsNullOrWhiteSpace(request.Explanation)) reason += " - " + request.Explanation.Trim();
        if (reason.Length > 500) reason = reason[..500];
        p.PaymentStatus = "Rejected"; p.Description = "Rejected: " + reason;
        _context.Notifications.Add(new Notification { UserID = p.UserID, Title = "Payment Rejected", Message = $"Your payment of R{p.Amount:N2} was rejected. Reason: {reason}", DateCreated = DateTime.UtcNow });
        await _context.SaveChangesAsync();
        return Ok(new { message = "Payment rejected.", status = p.PaymentStatus });
    }

    // ---------------------------- reports ----------------------------
    private IQueryable<Payment> FilterPayments(string? status, int? paymentTypeId, DateTime? from, DateTime? to)
    {
        var q = _context.Payments.AsNoTracking().Include(p => p.User).Include(p => p.PaymentType).Include(p => p.Application).ThenInclude(a => a!.Accommodation).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(p => p.PaymentStatus == status);
        if (paymentTypeId.HasValue) q = q.Where(p => p.PaymentTypeID == paymentTypeId.Value);
        if (from.HasValue) q = q.Where(p => p.PaymentDate >= from.Value.Date);
        if (to.HasValue) q = q.Where(p => p.PaymentDate < to.Value.Date.AddDays(1));
        return q;
    }
    private static bool DateArg(string? value, out DateTime date) => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    [HttpGet("reports")]
    public async Task<IActionResult> Reports([FromQuery] string? status, [FromQuery] int? paymentTypeId, [FromQuery] string? from, [FromQuery] string? to)
    {
        DateTime? fromDate = DateArg(from, out var fd) ? fd : null; DateTime? toDate = DateArg(to, out var td) ? td : null;
        var payments = await FilterPayments(status, paymentTypeId, fromDate, toDate).OrderByDescending(p => p.PaymentDate).Take(500).ToListAsync();
        var customers = await _users.Users.CountAsync(); var approved = await _context.Applications.CountAsync(a => a.Status == "Approved");
        var pendingApps = await _context.Applications.CountAsync(a => a.Status == "Pending"); var cancelled = await _context.Applications.CountAsync(a => a.Status == "Cancelled" || a.Status == "Withdrawn");
        var rows = payments.Select(p => new {
            paymentId = p.PaymentID,
            reference = p.ReferenceNumber,
            customer = Name(p.User),
            accommodation = p.Application?.Accommodation?.Name ?? "",
            paymentType = p.PaymentType?.Name ?? "Unknown",
            amount = p.Amount,
            method = p.PaymentMethod,
            status = p.PaymentStatus,
            paymentDate = p.PaymentDate,
            dueDate = p.DueDate
        }).ToList();
        return Ok(new
        {
            totalCustomers = customers,
            approvedApplications = approved,
            pendingApplications = pendingApps,
            cancelledApplications = cancelled,
            paidAmount = payments.Where(p => p.PaymentStatus == "Paid").Sum(p => p.Amount),
            pendingAmount = payments.Where(p => p.PaymentStatus == "Pending").Sum(p => p.Amount),
            rejectedAmount = payments.Where(p => p.PaymentStatus == "Rejected").Sum(p => p.Amount),
            count = rows.Count,
            rows,
            paymentTypes = await _context.PaymentTypes.AsNoTracking().OrderBy(p => p.Name).Select(p => new { paymentTypeId = p.PaymentTypeID, name = p.Name }).ToListAsync()
        });
    }

    [HttpGet("reports/export")]
    public async Task<IActionResult> ExportReport([FromQuery] string? status, [FromQuery] int? paymentTypeId, [FromQuery] string? from, [FromQuery] string? to)
    {
        DateTime? fromDate = DateArg(from, out var fd) ? fd : null; DateTime? toDate = DateArg(to, out var td) ? td : null;
        var payments = await FilterPayments(status, paymentTypeId, fromDate, toDate).OrderByDescending(p => p.PaymentDate).ToListAsync();
        static string Q(string? s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
        var csv = new StringBuilder("Reference,Customer,Accommodation,Payment Type,Amount,Method,Status,Payment Date,Due Date\r\n");
        foreach (var p in payments) csv.AppendLine(string.Join(",", Q(p.ReferenceNumber), Q(Name(p.User)), Q(p.Application?.Accommodation?.Name), Q(p.PaymentType?.Name), p.Amount.ToString("0.00", CultureInfo.InvariantCulture), Q(p.PaymentMethod), Q(p.PaymentStatus), Q(p.PaymentDate.ToString("yyyy-MM-dd HH:mm")), Q(p.DueDate?.ToString("yyyy-MM-dd"))));
        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"UFSStayConnect-PaymentReport-{DateTime.UtcNow:yyyyMMddHHmm}.csv");
    }

    // ---------------------------- reviews ----------------------------
    [HttpGet("reviews")]
    public async Task<IActionResult> Reviews()
    {
        var items = await _context.Reviews.AsNoTracking().Include(r => r.User).Include(r => r.Accommodation).OrderByDescending(r => r.DateCreated).Take(500)
            .Select(r => new {
                reviewId = r.ReviewID,
                customer = r.User == null ? "Unknown" : r.User.FirstName + " " + r.User.Surname,
                accommodationId = r.AccommodationID,
                accommodation = r.Accommodation == null ? "Residence" : r.Accommodation.Name,
                rating = r.Rating,
                comment = r.Comment ?? "",
                dateCreated = r.DateCreated
            }).ToListAsync();
        return Ok(new { total = items.Count, averageRating = items.Count == 0 ? 0 : Math.Round(items.Average(x => x.rating), 2), reviews = items });
    }

    // ---------------------------- financial advice ----------------------------
    [HttpGet("advice")]
    public async Task<IActionResult> Advice()
    {
        var query = _context.FinancialAdvices.AsNoTracking().Include(a => a.Officer).AsQueryable();
        if (!IsAdmin) { var me = await CurrentUserAsync(); if (me == null) return Unauthorized(); query = query.Where(a => a.OfficerUserID == me.Id); }
        var items = await query.OrderByDescending(a => a.DateCreated).Take(500).Select(a => new {
            adviceId = a.FinancialAdviceID,
            officerName = a.Officer == null ? "Unknown" : a.Officer.FirstName + " " + a.Officer.Surname,
            title = a.Title,
            advice = a.Advice,
            status = a.Status,
            adminResponse = a.AdminResponse ?? "",
            dateCreated = a.DateCreated,
            dateReviewed = a.DateReviewed
        }).ToListAsync();
        return Ok(new { total = items.Count, advice = items });
    }
    public sealed class AdviceRequest { public string? Title { get; set; } public string? Advice { get; set; } }
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "FinancialOfficer")]
    [HttpPost("advice")]
    public async Task<IActionResult> CreateAdvice([FromBody] AdviceRequest request)
    {
        var me = await CurrentUserAsync(); if (me == null) return Unauthorized();
        var title = request.Title?.Trim() ?? ""; var advice = request.Advice?.Trim() ?? "";
        if (title.Length == 0 || title.Length > 150 || advice.Length == 0 || advice.Length > 2000) return BadRequest(new { message = "Title is required (max 150 characters) and advice is required (max 2,000 characters)." });
        var item = new FinancialAdvice { OfficerUserID = me.Id, Title = title, Advice = advice, Status = "Open", DateCreated = DateTime.UtcNow };
        _context.FinancialAdvices.Add(item); await _context.SaveChangesAsync();
        var admins = await _users.GetUsersInRoleAsync("Admin");
        foreach (var admin in admins) _context.Notifications.Add(new Notification { UserID = admin.Id, Title = "New Financial Officer Advice", Message = $"{Name(me)} submitted: {title}", DateCreated = DateTime.UtcNow });
        await _context.SaveChangesAsync();
        return Ok(new { message = "Advice/input submitted to the administrator.", adviceId = item.FinancialAdviceID });
    }
    public sealed class AdviceResponseRequest { public string? Response { get; set; } }
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
    [HttpPost("advice/{id:int}/respond")]
    public async Task<IActionResult> RespondAdvice(int id, [FromBody] AdviceResponseRequest request)
    {
        var advice = await _context.FinancialAdvices.FirstOrDefaultAsync(a => a.FinancialAdviceID == id);
        if (advice == null) return NotFound(new { message = "Advice entry not found." });
        advice.Status = "Reviewed"; advice.AdminResponse = string.IsNullOrWhiteSpace(request.Response) ? null : request.Response.Trim(); advice.DateReviewed = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(advice.OfficerUserID)) _context.Notifications.Add(new Notification { UserID = advice.OfficerUserID, Title = "Financial Advice Reviewed", Message = $"Your advice/input '{advice.Title}' was reviewed by the administrator.", DateCreated = DateTime.UtcNow });
        await _context.SaveChangesAsync(); return Ok(new { message = "Financial advice marked as reviewed." });
    }

    // ---------------------------- accommodation applications ----------------------------
    [HttpGet("applications")]
    public async Task<IActionResult> Applications([FromQuery] string? status, [FromQuery] string? search)
    {
        var query = _context.Applications.AsNoTracking().Include(a => a.User).Include(a => a.Accommodation).Include(a => a.Room).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(a => a.Status == status);
        if (!string.IsNullOrWhiteSpace(search)) { var s = search.Trim(); query = query.Where(a => (a.User != null && (a.User.FirstName.Contains(s) || a.User.Surname.Contains(s) || (a.User.Email != null && a.User.Email.Contains(s)))) || (a.Accommodation != null && a.Accommodation.Name.Contains(s))); }
        var items = await query.OrderByDescending(a => a.ApplicationDate).Take(500).Select(a => new {
            applicationId = a.ApplicationID,
            customerId = a.UserID,
            customerName = a.User == null ? "Unknown" : a.User.FirstName + " " + a.User.Surname,
            email = a.User == null ? "" : a.User.Email,
            userType = a.User == null ? "" : a.User.UserType,
            accommodationId = a.AccommodationID,
            accommodation = a.Accommodation == null ? "Residence" : a.Accommodation.Name,
            roomId = a.RoomID,
            roomNumber = a.Room == null ? "Not allocated" : a.Room.RoomNumber,
            numberOfVehicles = a.NumberOfVehicles,
            parkingFee = a.ParkingFee,
            status = a.Status,
            rejectionReason = a.RejectionReason ?? "",
            applicationDate = a.ApplicationDate,
            idDocumentName = a.IDDocumentName ?? "",
            registrationDocumentName = a.RegistrationDocumentName ?? "",
            fundingDocumentName = a.FundingDocumentName ?? "",
            employmentDocumentName = a.EmploymentDocumentName ?? ""
        }).ToListAsync();
        return Ok(new { total = items.Count, applications = items });
    }

    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
    [HttpGet("applications/{id:int}/rooms")]
    public async Task<IActionResult> AvailableRooms(int id)
    {
        var a = await _context.Applications.AsNoTracking().FirstOrDefaultAsync(x => x.ApplicationID == id);
        if (a == null) return NotFound(new { message = "Application not found." });
        var rooms = await _context.Rooms.AsNoTracking().Where(r => r.AccommodationID == a.AccommodationID && r.Status == "Available")
            .OrderBy(r => r.RoomNumber).Select(r => new { roomId = r.RoomID, roomNumber = r.RoomNumber, roomType = r.RoomType, floor = r.Floor ?? "" }).ToListAsync();
        return Ok(new { applicationId = id, rooms });
    }

    public sealed class ApproveApplicationRequest { public int RoomId { get; set; } }
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
    [HttpPost("applications/{id:int}/approve")]
    public async Task<IActionResult> ApproveApplication(int id, [FromBody] ApproveApplicationRequest request)
    {
        if (id <= 0 || request.RoomId <= 0) return BadRequest(new { message = "Select a valid application and room." });
        var a = await _context.Applications.FirstOrDefaultAsync(x => x.ApplicationID == id);
        if (a == null) return NotFound(new { message = "Application not found." });
        if (a.Status != "Pending") return Conflict(new { message = $"Only pending applications can be approved; this one is {a.Status}." });
        var accommodation = await _context.Accommodations
            .Include(x => x.Rooms)
            .Include(x => x.AccommodationServices).ThenInclude(x => x.Service)
            .FirstOrDefaultAsync(x => x.AccommodationID == a.AccommodationID);
        var room = await _context.Rooms.FirstOrDefaultAsync(r => r.RoomID == request.RoomId);
        if (accommodation == null || room == null) return NotFound(new { message = "Accommodation or room could not be found." });
        if (room.AccommodationID != a.AccommodationID) return BadRequest(new { message = "The selected room does not belong to this accommodation." });
        if (!string.Equals(room.Status, "Available", StringComparison.OrdinalIgnoreCase)) return Conflict(new { message = "The selected room is no longer available." });
        a.Status = "Approved"; a.RoomID = room.RoomID; a.RejectionReason = null; room.Status = "Occupied";
        var configuredCharges = await _context.AccommodationCharges
            .Include(c => c.PaymentType)
            .Where(c => c.AccommodationID == accommodation.AccommodationID)
            .ToListAsync();
        a.ParkingFee = ParkingFeeCalculator.Calculate(
            accommodation, a.NumberOfVehicles, configuredCharges);
        var rooms = await _context.Rooms.Where(r => r.AccommodationID == accommodation.AccommodationID).ToListAsync();
        accommodation.TotalRooms = rooms.Count; accommodation.AvailableRooms = rooms.Count(r => r.RoomID != room.RoomID && string.Equals(r.Status, "Available", StringComparison.OrdinalIgnoreCase)); accommodation.Available = accommodation.AvailableRooms > 0;
        _context.Notifications.Add(new Notification
        {
            UserID = a.UserID,
            Title = "Accommodation Application Approved",
            Message = ParkingFeeCalculator.AddParkingNote(
                $"Your application has been approved. You have been allocated Room {room.RoomNumber} at {accommodation.Name}.",
                a.ParkingFee),
            DateCreated = DateTime.UtcNow
        });
        await _context.SaveChangesAsync(); return Ok(new { message = $"Application #{id} approved. Room {room.RoomNumber} has been allocated." });
    }

    public sealed class RejectApplicationRequest { public string? Reason { get; set; } }
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
    [HttpPost("applications/{id:int}/reject")]
    public async Task<IActionResult> RejectApplication(int id, [FromBody] RejectApplicationRequest request)
    {
        var reason = request.Reason?.Trim() ?? "";
        if (reason.Length == 0 || reason.Length > 1000) return BadRequest(new { message = "A rejection reason is required (maximum 1,000 characters)." });
        var a = await _context.Applications.Include(x => x.Accommodation).FirstOrDefaultAsync(x => x.ApplicationID == id);
        if (a == null) return NotFound(new { message = "Application not found." });
        if (a.Status != "Pending") return Conflict(new { message = "Only pending applications can be rejected." });
        a.Status = "Rejected"; a.RejectionReason = reason;
        _context.Notifications.Add(new Notification { UserID = a.UserID, Title = "Accommodation Application Update", Message = $"Your application for {a.Accommodation?.Name ?? "the accommodation"} was not approved. Reason: {reason}", DateCreated = DateTime.UtcNow });
        await _context.SaveChangesAsync(); return Ok(new { message = "Application rejected and customer notified." });
    }

    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [HttpGet("applications/{id:int}/documents/{type}")]
    public async Task<IActionResult> ApplicationDocument(int id, string type)
    {
        var application = await _context.Applications.AsNoTracking().FirstOrDefaultAsync(a => a.ApplicationID == id);
        if (application == null) return NotFound(new { message = "Application not found." });
        if (!IsAdmin && !User.IsInRole("FinancialOfficer"))
        {
            var me = await CurrentUserAsync(); if (me == null || me.Id != application.UserID) return Forbid();
        }
        byte[]? bytes; string name;
        switch ((type ?? "").ToLowerInvariant())
        {
            case "id": bytes = application.IDDocument; name = application.IDDocumentName ?? "identity.pdf"; break;
            case "registration": bytes = application.RegistrationDocument; name = application.RegistrationDocumentName ?? "registration.pdf"; break;
            case "employment": bytes = application.EmploymentDocument; name = application.EmploymentDocumentName ?? "employment.pdf"; break;
            case "funding": bytes = application.FundingDocument; name = application.FundingDocumentName ?? "funding.pdf"; break;
            default: return BadRequest(new { message = "Unknown document type." });
        }
        if (bytes == null || bytes.Length == 0) return NotFound(new { message = "That document is not available." });
        return File(bytes, "application/pdf", name);
    }

    // ---------------------------- notifications ----------------------------
    [HttpGet("notifications")]
    public async Task<IActionResult> Notifications()
    {
        var me = await CurrentUserAsync(); if (me == null) return Unauthorized();
        var items = await _context.Notifications.AsNoTracking().Where(n => n.UserID == me.Id).OrderByDescending(n => n.DateCreated).Take(100)
            .Select(n => new { notificationId = n.NotificationID, title = n.Title, message = n.Message, dateCreated = n.DateCreated, isRead = n.IsRead }).ToListAsync();
        return Ok(new { unread = items.Count(x => !x.isRead), notifications = items });
    }
}
