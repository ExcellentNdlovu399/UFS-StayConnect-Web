using System.ComponentModel.DataAnnotations;

namespace UFSStayConnect.ViewModels
{
    public class RecordServiceUsageViewModel
    {
        [Required] public int ApplicationID { get; set; }
        [Required] public int ServiceID { get; set; }
        [Display(Name = "Previous meter reading")]
        [Range(typeof(decimal), "0", "999999999")]
        public decimal PreviousReading { get; set; }
        [Display(Name = "Current meter reading")]
        [Range(typeof(decimal), "0", "999999999")]
        public decimal CurrentReading { get; set; }
        [Display(Name = "Billing due date")]
        [DataType(DataType.Date)]
        public DateTime BillingDueDate { get; set; } = DateTime.Today.AddDays(30);
        [StringLength(500)] public string? Notes { get; set; }
    }
}
