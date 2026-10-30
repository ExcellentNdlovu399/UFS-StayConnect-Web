using System.ComponentModel.DataAnnotations;

namespace UFSStayConnect.Models
{
    public class PaymentType
    {
        [Key]
        public int PaymentTypeID { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        public ICollection<Payment> Payments { get; set; } = new List<Payment>();

        public ICollection<AccommodationCharge> AccommodationCharges { get; set; }
            = new List<AccommodationCharge>();
    }
}