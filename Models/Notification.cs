using System.ComponentModel.DataAnnotations;

namespace UFSStayConnect.Models
{
    public class Notification
    {
        [Key]
        public int NotificationID { get; set; }

        public string UserID { get; set; } = string.Empty;

        [Required]
        [StringLength(150)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(1000)]
        public string Message { get; set; } = string.Empty;

        public DateTime DateCreated { get; set; } = DateTime.UtcNow;

        public bool IsRead { get; set; }

        /// <summary>Optional idempotency key used by automated payment reminders.
        /// Normal notifications can leave this null.</summary>
        [StringLength(200)]
        public string? ReminderKey { get; set; }

        public ApplicationUser? User { get; set; }
    }
}
