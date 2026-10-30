using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UFSStayConnect.Models
{
    public class Payment
    {
        [Key]
        public int PaymentID { get; set; }

        [Required]
        public string UserID { get; set; } = string.Empty;

        [Required]
        public int PaymentTypeID { get; set; }

        public int? ApplicationID { get; set; }

        public int? AccommodationChargeID { get; set; }

        /// <summary>
        /// Due date for the billing cycle represented by this payment.
        /// Capturing it on the payment prevents an old payment from being
        /// mistaken for payment of a newer recurring charge.
        /// </summary>
        public DateTime? DueDate { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Required]
        [StringLength(50)]
        public string PaymentStatus { get; set; } = "Pending";

        [Required]
        [StringLength(30)]
        public string PaymentMethod { get; set; } = "EFT";

        [Required]
        [StringLength(100)]
        public string ReferenceNumber { get; set; } = string.Empty;

        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

        [StringLength(500)]
        public string? Description { get; set; }

        [StringLength(100)]
        public string? PaymentBatchReference { get; set; }

        // Relative path to an uploaded EFT proof document. Card data is never stored.
        [StringLength(500)]
        public string? ProofOfPaymentPath { get; set; }

        public ApplicationUser User { get; set; } = null!;
        public PaymentType PaymentType { get; set; } = null!;
        public Application? Application { get; set; }
        public AccommodationCharge? AccommodationCharge { get; set; }
        public int? CustomerChargeID { get; set; }
        public CustomerCharge? CustomerCharge { get; set; }
    }
}
