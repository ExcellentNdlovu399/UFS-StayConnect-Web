using System.ComponentModel.DataAnnotations;

namespace UFSStayConnect.ViewModels
{
    public class PaymentRejectionViewModel
    {
        public int PaymentID { get; set; }

        [Required]
        [Display(Name = "Reason") ]
        public string Reason { get; set; } = string.Empty;

        [StringLength(500)]
        [Display(Name = "Additional explanation") ]
        public string? Explanation { get; set; }
    }
}
