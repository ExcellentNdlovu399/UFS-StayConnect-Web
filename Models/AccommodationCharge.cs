using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UFSStayConnect.Models
{
    public class AccommodationCharge
    {
        [Key]
        public int AccommodationChargeID { get; set; }

        [Required]
        public int AccommodationID { get; set; }

        [Required]
        public int PaymentTypeID { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Required]
        [StringLength(30)]
        public string Frequency { get; set; } = "Monthly";

        /// <summary>
        /// The first due date for this charge. Recurring due dates are calculated
        /// from this date according to the configured billing frequency.
        /// </summary>
        public DateTime? DueDate { get; set; }

        [Range(0, 31)]
        public int ReminderDaysBeforeDue { get; set; } = 3;

        [Range(0, 31)]
        public int ReminderDaysAfterDue { get; set; } = 3;

        [StringLength(500)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        public Accommodation Accommodation { get; set; } = null!;

        public PaymentType PaymentType { get; set; } = null!;
    }
}
