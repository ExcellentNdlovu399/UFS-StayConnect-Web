using System;
using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace UFSStayConnect.Validation
{
    public class ExpiryDateAttribute : ValidationAttribute
    {
        public override bool IsValid(object? value)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
                return true; // Use [Required] if the field is mandatory.

            string expiryDate = value.ToString()!;

            if (!DateTime.TryParseExact(
                    expiryDate,
                    "MM/yy",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateTime parsedDate))
            {
                return false;
            }

            // A card is valid through the last day of its expiry month.
            DateTime expiryMonthEnd = new DateTime(
                parsedDate.Year,
                parsedDate.Month,
                DateTime.DaysInMonth(parsedDate.Year, parsedDate.Month));

            return expiryMonthEnd >= DateTime.Today;
        }

        public override string FormatErrorMessage(string name)
        {
            return $"{name} has expired. Enter a current or future expiry date in MM/YY format.";
        }
    }
}