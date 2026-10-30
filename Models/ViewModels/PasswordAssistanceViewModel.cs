using System.ComponentModel.DataAnnotations;

namespace UFSStayConnect.ViewModels
{
    public class PasswordAssistanceViewModel
    {
        [Required]
        [EmailAddress]
        [Display(Name = "UFS Email")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Student / Staff Number")]
        public string StudentOrStaffNumber { get; set; } = string.Empty;

        [StringLength(500)]
        [Display(Name = "Reason")]
        public string? Reason { get; set; }
    }
}