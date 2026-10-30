using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using UFSStayConnect.Models;

namespace UFSStayConnect.Controllers.Api
{
    [ApiController]
    [Route("api/account")]
    public class ApiAccountController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IConfiguration _configuration;

        public ApiAccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            IConfiguration configuration)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _configuration = configuration;
        }

        // POST: /api/account/register
        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            var firstName = request.FirstName.Trim();
            var surname = request.Surname.Trim();
            var email = request.Email.Trim();
            var userType = request.UserType.Trim();

            if (!userType.Equals("Student", StringComparison.OrdinalIgnoreCase) &&
                !userType.Equals("Staff", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Choose either Student or Staff." });
            }

            var atIndex = email.LastIndexOf('@');
            if (atIndex < 1)
            {
                return BadRequest(new { message = "Enter a valid UFS email address." });
            }

            var domain = email[(atIndex + 1)..];

            if (!domain.Equals("ufs.ac.za", StringComparison.OrdinalIgnoreCase) &&
                !domain.Equals("ufs4life.ac.za", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    message = "Use an @ufs.ac.za or @ufs4life.ac.za email address."
                });
            }

            var student = userType.Equals("Student", StringComparison.OrdinalIgnoreCase);

            var number = student
                ? request.StudentNumber?.Trim()
                : request.EmployeeNumber?.Trim();

            if (string.IsNullOrWhiteSpace(number) || !Regex.IsMatch(number, @"^[0-9]{10}$"))
            {
                return BadRequest(new
                {
                    message = student
                        ? "Student number must contain exactly 10 digits."
                        : "Employee number must contain exactly 10 digits."
                });
            }

            if (await _userManager.FindByEmailAsync(email) != null)
            {
                return Conflict(new { message = "An account with this email already exists." });
            }

            var numberExists = student
                ? await _userManager.Users.AnyAsync(u => u.StudentNumber == number)
                : await _userManager.Users.AnyAsync(u => u.EmployeeNumber == number);

            if (numberExists)
            {
                return Conflict(new
                {
                    message = student
                        ? "This student number is already registered."
                        : "This employee number is already registered."
                });
            }

            var roleName = student ? "Student" : "Staff";

            if (!await _roleManager.RoleExistsAsync("Customer") ||
                !await _roleManager.RoleExistsAsync(roleName))
            {
                return Problem(
                    title: "Account roles are not configured. Please contact the administrator.",
                    statusCode: StatusCodes.Status500InternalServerError);
            }

            var user = new ApplicationUser
            {
                FirstName = firstName,
                Surname = surname,
                Email = email,
                UserName = email,
                UserType = roleName,
                StudentNumber = student ? number : null,
                EmployeeNumber = student ? null : number,
                AccountStatus = "Active",
                DateCreated = DateTime.UtcNow,
                PasswordChangeRequired = false
            };

            var createResult = await _userManager.CreateAsync(user, request.Password);

            if (!createResult.Succeeded)
            {
                var errors = createResult.Errors.Select(e => e.Description).ToList();

                // "message" is what the Android app displays, so put the real reason in it
                // (for example: "Passwords must have at least one digit ('0'-'9').").
                return BadRequest(new
                {
                    message = string.Join(" ", errors),
                    errors
                });
            }

            var roleResult = await _userManager.AddToRolesAsync(
                user, new[] { "Customer", roleName });

            if (!roleResult.Succeeded)
            {
                await _userManager.DeleteAsync(user);

                return Problem(
                    title: "Unable to assign the account role.",
                    statusCode: StatusCodes.Status500InternalServerError);
            }

            // Create a JWT immediately so Android can authenticate the new account
            // and open the dashboard without requiring a second login.
            var key = _configuration["Jwt:Key"];
            var issuer = _configuration["Jwt:Issuer"];
            var audience = _configuration["Jwt:Audience"];

            if (string.IsNullOrWhiteSpace(key) ||
                string.IsNullOrWhiteSpace(issuer) ||
                string.IsNullOrWhiteSpace(audience))
            {
                // Avoid leaving a newly created account without a usable session.
                await _userManager.DeleteAsync(user);
                return Problem(
                    title: "Authentication configuration is incomplete.",
                    statusCode: StatusCodes.Status500InternalServerError);
            }

            var roles = await _userManager.GetRolesAsync(user);
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.UserName ?? user.Email ?? user.Id),
                new Claim(ClaimTypes.Email, user.Email ?? string.Empty)
            };

            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
            var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
            var expiresAt = DateTime.UtcNow.AddMinutes(60);
            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: expiresAt,
                signingCredentials: credentials);
            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            return StatusCode(StatusCodes.Status201Created, new
            {
                message = "Account created successfully.",
                accessToken = tokenString,
                tokenType = "Bearer",
                expiresAt,
                passwordChangeRequired = user.PasswordChangeRequired,
                user = new
                {
                    id = user.Id,
                    firstName = user.FirstName,
                    surname = user.Surname,
                    email = user.Email,
                    userType = user.UserType,
                    roles
                }
            });
        }

        // POST: /api/account/login
        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email.Trim());

            if (user == null)
            {
                return Unauthorized(new { message = "Invalid email or password." });
            }

            if (!string.Equals(user.AccountStatus, "Active", StringComparison.OrdinalIgnoreCase))
            {
                return Unauthorized(new { message = "This account is not active." });
            }

            var passwordResult = await _signInManager.CheckPasswordSignInAsync(
                user, request.Password, lockoutOnFailure: true);

            if (passwordResult.IsLockedOut)
            {
                return Unauthorized(new
                {
                    message = "This account is locked. Please try again later or contact the administrator."
                });
            }

            if (!passwordResult.Succeeded)
            {
                return Unauthorized(new { message = "Invalid email or password." });
            }

            var roles = await _userManager.GetRolesAsync(user);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.UserName ?? user.Email ?? user.Id),
                new Claim(ClaimTypes.Email, user.Email ?? string.Empty)
            };

            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var key = _configuration["Jwt:Key"];
            var issuer = _configuration["Jwt:Issuer"];
            var audience = _configuration["Jwt:Audience"];

            if (string.IsNullOrWhiteSpace(key) ||
                string.IsNullOrWhiteSpace(issuer) ||
                string.IsNullOrWhiteSpace(audience))
            {
                return Problem(
                    title: "Authentication configuration is incomplete.",
                    statusCode: StatusCodes.Status500InternalServerError);
            }

            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
            var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
            var expiresAt = DateTime.UtcNow.AddMinutes(60);

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: expiresAt,
                signingCredentials: credentials);

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            return Ok(new
            {
                accessToken = tokenString,
                tokenType = "Bearer",
                expiresAt,
                passwordChangeRequired = user.PasswordChangeRequired,
                user = new
                {
                    id = user.Id,
                    firstName = user.FirstName,
                    surname = user.Surname,
                    email = user.Email,
                    userType = user.UserType,
                    roles
                }
            });
        }

        // GET: /api/account/me
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized(new { message = "The token does not contain a user ID." });
            }

            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
                return NotFound(new { message = "Account not found." });

            if (!string.Equals(user.AccountStatus, "Active", StringComparison.OrdinalIgnoreCase))
            {
                return Unauthorized(new { message = "This account is not active." });
            }

            var roles = await _userManager.GetRolesAsync(user);

            return Ok(new
            {
                id = user.Id,
                firstName = user.FirstName,
                surname = user.Surname,
                email = user.Email,
                userType = user.UserType,
                accountStatus = user.AccountStatus,
                passwordChangeRequired = user.PasswordChangeRequired,
                roles
            });
        }
    }

    public class LoginRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }

    public class RegisterRequest
    {
        [Required]
        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Surname { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(8)]
        public string Password { get; set; } = string.Empty;

        [Required]
        public string UserType { get; set; } = string.Empty;

        public string? StudentNumber { get; set; }

        public string? EmployeeNumber { get; set; }
    }
}
