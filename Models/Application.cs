
using System.ComponentModel.DataAnnotations;

namespace UFSStayConnect.Models
{
    public class Application
    {
        [Key]
        public int ApplicationID { get; set; }

        public string UserID { get; set; } = string.Empty;

        public int AccommodationID { get; set; }

        public int? RoomID { get; set; }

        [Range(0, 10)]
        public int NumberOfVehicles { get; set; } = 0;

        // Calculated on the server after approval and successful room allocation.
        [Range(typeof(decimal), "0", "9999999.99")]
        public decimal ParkingFee { get; set; } = 0m;

        public DateTime? MoveInDate { get; set; }

        public DateTime ApplicationDate { get; set; } = DateTime.UtcNow;

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = "Pending";

        [StringLength(1000)]
        public string? RejectionReason { get; set; }

        public byte[]? IDDocument { get; set; }
        public byte[]? RegistrationDocument { get; set; }
        public byte[]? FundingDocument { get; set; }
        public byte[]? EmploymentDocument { get; set; }

        [StringLength(255)]
        public string? IDDocumentName { get; set; }

        [StringLength(255)]
        public string? RegistrationDocumentName { get; set; }

        [StringLength(255)]
        public string? FundingDocumentName { get; set; }

        [StringLength(255)]
        public string? EmploymentDocumentName { get; set; }

        public ApplicationUser? User { get; set; }
        public Accommodation? Accommodation { get; set; }
        public Room? Room { get; set; }
    }
}
