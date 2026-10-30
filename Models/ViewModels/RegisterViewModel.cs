using System.ComponentModel.DataAnnotations;

namespace UFSStayConnect.ViewModels
{
    public class RegisterViewModel : IValidatableObject
    {
        [Required]
        [StringLength(100)]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Surname { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [Display(Name = "Email Address")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [Display(Name = "User Type")]
        public string UserType { get; set; } = string.Empty;

        [Display(Name = "Student Number")]
        [RegularExpression(@"^\d{10}$", ErrorMessage = "Student number must contain exactly 10 digits.")]
        public string? StudentNumber { get; set; }

        [Display(Name = "Employee Number")]
        [StringLength(20, ErrorMessage = "Employee number cannot exceed 20 characters.")]
        [RegularExpression(@"^[A-Za-z0-9-]+$", ErrorMessage = "Employee number may contain letters, numbers and hyphens only.")]
        public string? EmployeeNumber { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 12, ErrorMessage = "Password must be at least 12 characters.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 12, ErrorMessage = "Password must be at least 12 characters.")]
        [DataType(DataType.Password)]
        [Compare("Password")]
        [Display(Name = "Confirm Password")]
        public string ConfirmPassword { get; set; } = string.Empty;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.Equals(UserType, "Student", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(StudentNumber))
                    yield return new ValidationResult("Student number is required for student registration.", new[] { nameof(StudentNumber) });
                else if (!System.Text.RegularExpressions.Regex.IsMatch(StudentNumber, @"^\d{10}$"))
                    yield return new ValidationResult("Student number must contain exactly 10 digits.", new[] { nameof(StudentNumber) });
            }
            else if (string.Equals(UserType, "Staff", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(EmployeeNumber))
                    yield return new ValidationResult("Employee number is required for staff registration.", new[] { nameof(EmployeeNumber) });
            }
            else
            {
                yield return new ValidationResult("Select Student or Staff as the user type.", new[] { nameof(UserType) });
            }
        }
    }
}