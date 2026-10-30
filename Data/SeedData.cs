using Microsoft.EntityFrameworkCore;
using UFSStayConnect.Models;

namespace UFSStayConnect.Data
{
    public static class SeedData
    {
        public static async Task InitializeAsync(IServiceProvider services)
        {
            var context = services.GetRequiredService<AppDbContext>();

            var serviceNames = new[] { "Water", "Electricity", "Wi-Fi", "24/7 Security", "Parking", "Laundry", "Refuse" };
            foreach (var name in serviceNames)
            {
                if (!await context.Services.AnyAsync(s => s.ServiceName == name))
                    context.Services.Add(new Service { ServiceName = name, Description = $"{name} service or facility." });
            }

            var paymentNames = new[] { "Rent", "Water", "Electricity", "Parking", "Laundry", "Refuse" };
            foreach (var name in paymentNames)
            {
                var existing = await context.PaymentTypes.FirstOrDefaultAsync(p => p.Name == name);
                if (existing == null)
                    context.PaymentTypes.Add(new PaymentType { Name = name, Description = $"{name} accommodation charge.", IsActive = true });
                else if (!existing.IsActive)
                    existing.IsActive = true;
            }

            await context.SaveChangesAsync();

            var rentType = await context.PaymentTypes.FirstOrDefaultAsync(p => p.Name == "Rent");
            if (rentType != null)
            {
                var accommodationsForCharges = await context.Accommodations.ToListAsync();
                foreach (var accommodation in accommodationsForCharges)
                {
                    var rentCharge = await context.AccommodationCharges.FirstOrDefaultAsync(c => c.AccommodationID == accommodation.AccommodationID && c.PaymentTypeID == rentType.PaymentTypeID);
                    if (rentCharge == null)
                        context.AccommodationCharges.Add(new AccommodationCharge { AccommodationID = accommodation.AccommodationID, PaymentTypeID = rentType.PaymentTypeID, Amount = accommodation.MonthlyRent, Frequency = "Monthly", Description = "Monthly accommodation rent.", IsActive = true, DueDate = UFSStayConnect.Services.PaymentDueDateCalculator.DefaultDueDate(), ReminderDaysBeforeDue = 3, ReminderDaysAfterDue = 3 });
                    else
                    {
                        if (rentCharge.Amount != accommodation.MonthlyRent)
                            rentCharge.Amount = accommodation.MonthlyRent;
                        if (!rentCharge.DueDate.HasValue)
                            rentCharge.DueDate = UFSStayConnect.Services.PaymentDueDateCalculator.DefaultDueDate();
                        if (rentCharge.ReminderDaysBeforeDue < 0) rentCharge.ReminderDaysBeforeDue = 3;
                        if (rentCharge.ReminderDaysAfterDue < 0) rentCharge.ReminderDaysAfterDue = 3;
                    }
                }
                await context.SaveChangesAsync();
            }

            var chargesWithoutDueDates = await context.AccommodationCharges.Where(c => c.DueDate == null).ToListAsync();
            foreach (var charge in chargesWithoutDueDates)
                charge.DueDate = UFSStayConnect.Services.PaymentDueDateCalculator.DefaultDueDate();
            await context.SaveChangesAsync();

            var accommodations = await context.Accommodations.Include(a => a.Rooms).Include(a => a.Applications).ToListAsync();
            foreach (var accommodation in accommodations)
            {
                if (accommodation.Rooms.Count == 0 && accommodation.TotalRooms > 0)
                {
                    for (var i = 1; i <= accommodation.TotalRooms; i++)
                        context.Rooms.Add(new Room { AccommodationID = accommodation.AccommodationID, RoomNumber = i.ToString("D2"), RoomType = "Standard", Status = "Available" });
                }
            }
            await context.SaveChangesAsync();

            var allRooms = await context.Rooms.ToListAsync();
            foreach (var accommodation in accommodations)
            {
                var rooms = allRooms.Where(r => r.AccommodationID == accommodation.AccommodationID).OrderBy(r => r.RoomNumber).ToList();
                var approved = accommodation.Applications.Where(a => a.Status == "Approved" && !a.RoomID.HasValue).OrderBy(a => a.ApplicationDate).ToList();
                foreach (var application in approved)
                {
                    var room = rooms.FirstOrDefault(r => r.Status == "Available");
                    if (room == null) break;
                    application.RoomID = room.RoomID;
                    room.Status = "Occupied";
                }
                if (rooms.Count > 0)
                {
                    accommodation.TotalRooms = rooms.Count;
                    accommodation.AvailableRooms = rooms.Count(r => r.Status == "Available");
                    accommodation.Available = accommodation.AvailableRooms > 0;
                }
            }
            await context.SaveChangesAsync();
        }
    }
}
