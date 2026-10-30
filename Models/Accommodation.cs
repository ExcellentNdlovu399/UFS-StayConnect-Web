using System.ComponentModel.DataAnnotations;

namespace UFSStayConnect.Models
{
    public class Accommodation
    {
        [Key]
        public int AccommodationID { get; set; }

        [Required]
        [StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(150)]
        public string Location { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Type { get; set; } = string.Empty;

        [Required]
        [StringLength(1000)]
        public string Description { get; set; } = string.Empty;

        [Range(0, 1000000)]
        public decimal MonthlyRent { get; set; }

        // Number of bedrooms
        [Range(1, 500)]
        public int Bedrooms { get; set; }

        // Number of bathrooms
        [Range(1, 200)]
        public int Bathrooms { get; set; }

        [Range(1, 100)]
        public int TotalRooms { get; set; }

        [Range(0, 100)]
        public int AvailableRooms { get; set; }

        // Stores the path of the uploaded image
        public string? ImagePath { get; set; }

        public bool Available { get; set; } = true;

        public DateTime DateAdded { get; set; } = DateTime.UtcNow;

        public ICollection<AccommodationService> AccommodationServices { get; set; }
            = new List<AccommodationService>();

        public ICollection<Application> Applications { get; set; }
            = new List<Application>();

        public ICollection<Review> Reviews { get; set; }
            = new List<Review>();

        public ICollection<AccommodationCharge> AccommodationCharges { get; set; }
            = new List<AccommodationCharge>();

        public ICollection<Room> Rooms { get; set; }
            = new List<Room>();
    }
}