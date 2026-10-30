using System.ComponentModel.DataAnnotations;

namespace UFSStayConnect.Models
{
    public class Review
    {
        [Key]
        public int ReviewID { get; set; }

        public string UserID { get; set; } = string.Empty;

        public int AccommodationID { get; set; }

        [Required]
        [Range(1, 5)]
        public int Rating { get; set; }

        [StringLength(1000)]
        public string? Comment { get; set; }

        public DateTime DateCreated { get; set; } = DateTime.UtcNow;

        public ApplicationUser? User { get; set; }

        public Accommodation? Accommodation { get; set; }
    }
}