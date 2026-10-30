using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UFSStayConnect.Models;

public class Service
{
    [Key] public int ServiceID { get; set; }
    [Required, StringLength(100)] public string ServiceName { get; set; } = string.Empty;
    [StringLength(500)] public string? Description { get; set; }
    [Range(typeof(decimal), "0", "999999.99", ErrorMessage = "Rate must be zero or greater.")]
    [Column(TypeName = "decimal(18,2)")]
    [Display(Name = "Default rate per unit (R)")]
    public decimal RatePerUnit { get; set; }
    [Required, StringLength(30)]
    [Display(Name = "Charging method")]
    public string ChargingMethod { get; set; } = "PayAsYouGo"; // PayAsYouGo or Fixed
    [Required, StringLength(40)]
    [Display(Name = "Unit of measure")]
    public string UnitOfMeasure { get; set; } = "unit"; // kWh, kL, load, page, session, vehicle/month
    public bool IsActive { get; set; } = true;
    public ICollection<AccommodationService> AccommodationServices { get; set; } = new List<AccommodationService>();
}
