namespace UFSStayConnect.ViewModels
{
    public class CustomerApprovedAccommodationViewModel
    {
        public int ApplicationID { get; set; }
        public int AccommodationID { get; set; }
        public string AccommodationName { get; set; } = string.Empty;
        public string? RoomNumber { get; set; }
        public int? AvailableRooms { get; set; }
        public string Status { get; set; } = "Approved";
    }
}
