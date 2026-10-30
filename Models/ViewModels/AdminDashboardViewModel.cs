using System.ComponentModel.DataAnnotations;

namespace UFSStayConnect.ViewModels
{
    public class AdminDashboardViewModel
    {
        // ================================
        // USER STATISTICS
        // ================================

        [Range(0, int.MaxValue)]
        public int TotalUsers { get; set; }

        [Range(0, int.MaxValue)]
        public int TotalStudents { get; set; }

        [Range(0, int.MaxValue)]
        public int TotalStaff { get; set; }

        [Range(0, int.MaxValue)]
        public int TotalFinancialOfficers { get; set; }


        // ================================
        // ACCOMMODATION
        // ================================

        [Range(0, int.MaxValue)]
        public int AvailableAccommodation { get; set; }

        [Range(0, int.MaxValue)]
        public int TotalRooms { get; set; }

        [Range(0, int.MaxValue)]
        public int OccupiedRooms { get; set; }


        // ================================
        // APPLICATIONS
        // ================================

        [Range(0, int.MaxValue)]
        public int TotalApplications { get; set; }

        [Range(0, int.MaxValue)]
        public int PendingApplications { get; set; }


        // ================================
        // PAYMENTS
        // ================================

        [Range(0, double.MaxValue)]
        public decimal TotalPayments { get; set; }

        [Range(0, double.MaxValue)]
        public decimal OutstandingPayments { get; set; }


        // ================================
        // RECENT DATA
        // ================================

        public List<AdminApplicationItemViewModel> RecentApplications { get; set; }
            = new();

        public List<AdminPaymentItemViewModel> RecentPayments { get; set; }
            = new();


        // ================================
        // SAFE DISPLAY VALUES
        // ================================

        public int SafeTotalUsers =>
            TotalUsers < 0 ? 0 : TotalUsers;

        public int SafeTotalStudents =>
            TotalStudents < 0 ? 0 : TotalStudents;

        public int SafeTotalStaff =>
            TotalStaff < 0 ? 0 : TotalStaff;

        public int SafeTotalFinancialOfficers =>
            TotalFinancialOfficers < 0 ? 0 : TotalFinancialOfficers;

        public int SafeAvailableAccommodation =>
            AvailableAccommodation < 0 ? 0 : AvailableAccommodation;

        public int SafeTotalRooms =>
            TotalRooms < 0 ? 0 : TotalRooms;

        public int SafeOccupiedRooms =>
            OccupiedRooms < 0 ? 0 : OccupiedRooms;

        public int SafeTotalApplications =>
            TotalApplications < 0 ? 0 : TotalApplications;

        public int SafePendingApplications =>
            PendingApplications < 0 ? 0 : PendingApplications;

        public decimal SafeTotalPayments =>
            TotalPayments < 0 ? 0 : TotalPayments;

        public decimal SafeOutstandingPayments =>
            OutstandingPayments < 0 ? 0 : OutstandingPayments;

        // ================================
        // OCCUPANCY
        // ================================

        public double OccupancyPercentage =>
            SafeTotalRooms > 0
                ? Math.Round(
                    (double)SafeOccupiedRooms / SafeTotalRooms * 100,
                    1)
                : 0;
    }


    // =========================================
    // RECENT APPLICATION
    // =========================================

    public class AdminApplicationItemViewModel
    {
        public int ApplicationID { get; set; }

        public string ApplicantName { get; set; } =
            "Unknown Applicant";

        public string AccommodationName { get; set; } =
            "Unknown Accommodation";

        public string Status { get; set; } =
            "Unknown";

        public DateTime ApplicationDate { get; set; }


        // ================================
        // SAFE DISPLAY VALUES
        // ================================

        public string SafeApplicantName =>
            string.IsNullOrWhiteSpace(ApplicantName)
                ? "Unknown Applicant"
                : ApplicantName.Trim();

        public string SafeAccommodationName =>
            string.IsNullOrWhiteSpace(AccommodationName)
                ? "Unknown Accommodation"
                : AccommodationName.Trim();

        public string SafeStatus =>
            string.IsNullOrWhiteSpace(Status)
                ? "Unknown"
                : Status.Trim();

        public string DisplayDate =>
            ApplicationDate == default
                ? "Date unavailable"
                : ApplicationDate.ToString("dd/MM/yyyy");
    }


    // =========================================
    // RECENT PAYMENT
    // =========================================

    public class AdminPaymentItemViewModel
    {
        public int PaymentID { get; set; }

        public string CustomerName { get; set; } =
            "Unknown Customer";

        public string PaymentType { get; set; } =
            "Unknown";

        public decimal Amount { get; set; }

        public string Status { get; set; } =
            "Unknown";

        public DateTime PaymentDate { get; set; }


        // ================================
        // SAFE DISPLAY VALUES
        // ================================

        public string SafeCustomerName =>
            string.IsNullOrWhiteSpace(CustomerName)
                ? "Unknown Customer"
                : CustomerName.Trim();

        public string SafePaymentType =>
            string.IsNullOrWhiteSpace(PaymentType)
                ? "Unknown"
                : PaymentType.Trim();

        public decimal SafeAmount =>
            Amount < 0 ? 0 : Amount;

        public string SafeStatus =>
            string.IsNullOrWhiteSpace(Status)
                ? "Unknown"
                : Status.Trim();

        public string DisplayDate =>
            PaymentDate == default
                ? "Date unavailable"
                : PaymentDate.ToString("dd/MM/yyyy");
    }
}