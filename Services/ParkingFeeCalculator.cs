using UFSStayConnect.Models;

namespace UFSStayConnect.Services;

/// <summary>
/// One server-side source of truth for per-application parking charges.
/// The first vehicle is free; additional vehicles are charged at the
/// residence-specific Parking service rate (or the configured Parking
/// charge as a fallback for older residences without a service assignment).
/// </summary>
public static class ParkingFeeCalculator
{
    public static bool IsParkingCharge(string? paymentTypeName) =>
        !string.IsNullOrWhiteSpace(paymentTypeName) &&
        paymentTypeName.Contains("parking", StringComparison.OrdinalIgnoreCase);

    public static bool IsRentCharge(string? paymentTypeName) =>
        !string.IsNullOrWhiteSpace(paymentTypeName) &&
        paymentTypeName.Contains("rent", StringComparison.OrdinalIgnoreCase) &&
        !IsParkingCharge(paymentTypeName);

    public static decimal Calculate(
        Accommodation accommodation,
        int numberOfVehicles,
        IEnumerable<AccommodationCharge>? configuredCharges = null)
    {
        if (numberOfVehicles <= 1)
            return 0m;

        var parkingAssignment = accommodation.AccommodationServices?
            .FirstOrDefault(assignment =>
                assignment.IsEnabled &&
                assignment.Service != null &&
                assignment.Service.IsActive &&
                IsParkingCharge(assignment.Service.ServiceName));

        if (parkingAssignment != null)
        {
            // Prefer the accommodation-specific rate. If it is not configured,
            // fall back to the service catalogue's default rate.
            var rate = parkingAssignment.RatePerUnit > 0m
                ? parkingAssignment.RatePerUnit
                : parkingAssignment.Service!.RatePerUnit;
            if (rate > 0m)
            {
                // At least one vehicle is always included, even if an older
                // assignment was saved with FreeUnits = 0.
                var includedVehicles = Math.Max(1, parkingAssignment.FreeUnits);
                var billableVehicles = Math.Max(0, numberOfVehicles - includedVehicles);
                return decimal.Round(
                    rate * billableVehicles,
                    2,
                    MidpointRounding.AwayFromZero);
            }
        }

        // Compatibility for residences that use a Parking AccommodationCharge
        // but have not yet configured a positive Parking service rate.
        var parkingCharge = configuredCharges?.FirstOrDefault(charge =>
            charge.IsActive && IsParkingCharge(charge.PaymentType?.Name));

        if (parkingCharge == null)
            return 0m;

        return decimal.Round(
            parkingCharge.Amount * Math.Max(0, numberOfVehicles - 1),
            2,
            MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// A fee only becomes payable once the application is approved and a room
    /// has been allocated. Existing approved records with an old zero fee are
    /// calculated from current settings as a migration-free compatibility path.
    /// </summary>
    public static decimal ForApprovedApplication(
        Application application,
        Accommodation accommodation,
        IEnumerable<AccommodationCharge>? configuredCharges = null)
    {
        if (!string.Equals(application.Status, "Approved", StringComparison.OrdinalIgnoreCase) ||
            !application.RoomID.HasValue)
            return 0m;

        if (application.ParkingFee > 0m)
            return application.ParkingFee;

        return Calculate(accommodation, application.NumberOfVehicles, configuredCharges);
    }

    public static string AddParkingNote(string? description, decimal parkingFee)
    {
        var baseDescription = string.IsNullOrWhiteSpace(description)
            ? "Monthly accommodation rent."
            : description.Trim();

        return parkingFee <= 0m
            ? baseDescription
            : $"{baseDescription} Includes R{parkingFee:N2} parking for additional vehicle(s).";
    }
}
