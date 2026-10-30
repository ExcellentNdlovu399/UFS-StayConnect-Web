using System.ComponentModel.DataAnnotations;

namespace UFSStayConnect.ViewModels
{
    public sealed class ResetOfficerPasswordViewModel
    {
        public string OfficerId { get; set; } = string.Empty;

        public string OfficerName { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 12, ErrorMessage = "The temporary password must be at least 12 characters.")]
        [DataType(DataType.Password)]
        [Display(Name = "New Temporary Password")]
        public string NewPassword { get; set; } = string.Empty;

        [Required]
        [Compare(nameof(NewPassword), ErrorMessage = "The passwords do not match.")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
