using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using UFSStayConnect.DataAccess;
using UFSStayConnect.Models;

namespace UFSStayConnect.Controllers.Api
{
    /// <summary>Small helpers shared by the API controllers.</summary>
    public static class ApiHelpers
    {
        public const string Bearer = JwtBearerDefaults.AuthenticationScheme;

        // Same image the web "My Stay" page uses for approved accommodations.
        public const string FallbackImage = "/images/accommodation/accommodation-10.jpg";

        private static readonly string[] ImagePropertyNames =
        {
            "ImagePath", "ImageUrl", "ImageURL", "Image", "MainImage",
            "PhotoPath", "ImageFileName", "FeaturedImage"
        };

        /// <summary>Always returns an ABSOLUTE url the phone can download.</summary>
        public static string ImagePath(HttpRequest request, Accommodation? accommodation)
        {
            string path = FallbackImage;

            if (accommodation != null)
            {
                foreach (var name in ImagePropertyNames)
                {
                    var prop = typeof(Accommodation).GetProperty(name);
                    if (prop == null || prop.PropertyType != typeof(string)) continue;

                    var value = (prop.GetValue(accommodation) as string)?.Trim();
                    if (string.IsNullOrEmpty(value)) continue;

                    if (value.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                        return value;

                    if (value.StartsWith("~/")) value = value.Substring(1);

                    path = value.StartsWith("/") ? value : "/images/accommodation/" + value;
                    break;
                }
            }

            return $"{request.Scheme}://{request.Host}{path}";
        }

        public static string Iso(DateTime date) =>
            DateTime.SpecifyKind(date, DateTimeKind.Utc).ToString("yyyy-MM-dd'T'HH:mm:ss");

        public static bool Is(string? value, string expected) =>
            string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Finds the logged-in user even if the JWT claim types are not mapped
        /// the way GetUserAsync expects.
        /// </summary>
        public static async Task<ApplicationUser?> ResolveUserAsync(
            UserManager<ApplicationUser> userManager, ClaimsPrincipal principal)
        {
            var user = await userManager.GetUserAsync(principal);
            if (user != null) return user;

            var id = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? principal.FindFirstValue("sub")
                     ?? principal.FindFirstValue("nameid");
            if (!string.IsNullOrEmpty(id))
            {
                user = await userManager.FindByIdAsync(id);
                if (user != null) return user;
            }

            var email = principal.FindFirstValue(ClaimTypes.Email)
                        ?? principal.FindFirstValue("email");
            if (!string.IsNullOrEmpty(email))
            {
                user = await userManager.FindByEmailAsync(email);
                if (user != null) return user;
            }

            var name = principal.Identity?.Name;
            if (!string.IsNullOrEmpty(name))
                user = await userManager.FindByNameAsync(name);

            return user;
        }

        /// <summary>Reloads an application with its Accommodation and Room.</summary>
        public static Application Hydrate(IRepositoryWrapper repository, Application application)
        {
            var full = repository.Application.GetApplicationById(application.ApplicationID)
                       ?? application;

            if (full.Accommodation == null)
            {
                full.Accommodation = repository.Accommodation
                    .GetAccommodationById(full.AccommodationID);
            }

            return full;
        }
    }

    [ApiController]
    [Route("api/dashboard")]
    [Authorize(AuthenticationSchemes = ApiHelpers.Bearer)]
    public class ApiDashboardController : ControllerBase
    {
        private readonly IRepositoryWrapper _repository;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<ApiDashboardController> _logger;

        public ApiDashboardController(
            IRepositoryWrapper repository,
            UserManager<ApplicationUser> userManager,
            ILogger<ApiDashboardController> logger)
        {
            _repository = repository;
            _userManager = userManager;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            try
            {
                var user = await ApiHelpers.ResolveUserAsync(_userManager, User);
                if (user == null) return Unauthorized(new { message = "User not found for this token." });

                var applications = _repository.Application
                    .FindByUser(user.Id)
                    .ToList();

                var approved = applications
                    .Where(a => ApiHelpers.Is(a.Status, "Approved") && a.RoomID.HasValue)
                    .OrderByDescending(a => a.ApplicationDate)
                    .Select(a => ApiHelpers.Hydrate(_repository, a))
                    .ToList();

                var items = approved.Select(a => new
                {
                    applicationId = a.ApplicationID,
                    accommodationId = a.AccommodationID,
                    accommodationName = a.Accommodation?.Name
                                        ?? $"Accommodation #{a.AccommodationID}",
                    location = a.Accommodation?.Location ?? "",
                    type = a.Accommodation?.Type ?? "",
                    roomNumber = a.Room?.RoomNumber ?? "",
                    status = a.Status,
                    imagePath = ApiHelpers.ImagePath(Request, a.Accommodation)
                }).ToList();

                var first = (user.FirstName ?? "").Trim();
                var surname = (user.Surname ?? "").Trim();

                var initials =
                    (first.Length > 0 ? first.Substring(0, 1).ToUpperInvariant() : "") +
                    (surname.Length > 0 ? surname.Substring(0, 1).ToUpperInvariant() : "");

                var unread = _repository.Notification
                    .FindByUser(user.Id)
                    .Count(n => !n.IsRead);

                return Ok(new
                {
                    firstName = first,
                    surname,
                    fullName = $"{first} {surname}".Trim(),
                    initials,
                    userType = user.UserType,
                    avatarPath = "",
                    unreadNotifications = unread,
                    totalApplications = applications.Count,
                    pendingApplications = applications.Count(a => ApiHelpers.Is(a.Status, "Pending")),
                    approvedCount = items.Count,
                    allocation = items.FirstOrDefault(),
                    approvedAccommodations = items
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading API dashboard.");
                return StatusCode(500, new { message = "Unable to load your dashboard." });
            }
        }
    }
}