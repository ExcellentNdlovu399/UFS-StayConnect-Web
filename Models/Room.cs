using System.ComponentModel.DataAnnotations;

namespace UFSStayConnect.Models
{
    public class Room
    {
        [Key]
        public int RoomID { get; set; }

        [Required]
        public int AccommodationID { get; set; }

        [Required]
        [StringLength(30)]
        public string RoomNumber { get; set; } = string.Empty;

        [StringLength(30)]
        public string? Floor { get; set; }

        [Required]
        [StringLength(50)]
        public string RoomType { get; set; } = "Standard";

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = "Available";

        [StringLength(500)]
        public string? Description { get; set; }

        public Accommodation Accommodation { get; set; } = null!;

        public ICollection<Application> Applications { get; set; } = new List<Application>();
    }
}
