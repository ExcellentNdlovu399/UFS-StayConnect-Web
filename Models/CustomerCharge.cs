using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UFSStayConnect.Models;

public class CustomerCharge
{
    [Key] public int CustomerChargeID { get; set; }
    [Required] public string UserID { get; set; } = "";
    [Required] public int ApplicationID { get; set; }
    [Required] public int AccommodationID { get; set; }
    [Required] public int PaymentTypeID { get; set; }
    [Required, StringLength(120)] public string Title { get; set; } = "";
    [StringLength(600)] public string? Description { get; set; }
    [Column(TypeName = "decimal(18,2)"), Range(typeof(decimal), "0.01", "99999999")] public decimal Amount { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public DateTime DueDate { get; set; } = DateTime.UtcNow.Date.AddDays(14);
    public bool IsActive { get; set; } = true;
    [StringLength(500)] public string? AdminNote { get; set; }
    public ApplicationUser? User { get; set; }
    public Application? Application { get; set; }
    public Accommodation? Accommodation { get; set; }
    public PaymentType? PaymentType { get; set; }
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
