using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using UFSStayConnect.Data;
using UFSStayConnect.Models;

namespace UFSStayConnect.Controllers.Api
{
    /// <summary>
    /// Mobile API for customer reviews. Mirrors the web ReviewController (Customer view):
    ///   GET  api/reviews   approved residence, the customer's current review, review history
    ///   POST api/reviews   create or update the review for the approved residence
    /// </summary>
    [ApiController]
    [Route("api/reviews")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Customer")]
    public class ReviewsController : ControllerBase
    {
        private const int MaxCommentLength = 1000;

        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<ReviewsController> _logger;

        public ReviewsController(
            AppDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<ReviewsController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        public class SaveReviewRequest
        {
            public int AccommodationId { get; set; }
            public int Rating { get; set; }
            public string? Comment { get; set; }
        }

        // =========================================================
        // GET api/reviews
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var user = await GetUserAsync();
            if (user == null) return Unauthorized(new { message = "Please sign in again." });

            var approved = await _context.Applications
                .AsNoTracking()
                .Include(a => a.Accommodation)
                .FirstOrDefaultAsync(a => a.UserID == user.Id && a.Status == "Approved");

            var reviews = await _context.Reviews
                .AsNoTracking()
                .Include(r => r.Accommodation)
                .Where(r => r.UserID == user.Id)
                .OrderByDescending(r => r.DateCreated)
                .ToListAsync();

            var accommodation = approved?.Accommodation;

            var mine = accommodation == null
                ? null
                : reviews.FirstOrDefault(r => r.AccommodationID == accommodation.AccommodationID);

            return Ok(new
            {
                hasApprovedAccommodation = accommodation != null,
                accommodationId = accommodation?.AccommodationID ?? 0,
                accommodationName = accommodation?.Name ?? "",
                hasReview = mine != null,
                myRating = mine?.Rating ?? 0,
                myComment = mine?.Comment ?? "",
                reviews = reviews.Select(r => new
                {
                    accommodationId = r.AccommodationID,
                    accommodationName = r.Accommodation?.Name ?? "Residence",
                    rating = r.Rating,
                    comment = r.Comment ?? "",
                    dateCreated = AsUtc(r.DateCreated)
                }).ToList()
            });
        }

        // =========================================================
        // POST api/reviews
        // =========================================================

        [HttpPost]
        public async Task<IActionResult> Save([FromBody] SaveReviewRequest request)
        {
            var user = await GetUserAsync();
            if (user == null) return Unauthorized(new { message = "Please sign in again." });

            if (request == null)
                return BadRequest(new { message = "Invalid review." });

            if (request.Rating < 1 || request.Rating > 5)
                return BadRequest(new { message = "Please select a rating between 1 and 5." });

            var comment = request.Comment?.Trim();

            if (comment != null && comment.Length > MaxCommentLength)
                return BadRequest(new { message = "Your review cannot be longer than 1,000 characters." });

            // Only customers with an approved application for this residence may review it.
            var approved = await _context.Applications.AnyAsync(a =>
                a.UserID == user.Id &&
                a.AccommodationID == request.AccommodationId &&
                a.Status == "Approved");

            if (!approved)
            {
                return StatusCode(403, new
                {
                    message = "You can only review a residence once your application has been approved."
                });
            }

            try
            {
                var existing = await _context.Reviews.FirstOrDefaultAsync(r =>
                    r.UserID == user.Id && r.AccommodationID == request.AccommodationId);

                string message;

                if (existing == null)
                {
                    _context.Reviews.Add(new Review
                    {
                        UserID = user.Id,
                        AccommodationID = request.AccommodationId,
                        Rating = request.Rating,
                        Comment = comment,
                        DateCreated = DateTime.UtcNow
                    });
                    message = "Thank you for your review.";
                }
                else
                {
                    existing.Rating = request.Rating;
                    existing.Comment = comment;
                    existing.DateCreated = DateTime.UtcNow;
                    message = "Your review was updated successfully.";
                }

                await _context.SaveChangesAsync();

                return Ok(new { message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save review for user {UserId}", user.Id);
                return StatusCode(500, new { message = "Your review could not be saved. Please try again." });
            }
        }

        // =========================================================
        // HELPERS
        // =========================================================

        private async Task<ApplicationUser?> GetUserAsync()
        {
            var id = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            return id == null
                ? await _userManager.GetUserAsync(User)
                : await _userManager.FindByIdAsync(id);
        }

        private static DateTime AsUtc(DateTime value) =>
            DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }
}
