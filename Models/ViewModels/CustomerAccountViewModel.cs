using System.ComponentModel.DataAnnotations;

namespace UFSStayConnect.ViewModels
{
    public class CustomerAccountViewModel
    {
        public string Id { get; set; } = string.Empty;

        [Required, StringLength(100)]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string Surname { get; set; } = string.Empty;

        [Required, EmailAddress, StringLength(256)]
        [Display(Name = "Email Address")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Customer Type")]
        public string UserType { get; set; } = "Student";

        [Display(Name = "Student Number")]
        public string? StudentNumber { get; set; }

        [Display(Name = "Employee Number")]
        public string? EmployeeNumber { get; set; }

        [Phone]
        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }

        [Required]
        [Display(Name = "Account Status")]
        public string AccountStatus { get; set; } = "Active";

        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 12, ErrorMessage = "Password must be at least 12 characters.")]
        [Display(Name = "Temporary Password")]
        public string? Password { get; set; }

        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
        [Display(Name = "Confirm Password")]
        public string? ConfirmPassword { get; set; }
    }

    public class ResetCustomerPasswordViewModel
    {
        public string CustomerId { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;

        [Required, StringLength(100, MinimumLength = 12, ErrorMessage = "Password must be at least 12 characters.")]
        [DataType(DataType.Password)]
        [Display(Name = "New Temporary Password")]
        public string NewPassword { get; set; } = string.Empty;

        [Required, Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
