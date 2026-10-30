using System.ComponentModel.DataAnnotations;

namespace UFSStayConnect.Models
{
    public class PasswordResetRequest
    {
        [Key]
        public int PasswordResetRequestID { get; set; }

        [Required]
        public string UserID { get; set; } = string.Empty;

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = "Pending";

        [StringLength(500)]
        public string? Reason { get; set; }

        public DateTime RequestDate { get; set; } = DateTime.UtcNow;

        public DateTime? ProcessedDate { get; set; }

        public string? ProcessedByUserID { get; set; }

        public ApplicationUser? User { get; set; }

        public ApplicationUser? ProcessedByUser { get; set; }
    }
}