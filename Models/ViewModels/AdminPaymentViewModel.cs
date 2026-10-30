namespace UFSStayConnect.ViewModels
{
    public class AdminPaymentViewModel
    {
        public int PaymentID { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? StudentNumber { get; set; }
        public string PaymentType { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
        public string ReferenceNumber { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = string.Empty;
        public DateTime PaymentDate { get; set; }
        public DateTime? DueDate { get; set; }
        public string? AccommodationName { get; set; }
        public string? ProofOfPaymentPath { get; set; }
    }
}
