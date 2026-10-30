using System.ComponentModel.DataAnnotations;
namespace UFSStayConnect.ViewModels
{
    public class ReviewViewModel
    {
        public int ReviewID { get; set; }
        public int AccommodationID { get; set; }
        public string AccommodationName { get; set; } = string.Empty;
        [Required, Range(1,5)] public int Rating { get; set; }
        [StringLength(1000)] public string? Comment { get; set; }
    }
}
