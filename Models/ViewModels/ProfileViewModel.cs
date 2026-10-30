using System.ComponentModel.DataAnnotations;

namespace UFSStayConnect.ViewModels
{
    public class ProfileViewModel
    {
        [Required]
        [StringLength(100)]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        [Display(Name = "Surname")]
        public string Surname { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [Display(Name = "Email Address")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [Display(Name = "User Type")]
        public string UserType { get; set; } = string.Empty;

        [Display(Name = "Student Number")]
        public string? StudentNumber { get; set; }

        [Display(Name = "Employee Number")]
        public string? EmployeeNumber { get; set; }

        [Phone]
        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }
    }
}