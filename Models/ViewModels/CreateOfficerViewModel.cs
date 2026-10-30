using System.ComponentModel.DataAnnotations;

namespace UFSStayConnect.ViewModels
{
    public class CreateOfficerViewModel
    {
        [Required(ErrorMessage = "First name is required.")]
        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;


        [Required(ErrorMessage = "Surname is required.")]
        [StringLength(100)]
        public string Surname { get; set; } = string.Empty;


        [Required(ErrorMessage = "Email address is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        public string Email { get; set; } = string.Empty;


        [Required(ErrorMessage = "Employee number is required.")]
        public string EmployeeNumber { get; set; } = string.Empty;


        [Required(ErrorMessage = "Password is required.")]
        [StringLength(
            100,
            MinimumLength = 12,
            ErrorMessage = "Password must be at least 12 characters.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;


        [Required(ErrorMessage = "Please confirm the password.")]
        [Compare(
            "Password",
            ErrorMessage = "Passwords do not match.")]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}