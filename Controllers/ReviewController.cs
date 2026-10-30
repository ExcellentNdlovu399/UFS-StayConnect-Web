using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UFSStayConnect.Data;
using UFSStayConnect.Models;
using UFSStayConnect.ViewModels;

namespace UFSStayConnect.Controllers
{
    [Authorize]
    public class ReviewController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        public ReviewController(AppDbContext context, UserManager<ApplicationUser> userManager) { _context = context; _userManager = userManager; }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (User.IsInRole("Admin") || User.IsInRole("FinancialOfficer"))
                return View("Management", await _context.Reviews.Include(r => r.User).Include(r => r.Accommodation).OrderByDescending(r => r.DateCreated).ToListAsync());
            var user = await _userManager.GetUserAsync(User); if (user == null) return Challenge();
            var approved = await _context.Applications.Include(a => a.Accommodation).FirstOrDefaultAsync(a => a.UserID == user.Id && a.Status == "Approved");
            ViewBag.ApprovedAccommodation = approved?.Accommodation;
            var reviews = await _context.Reviews.Include(r => r.Accommodation).Where(r => r.UserID == user.Id).OrderByDescending(r => r.DateCreated).ToListAsync();
            return View("Customer", reviews);
        }

        [Authorize(Roles = "Customer")]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ReviewViewModel model)
        {
            var user = await _userManager.GetUserAsync(User); if (user == null) return Challenge();
            var approved = await _context.Applications.FirstOrDefaultAsync(a => a.UserID == user.Id && a.AccommodationID == model.AccommodationID && a.Status == "Approved");
            if (approved == null) return Forbid();
            if (!ModelState.IsValid) return RedirectToAction(nameof(Index));
            var existing = await _context.Reviews.FirstOrDefaultAsync(r => r.UserID == user.Id && r.AccommodationID == model.AccommodationID);
            if (existing == null)
            {
                _context.Reviews.Add(new Review { UserID = user.Id, AccommodationID = model.AccommodationID, Rating = model.Rating, Comment = model.Comment?.Trim(), DateCreated = DateTime.UtcNow });
                TempData["SuccessMessage"] = "Thank you for your review.";
            }
            else
            {
                existing.Rating = model.Rating;
                existing.Comment = model.Comment?.Trim();
                existing.DateCreated = DateTime.UtcNow;
                TempData["SuccessMessage"] = "Your review was updated successfully.";
            }
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}
