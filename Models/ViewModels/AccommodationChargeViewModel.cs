using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace UFSStayConnect.ViewModels
{
    public class AccommodationChargeViewModel
    {
        public int AccommodationChargeID { get; set; }

        [Required]
        [Display(Name = "Residence")]
        public int AccommodationID { get; set; }

        [Required]
        [Display(Name = "Charge Type")]
        public int PaymentTypeID { get; set; }

        [Required]
        [Range(0.01, 9999999.99)]
        [Display(Name = "Amount")]
        public decimal Amount { get; set; }

        [Required]
        [StringLength(30)]
        [Display(Name = "Billing Frequency")]
        public string Frequency { get; set; } = "Monthly";

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "First Due Date")]
        public DateTime DueDate { get; set; } = DateTime.Today.AddMonths(1);

        [Range(0, 31)]
        [Display(Name = "Reminder Days Before Due Date")]
        public int ReminderDaysBeforeDue { get; set; } = 3;

        [Range(0, 31)]
        [Display(Name = "Reminder Days After Due Date")]
        public int ReminderDaysAfterDue { get; set; } = 3;

        [StringLength(500)]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        public IEnumerable<SelectListItem> Accommodations { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> PaymentTypes { get; set; } = new List<SelectListItem>();
    }
}
