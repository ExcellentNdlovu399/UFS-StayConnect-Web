namespace UFSStayConnect.ViewModels
{
    public class AdminUserViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string UserType { get; set; } = string.Empty;
        public string Number { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string AccountStatus { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = "No allocation";
        public decimal OutstandingAmount { get; set; }
    }
}
