using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace UFSStayConnect.ViewModels
{
    public class AccommodationViewModel
    {
        public int AccommodationID { get; set; }

        [Required(
            ErrorMessage = "Accommodation name is required.")]
        [StringLength(
            100,
            ErrorMessage =
                "Accommodation name cannot exceed 100 characters.")]
        public string Name { get; set; } = string.Empty;


        [Required(
            ErrorMessage = "Location is required.")]
        [StringLength(
            150,
            ErrorMessage =
                "Location cannot exceed 150 characters.")]
        public string Location { get; set; } = string.Empty;


        [Required(
            ErrorMessage = "Accommodation type is required.")]
        [RegularExpression(
            "^(Flat|Townhouse)$",
            ErrorMessage =
                "Accommodation type must be Flat or Townhouse.")]
        public string Type { get; set; } = string.Empty;


        [Required(
            ErrorMessage = "Description is required.")]
        [StringLength(
            1000,
            MinimumLength = 10,
            ErrorMessage =
                "Description must contain between 10 and 1000 characters.")]
        public string Description { get; set; } = string.Empty;


        [Required(
            ErrorMessage = "Monthly rent is required.")]
        [Range(
            0.01,
            1000000,
            ErrorMessage =
                "Monthly rent must be greater than R0 and cannot exceed R1,000,000.")]
        public decimal MonthlyRent { get; set; }


        [Required(
            ErrorMessage = "Number of bedrooms is required.")]
        [Range(
            1,
            500,
            ErrorMessage =
                "Bedrooms must be between 1 and 500.")]
        public int Bedrooms { get; set; }


        [Required(
            ErrorMessage = "Number of bathrooms is required.")]
        [Range(
            1,
            200,
            ErrorMessage =
                "Bathrooms must be between 1 and 200.")]
        public int Bathrooms { get; set; }

        [Required(
     ErrorMessage = "Total number of rooms is required.")]
        [Range(
     1,
     100,
     ErrorMessage = "Total rooms must be between 1 and 100.")]
        public int TotalRooms { get; set; }

        public int AvailableRooms { get; set; }


        // Image selected from the user's computer
        public IFormFile? ImageFile { get; set; }


        // Existing image path when editing
        public string? ExistingImagePath { get; set; }


        public bool Available { get; set; } = true;
    }
}