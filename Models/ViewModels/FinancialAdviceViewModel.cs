using System.ComponentModel.DataAnnotations;

namespace UFSStayConnect.ViewModels
{
    public class FinancialAdviceViewModel
    {
        public int FinancialAdviceID { get; set; }

        [Required, StringLength(150)]
        public string Title { get; set; } = string.Empty;

        [Required, StringLength(2000)]
        [Display(Name = "Advice / Input")]
        public string Advice { get; set; } = string.Empty;

        public string Status { get; set; } = "Open";

        [StringLength(1000)]
        [Display(Name = "Administrator Response")]
        public string? AdminResponse { get; set; }
    }
}
