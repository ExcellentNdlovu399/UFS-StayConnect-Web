using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.AspNetCore.Http;
using UFSStayConnect.Validation;
namespace UFSStayConnect.ViewModels
{
    public class PaymentCheckoutViewModel
    {
        // Keep the two entity key types separate; their integer values may overlap.
        public List<int> SelectedChargeIds { get; set; } = new();
        public List<int> SelectedCustomerChargeIds { get; set; } = new();

        public string AccommodationName { get; set; } = string.Empty;

        public string RoomNumber { get; set; } = "Not assigned";

        public List<PaymentCheckoutItemViewModel> Items { get; set; } = new();

        public decimal TotalAmount { get; set; }


        // =========================================================
        // PAYMENT METHOD
        // =========================================================

        [Required(ErrorMessage = "Please select a payment method.")]
        [StringLength(30)]
        [Display(Name = "Payment Method")]
        public string PaymentMethod { get; set; } = "Card";


        // =========================================================
        // CARD HOLDER
        // =========================================================

        [StringLength(100)]
        [RegularExpression(
            @"^[A-Za-zÀ-ÿ\s'-]+$",
            ErrorMessage = "Cardholder name may contain letters, spaces, apostrophes and hyphens only.")]
        [Display(Name = "Name on Card")]
        public string? CardHolderName { get; set; }


        // =========================================================
        // CARD NUMBER
        // =========================================================
        // Digits only.
        // Exactly 16 digits.

        [StringLength(
            16,
            MinimumLength = 16,
            ErrorMessage = "Card number must contain exactly 16 digits.")]
        [RegularExpression(
            @"^\d{16}$",
            ErrorMessage = "Card number must contain digits only.")]
        [Display(Name = "Card Number")]
        public string? CardNumber { get; set; }


        // =========================================================
        // EXPIRY DATE
        // =========================================================
        // Displayed as MM/YY.
        // Example: 05/28.

        [StringLength(5, MinimumLength = 5,
    ErrorMessage = "Expiry date must be in MM/YY format.")]
        [RegularExpression(@"^(0[1-9]|1[0-2])/\d{2}$",
    ErrorMessage = "Expiry date must be in MM/YY format, for example 05/28.")]
        [ExpiryDate]
        [Display(Name = "Expiry Date")]
        public string? ExpiryDate { get; set; }


        // =========================================================
        // CVV
        // =========================================================
        // Digits only.
        // Exactly 3 digits.

        [StringLength(
            3,
            MinimumLength = 3,
            ErrorMessage = "CVV must contain exactly 3 digits.")]
        [RegularExpression(
            @"^\d{3}$",
            ErrorMessage = "CVV must contain digits only.")]
        [Display(Name = "CVV")]
        public string? CVV { get; set; }


        // =========================================================
        // EFT
        // =========================================================
        // Must start with T.
        // Everything after T must be digits.
        //
        // Examples:
        // T12345       valid
        // T123456789   valid
        //
        // Examples of invalid values:
        // 12345        invalid
        // A12345       invalid
        // TABC123      invalid
        // TT12345      invalid

        [StringLength(
            20,
            MinimumLength = 2,
            ErrorMessage = "EFT reference must be between 2 and 20 characters.")]
        [RegularExpression(
            @"^T\d+$",
            ErrorMessage = "EFT reference must start with T and contain digits only.")]
        [Display(Name = "EFT Reference")]
        public string? EFTReference { get; set; }
    }
    // =============================================================
    // PAYMENT CHECKOUT ITEM
    // =============================================================

    public class PaymentCheckoutItemViewModel
    {
        public int AccommodationChargeID { get; set; }
        public int? CustomerChargeID { get; set; }
        public bool IsAssignedCustomerCharge { get; set; }
        public string ChargeTitle { get; set; } = string.Empty;

        public int PaymentTypeID { get; set; }

        public string PaymentTypeName { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        // Base configured charge plus any per-application parking amount included in Rent.
        public decimal BaseAmount { get; set; }
        public decimal ParkingFeeAmount { get; set; }

        public string Frequency { get; set; } = string.Empty;

        public DateTime DueDate { get; set; }

        // True when this item is the per-application parking fee rather than a recurring accommodation charge.
        public bool IsParkingFee { get; set; }
    }
}