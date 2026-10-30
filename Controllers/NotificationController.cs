using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using UFSStayConnect.DataAccess;
using UFSStayConnect.Models;

namespace UFSStayConnect.Controllers
{
    [Authorize]
    public class NotificationController : Controller
    {
        private readonly IRepositoryWrapper _repository;
        private readonly UserManager<ApplicationUser> _userManager;
        public NotificationController(IRepositoryWrapper repository, UserManager<ApplicationUser> userManager) { _repository = repository; _userManager = userManager; }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            // Notifications are private to the signed-in account. Administrators
            // receive their own staff notifications just like Financial Officers.
            var notifications = _repository.Notification.FindByUser(user.Id).ToList();
            return User.IsInRole("Admin") ? View("AdminIndex", notifications) : View(notifications);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkRead(int id)
        {
            var notification = _repository.Notification.GetNotificationById(id); if (notification == null) return NotFound();
            if (!User.IsInRole("Admin"))
            {
                var user = await _userManager.GetUserAsync(User); if (user == null || notification.UserID != user.Id) return Forbid();
            }
            notification.IsRead = true; _repository.Notification.UpdateNotification(notification); _repository.Save();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllRead()
        {
            var user = await _userManager.GetUserAsync(User); if (user == null) return Challenge();
            var notifications = _repository.Notification.FindByUser(user.Id).Where(n => !n.IsRead).ToList();
            foreach (var n in notifications) { n.IsRead = true; _repository.Notification.UpdateNotification(n); }
            _repository.Save(); return RedirectToAction(nameof(Index));
        }
    }
}
