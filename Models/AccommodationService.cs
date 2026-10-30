using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UFSStayConnect.Models;

public class AccommodationService
{
    public int AccommodationID { get; set; }
    public int ServiceID { get; set; }
    // Accommodation-specific rate overrides the service's default rate.
    [Range(typeof(decimal), "0", "999999.99")]
    [Column(TypeName = "decimal(18,2)")]
    public decimal RatePerUnit { get; set; }
    [Required, StringLength(40)] public string UnitOfMeasure { get; set; } = "unit";
    // For parking, first car is free: set FreeUnits = 1.
    [Range(0, 1000)] public int FreeUnits { get; set; }
    public bool IsPayAsYouGo { get; set; } = true;
    public bool IsEnabled { get; set; } = true;
    public Accommodation? Accommodation { get; set; }
    public Service? Service { get; set; }
}
