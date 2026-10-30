using UFSStayConnect.Models;

namespace UFSStayConnect.Services
{
    public static class PaymentDueDateCalculator
    {
        /// <summary>
        /// Creates the first due date for a newly configured charge.
        /// The first payment is due 7 days from the supplied date.
        /// </summary>
        public static DateTime DefaultDueDate(DateTime? from = null)
        {
            var date = (from ?? DateTime.UtcNow).Date;

            return date.AddDays(7);
        }

        /// <summary>
        /// Gets the next upcoming due date for a charge.
        /// </summary>
        public static DateTime GetNextDueDate(
            AccommodationCharge charge,
            DateTime? asOf = null)
        {
            var today = (asOf ?? DateTime.UtcNow).Date;

            var anchor =
                (charge.DueDate ?? DefaultDueDate(today)).Date;

            var frequency =
                (charge.Frequency ?? "Monthly")
                    .Trim()
                    .ToLowerInvariant();

            // ---------------------------------------------------------
            // ONCE-OFF
            // ---------------------------------------------------------

            if (frequency == "once-off")
            {
                return anchor;
            }

            // ---------------------------------------------------------
            // WEEKLY
            // ---------------------------------------------------------

            if (frequency == "weekly")
            {
                if (anchor >= today)
                {
                    return anchor;
                }

                var weeks =
                    (int)Math.Ceiling(
                        (today - anchor).TotalDays / 7d);

                return anchor.AddDays(
                    Math.Max(0, weeks) * 7);
            }

            // ---------------------------------------------------------
            // ANNUAL
            // ---------------------------------------------------------

            if (frequency == "annual")
            {
                var candidate =
                    SafeDate(
                        today.Year,
                        anchor.Month,
                        anchor.Day);

                if (candidate < today)
                {
                    candidate =
                        SafeDate(
                            today.Year + 1,
                            anchor.Month,
                            anchor.Day);
                }

                return candidate;
            }

            // ---------------------------------------------------------
            // TERMLY
            // ---------------------------------------------------------

            if (frequency == "termly")
            {
                var candidate = anchor;

                while (candidate < today)
                {
                    candidate = candidate.AddMonths(3);
                }

                return candidate;
            }

            // ---------------------------------------------------------
            // MONTHLY
            // ---------------------------------------------------------

            var day =
                Math.Min(
                    anchor.Day,
                    DateTime.DaysInMonth(
                        today.Year,
                        today.Month));

            var monthly =
                new DateTime(
                    today.Year,
                    today.Month,
                    day);

            if (monthly < today)
            {
                var next =
                    today.AddMonths(1);

                day =
                    Math.Min(
                        anchor.Day,
                        DateTime.DaysInMonth(
                            next.Year,
                            next.Month));

                monthly =
                    new DateTime(
                        next.Year,
                        next.Month,
                        day);
            }

            return monthly;
        }

        /// <summary>
        /// Gets the due date belonging to the current billing cycle.
        /// This may return a past date so an unpaid charge can correctly
        /// be shown as overdue.
        /// </summary>
        public static DateTime GetCurrentCycleDueDate(
            AccommodationCharge charge,
            DateTime? asOf = null)
        {
            var today =
                (asOf ?? DateTime.UtcNow).Date;

            var anchor =
                (charge.DueDate ?? DefaultDueDate(today)).Date;

            var frequency =
                (charge.Frequency ?? "Monthly")
                    .Trim()
                    .ToLowerInvariant();

            // ---------------------------------------------------------
            // ONCE-OFF
            // ---------------------------------------------------------

            if (frequency == "once-off")
            {
                return anchor;
            }

            // ---------------------------------------------------------
            // WEEKLY
            // ---------------------------------------------------------

            if (frequency == "weekly")
            {
                if (today <= anchor)
                {
                    return anchor;
                }

                var weeks =
                    (int)Math.Floor(
                        (today - anchor).TotalDays / 7d);

                return anchor.AddDays(
                    Math.Max(0, weeks) * 7);
            }

            // ---------------------------------------------------------
            // ANNUAL
            // ---------------------------------------------------------

            if (frequency == "annual")
            {
                var candidate =
                    SafeDate(
                        today.Year,
                        anchor.Month,
                        anchor.Day);

                return candidate > today
                    ? SafeDate(
                        today.Year - 1,
                        anchor.Month,
                        anchor.Day)
                    : candidate;
            }

            // ---------------------------------------------------------
            // TERMLY
            // ---------------------------------------------------------

            if (frequency == "termly")
            {
                if (today <= anchor)
                {
                    return anchor;
                }

                var candidate = anchor;

                while (candidate.AddMonths(3) <= today)
                {
                    candidate =
                        candidate.AddMonths(3);
                }

                return candidate;
            }

            // ---------------------------------------------------------
            // MONTHLY
            // ---------------------------------------------------------

            // First configured due date is still ahead.
            if (anchor > today)
            {
                return anchor;
            }

            var day =
                Math.Min(
                    anchor.Day,
                    DateTime.DaysInMonth(
                        today.Year,
                        today.Month));

            var monthly =
                new DateTime(
                    today.Year,
                    today.Month,
                    day);

            return monthly;
        }

        private static DateTime SafeDate(
            int year,
            int month,
            int day)
        {
            return new DateTime(
                year,
                month,
                Math.Min(
                    day,
                    DateTime.DaysInMonth(
                        year,
                        month)));
        }
    }
}