using System.ComponentModel.DataAnnotations;

namespace UFSStayConnect.ViewModels
{
    public class CustomerDashboardViewModel
    {
        public string FirstName { get; set; } = string.Empty;

        public string Surname { get; set; } = string.Empty;

        public string FullName =>
            $"{FirstName} {Surname}".Trim();

        public string UserType { get; set; } = string.Empty;

        public string? StudentNumber { get; set; }

        public string? EmployeeNumber { get; set; }

        // Accommodation information
        public string AccommodationStatus { get; set; }
            = "No Accommodation";

        public string? AccommodationName { get; set; }

        public int? AccommodationID { get; set; }

        public int? AvailableRooms { get; set; }

        public string? RoomNumber { get; set; }

        // Application information
        public int TotalApplications { get; set; }

        public int PendingApplications { get; set; }

        public int ApprovedApplications { get; set; }

        public int RejectedApplications { get; set; }

        // All approved accommodations linked to this customer.
        public List<CustomerApprovedAccommodationViewModel> ApprovedAccommodationItems { get; set; } = new();

        // Payment information
        [Range(0, double.MaxValue)]
        public decimal OutstandingBalance { get; set; }

        [Range(0, double.MaxValue)]
        public decimal RecentPaymentAmount { get; set; }

        public decimal OverdueBalance { get; set; }
        public decimal DueSoonBalance { get; set; }
        public List<DashboardPaymentItemViewModel> PaymentItems { get; set; } = new();

        // Notifications
        public int UnreadNotifications { get; set; }
    }
}
namespace UFSStayConnect.ViewModels
{
    public class DashboardPaymentItemViewModel
    {
        public string PaymentTypeName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime DueDate { get; set; }
        public string Status { get; set; } = "Unpaid";
    }
}
