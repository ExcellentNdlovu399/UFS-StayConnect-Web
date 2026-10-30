using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace UFSStayConnect.ViewModels
{
    public class RoomViewModel
    {
        public int RoomID { get; set; }
        [Required] public int AccommodationID { get; set; }
        [Required, StringLength(30)] public string RoomNumber { get; set; } = string.Empty;
        [StringLength(30)] public string? Floor { get; set; }
        [Required, StringLength(50)] public string RoomType { get; set; } = "Standard";
        [Required, StringLength(30)] public string Status { get; set; } = "Available";
        [StringLength(500)] public string? Description { get; set; }
        public IEnumerable<SelectListItem> Accommodations { get; set; } = new List<SelectListItem>();
    }
}
