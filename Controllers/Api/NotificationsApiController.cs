using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using UFSStayConnect.DataAccess;
using UFSStayConnect.Models;

namespace UFSStayConnect.Controllers.Api
{
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [Route("api/notifications")]
    public class NotificationsApiController : ControllerBase
    {
        private readonly IRepositoryWrapper _repository;
        private readonly UserManager<ApplicationUser> _userManager;

        public NotificationsApiController(
            IRepositoryWrapper repository,
            UserManager<ApplicationUser> userManager)
        {
            _repository = repository;
            _userManager = userManager;
        }

        // GET api/notifications
        // Returns a bare JSON array (newest first), which is what the Android app expects.
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var items = _repository.Notification
                .FindByUser(user.Id)
                .OrderByDescending(n => n.DateCreated)
                .ToList()
                .Select(ToDto)
                .ToList();

            return Ok(items);
        }

        // GET api/notifications/summary
        [HttpGet("summary")]
        public async Task<IActionResult> Summary()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var all = _repository.Notification.FindByUser(user.Id).ToList();
            var unread = all.Count(n => !n.IsRead);

            return Ok(new
            {
                total = all.Count,
                unread,
                read = all.Count - unread
            });
        }

        // PUT api/notifications/{id}/read
        [HttpPut("{id:int}/read")]
        public async Task<IActionResult> MarkRead(int id)
        {
            var notification = _repository.Notification.GetNotificationById(id);
            if (notification == null) return NotFound(new { message = "Notification not found." });

            if (!User.IsInRole("Admin"))
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return Unauthorized();
                if (notification.UserID != user.Id) return Forbid();
            }

            if (!notification.IsRead)
            {
                notification.IsRead = true;
                _repository.Notification.UpdateNotification(notification);
                _repository.Save();
            }

            return Ok(new { id, isRead = true });
        }

        // PUT api/notifications/read-all
        [HttpPut("read-all")]
        public async Task<IActionResult> MarkAllRead()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var unread = _repository.Notification
                .FindByUser(user.Id)
                .Where(n => !n.IsRead)
                .ToList();

            foreach (var n in unread)
            {
                n.IsRead = true;
                _repository.Notification.UpdateNotification(n);
            }

            if (unread.Count > 0) _repository.Save();

            return Ok(new { updated = unread.Count });
        }

        // ---- Mapping -------------------------------------------------------

        private static object ToDto(Notification n) => new
        {
            id = n.NotificationID,
            title = n.Title,
            message = n.Message,
            createdAt = DateTime.SpecifyKind(n.DateCreated, DateTimeKind.Utc)
                .ToString("o"),                       // ISO-8601, e.g. 2026-10-20T08:15:00.0000000Z
            isRead = n.IsRead,
            type = InferType(n),
            isImportant = InferImportant(n)
        };

        // Your web Notification model has no Type / IsImportant columns, so these are
        // derived from the text. If you add real columns, replace these two methods.
        private static string InferType(Notification n)
        {
            var t = $"{n.Title} {n.Message}".ToLowerInvariant();
            if (t.Contains("payment") || t.Contains("invoice") || t.Contains("paid")) return "payment";
            if (t.Contains("application")) return "application";
            if (t.Contains("accommodation") || t.Contains("room") || t.Contains("residence")) return "accommodation";
            if (t.Contains("document")) return "document";
            if (t.Contains("parking")) return "parking";
            return "general";
        }

        private static bool InferImportant(Notification n)
        {
            var t = $"{n.Title} {n.Message}".ToLowerInvariant();
            return t.Contains("overdue") || t.Contains("rejected") || t.Contains("approved")
                || t.Contains("urgent") || t.Contains("action required") || t.Contains("failed");
        }
    }
}