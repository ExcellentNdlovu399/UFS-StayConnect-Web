using Microsoft.EntityFrameworkCore;
using UFSStayConnect.Data;
using UFSStayConnect.Models;

namespace UFSStayConnect.Services
{
    public class PaymentReminderService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<PaymentReminderService> _logger;

        public PaymentReminderService(IServiceScopeFactory scopeFactory, ILogger<PaymentReminderService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await RunOnceAsync(stoppingToken);

            using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await RunOnceAsync(stoppingToken);
        }

        private async Task RunOnceAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var today = DateTime.UtcNow.Date;

                var approvedApplications = await context.Applications
                    .Include(a => a.Accommodation)
                    .Where(a => a.Status == "Approved")
                    .ToListAsync(cancellationToken);

                var userIds = approvedApplications.Select(a => a.UserID).Distinct().ToList();
                if (userIds.Count == 0) return;

                var chargesByAccommodation = await context.AccommodationCharges
                    .Include(c => c.PaymentType)
                    .Where(c => c.IsActive)
                    .ToListAsync(cancellationToken);

                var payments = await context.Payments
                    .Where(p => userIds.Contains(p.UserID) && p.AccommodationChargeID != null)
                    .ToListAsync(cancellationToken);

                var existingKeys = await context.Notifications
                    .Where(n => n.ReminderKey != null)
                    .Select(n => n.ReminderKey!)
                    .ToListAsync(cancellationToken);
                var keySet = existingKeys.ToHashSet(StringComparer.Ordinal);

                foreach (var application in approvedApplications)
                {
                    foreach (var charge in chargesByAccommodation.Where(c => c.AccommodationID == application.AccommodationID))
                    {
                        var dueDate = PaymentDueDateCalculator.GetCurrentCycleDueDate(charge, today);
                        var paid = payments.Any(p =>
                            p.UserID == application.UserID &&
                            p.AccommodationChargeID == charge.AccommodationChargeID &&
                            p.PaymentStatus == "Paid" &&
                            p.DueDate.HasValue &&
                            p.DueDate.Value.Date == dueDate.Date);

                        if (paid) continue;

                        string? kind = null;
                        string title;
                        string message;

                        if (today == dueDate.AddDays(-charge.ReminderDaysBeforeDue).Date && charge.ReminderDaysBeforeDue > 0)
                        {
                            kind = "BEFORE";
                            title = "Payment Reminder";
                            message = $"Your {charge.PaymentType?.Name ?? "payment"} charge of R{charge.Amount:N2} is due on {dueDate:dd MMM yyyy}.";
                        }
                        else if (today >= dueDate.AddDays(charge.ReminderDaysAfterDue).Date && charge.ReminderDaysAfterDue >= 0)
                        {
                            kind = "OVERDUE";
                            title = "Overdue Payment Reminder";
                            var days = Math.Max(1, (today - dueDate).Days);
                            message = $"Your {charge.PaymentType?.Name ?? "payment"} charge of R{charge.Amount:N2} was due on {dueDate:dd MMM yyyy} and is now {days} day(s) overdue. Please make payment as soon as possible.";
                        }
                        else
                        {
                            continue;
                        }

                        var key = $"PAYMENT-REMINDER:{application.UserID}:{charge.AccommodationChargeID}:{dueDate:yyyy-MM-dd}:{kind}";
                        if (keySet.Contains(key)) continue;

                        context.Notifications.Add(new Notification
                        {
                            UserID = application.UserID,
                            Title = title,
                            Message = message,
                            DateCreated = DateTime.UtcNow,
                            IsRead = false,
                            ReminderKey = key
                        });
                        keySet.Add(key);
                    }
                }

                await context.SaveChangesAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Application is shutting down.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Payment reminder service failed while checking overdue charges.");
            }
        }
    }
}
