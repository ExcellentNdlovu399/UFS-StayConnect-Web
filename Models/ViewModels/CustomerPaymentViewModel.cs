namespace UFSStayConnect.ViewModels
{
    public class CustomerPaymentViewModel
    {
        public int AccommodationChargeID { get; set; }
        public int? CustomerChargeID { get; set; }
        public bool IsAssignedCustomerCharge { get; set; }
        public string ChargeTitle { get; set; } = string.Empty;
        public int AccommodationID { get; set; }
        public string AccommodationName { get; set; } = string.Empty;
        public string RoomNumber { get; set; } = "Not assigned";
        public int PaymentTypeID { get; set; }
        public string PaymentTypeName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal BaseAmount { get; set; }
        public decimal ParkingFeeAmount { get; set; }
        public string Frequency { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Status { get; set; } = "Unpaid";
        public string? LastReference { get; set; }
        public DateTime? LastPaymentDate { get; set; }
        public DateTime DueDate { get; set; }
        public int DaysUntilDue => (DueDate.Date - DateTime.UtcNow.Date).Days;
        public bool IsOverdue => Status.Equals("Overdue", StringComparison.OrdinalIgnoreCase);
        public bool CanPay => Status.Equals("Unpaid", StringComparison.OrdinalIgnoreCase)
                              || Status.Equals("Overdue", StringComparison.OrdinalIgnoreCase)
                              || Status.Equals("Rejected", StringComparison.OrdinalIgnoreCase);
    }
}
