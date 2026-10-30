using System.ComponentModel.DataAnnotations;

namespace UFSStayConnect.ViewModels
{
    public class ServiceAssignmentViewModel
    {
        [Required] public int AccommodationID { get; set; }
        public string AccommodationName { get; set; } = string.Empty;
        public List<ServiceSelectionViewModel> Services { get; set; } = new();
    }

    public class ServiceSelectionViewModel
    {
        public int ServiceID { get; set; }
        public string ServiceName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool Selected { get; set; }
        [Range(typeof(decimal), "0", "999999.99")] public decimal RatePerUnit { get; set; }
        [Required, StringLength(40)] public string UnitOfMeasure { get; set; } = "unit";
        [Range(0, 1000)] public int FreeUnits { get; set; }
        public bool IsPayAsYouGo { get; set; } = true;
        public bool IsEnabled { get; set; } = true;
    }
}
