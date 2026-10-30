using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UFSStayConnect.Models
{
    /// <summary>
    /// Records actual usage of a metered accommodation service for pay-as-you-go billing.
    /// Configure this entity in AppDbContext and add a migration before using it.
    /// </summary>
    public class ServiceUsageRecord
    {
        [Key]
        public int ServiceUsageRecordID { get; set; }

        [Required]
        public int ApplicationID { get; set; }

        [Required]
        public int ServiceID { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime ReadingDate { get; set; } = DateTime.Today;

        [Range(typeof(decimal), "0", "999999999")]
        [Column(TypeName = "decimal(18,3)")]
        public decimal PreviousReading { get; set; }

        [Range(typeof(decimal), "0", "999999999")]
        [Column(TypeName = "decimal(18,3)")]
        public decimal CurrentReading { get; set; }

        [Range(typeof(decimal), "0", "999999999")]
        [Column(TypeName = "decimal(18,3)")]
        public decimal UnitsUsed { get; set; }

        [Range(typeof(decimal), "0", "9999999.99")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal RatePerUnit { get; set; }

        [Range(typeof(decimal), "0", "999999999.99")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal AmountDue { get; set; }

        [Required]
        [StringLength(450)]
        public string RecordedByUserID { get; set; } = string.Empty;

        [Required]
        [StringLength(30)]
        public string BillingStatus { get; set; } = "Unbilled";

        public DateTime BillingDueDate { get; set; } = DateTime.Today.AddDays(30);

        [StringLength(500)]
        public string? Notes { get; set; }

        public Application? Application { get; set; }
        public Service? Service { get; set; }
    }
}
