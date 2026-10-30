
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;

namespace UFSStayConnect.ViewModels
{
    public class ApplicationViewModel : IValidatableObject
    {
        public int AccommodationID { get; set; }

        // Personal information
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Student or staff number is required.")]
        [StringLength(20, ErrorMessage = "Number cannot exceed 20 characters.")]
        [Display(Name = "Student / Staff Number")]
        public string StudentOrStaffNumber { get; set; } = string.Empty;

        [Display(Name = "Email Address")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Display(Name = "User Type")]
        public string UserType { get; set; } = string.Empty;

        // Vehicles
        [Range(0, 10, ErrorMessage = "Select between 0 and 10 cars.")]
        [Display(Name = "Number of Cars")]
        public int NumberOfVehicles { get; set; } = 0;

        // Display only; the controller must calculate the actual fee.
        [Display(Name = "Estimated Parking Fee")]
        public decimal ParkingFee { get; set; } = 0m;

        // Documents
        [Required(ErrorMessage = "Certified copy of ID is required.")]
        [Display(Name = "Certified Copy of ID")]
        public IFormFile? IDDocument { get; set; }

        [Display(Name = "Proof of Registration")]
        public IFormFile? RegistrationDocument { get; set; }

        [Display(Name = "Proof of Employment")]
        public IFormFile? EmploymentDocument { get; set; }

        // Retained for compatibility with older forms.
        // Funding documentation is not required for new applications.
        public IFormFile? FundingDocument { get; set; }

        // Declaration
        [Display(Name = "Application Declaration")]
        public bool DeclarationAccepted { get; set; }

        public IEnumerable<ValidationResult> Validate(
            ValidationContext validationContext)
        {
            if (!DeclarationAccepted)
            {
                yield return new ValidationResult(
                    "You must accept the application declaration before submitting.",
                    new[] { nameof(DeclarationAccepted) });
            }

            if (NumberOfVehicles < 0 || NumberOfVehicles > 10)
            {
                yield return new ValidationResult(
                    "The number of cars must be between 0 and 10.",
                    new[] { nameof(NumberOfVehicles) });
            }

            if (string.Equals(UserType, "Student",
                StringComparison.OrdinalIgnoreCase))
            {
                if (!Regex.IsMatch(
                    StudentOrStaffNumber ?? string.Empty, @"^\d{10}$"))
                {
                    yield return new ValidationResult(
                        "Student number must contain exactly 10 digits.",
                        new[] { nameof(StudentOrStaffNumber) });
                }

                if (RegistrationDocument == null ||
                    RegistrationDocument.Length == 0)
                {
                    yield return new ValidationResult(
                        "Proof of registration is required for students.",
                        new[] { nameof(RegistrationDocument) });
                }
            }
            else if (string.Equals(UserType, "Staff",
                StringComparison.OrdinalIgnoreCase))
            {
                if (!Regex.IsMatch(
                    StudentOrStaffNumber ?? string.Empty,
                    @"^[A-Za-z0-9-]{1,20}$"))
                {
                    yield return new ValidationResult(
                        "Enter a valid staff number.",
                        new[] { nameof(StudentOrStaffNumber) });
                }

                if (EmploymentDocument == null ||
                    EmploymentDocument.Length == 0)
                {
                    yield return new ValidationResult(
                        "Proof of employment is required for staff.",
                        new[] { nameof(EmploymentDocument) });
                }
            }
            else
            {
                yield return new ValidationResult(
                    "The applicant type could not be determined.",
                    new[] { nameof(UserType) });
            }
        }
    }
}
