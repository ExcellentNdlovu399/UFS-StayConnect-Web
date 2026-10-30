using System.ComponentModel.DataAnnotations;

namespace UFSStayConnect.Models
{
    public class FinancialAdvice
    {
        [Key]
        public int FinancialAdviceID { get; set; }

        [Required, StringLength(450)]
        public string OfficerUserID { get; set; } = string.Empty;

        [Required, StringLength(150)]
        public string Title { get; set; } = string.Empty;

        [Required, StringLength(2000)]
        public string Advice { get; set; } = string.Empty;

        [Required, StringLength(30)]
        public string Status { get; set; } = "Open";

        [StringLength(1000)]
        public string? AdminResponse { get; set; }

        public DateTime DateCreated { get; set; } = DateTime.UtcNow;
        public DateTime? DateReviewed { get; set; }

        public ApplicationUser? Officer { get; set; }
    }
}
