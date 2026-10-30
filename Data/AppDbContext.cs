using Microsoft.EntityFrameworkCore;
using UFSStayConnect.Models;

namespace UFSStayConnect.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(
            DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<Accommodation> Accommodations { get; set; }

        public DbSet<Service> Services { get; set; }

        public DbSet<AccommodationService> AccommodationServices { get; set; }

        public DbSet<Application> Applications { get; set; }

        public DbSet<Payment> Payments { get; set; }

        public DbSet<PaymentType> PaymentTypes { get; set; }
        public DbSet<ApplicationUser> ApplicationUsers { get; set; }

        public DbSet<Notification> Notifications { get; set; }

        public DbSet<Review> Reviews { get; set; }
        public DbSet<AccommodationCharge> AccommodationCharges { get; set; }
        public DbSet<CustomerCharge> CustomerCharges { get; set; }
        public DbSet<Room> Rooms { get; set; }
        public DbSet<FinancialAdvice> FinancialAdvices { get; set; }

        public DbSet<PasswordResetRequest> PasswordResetRequests { get; set; }
        public DbSet<ServiceUsageRecord> ServiceUsageRecords { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // =========================================================
            // APPLICATION USER
            // =========================================================
            // ApplicationUser is managed by ASP.NET Identity.
            // AppDbContext only needs to reference the same AspNetUsers
            // table. It must NOT create another ApplicationUser table.
            modelBuilder.Entity<ApplicationUser>()
                .ToTable("AspNetUsers", tableBuilder =>
                    tableBuilder.ExcludeFromMigrations());


            modelBuilder.Entity<Application>()
               .Property(a => a.ParkingFee)
                   .HasPrecision(18, 2);

            // =========================================================
            // ACCOMMODATION ↔ SERVICE
            // =========================================================
            modelBuilder.Entity<AccommodationService>()
                .HasKey(x => new
                {
                    x.AccommodationID,
                    x.ServiceID
                });

            modelBuilder.Entity<AccommodationService>()
                .HasOne(x => x.Accommodation)
                .WithMany(a => a.AccommodationServices)
                .HasForeignKey(x => x.AccommodationID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AccommodationService>()
                .HasOne(x => x.Service)
                .WithMany(s => s.AccommodationServices)
                .HasForeignKey(x => x.ServiceID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AccommodationService>()
                .Property(x => x.RatePerUnit).HasPrecision(18, 2);

            modelBuilder.Entity<Service>()
                .Property(x => x.RatePerUnit).HasPrecision(18, 2);

            // Actual usage records support pay-as-you-go billing.
            modelBuilder.Entity<ServiceUsageRecord>()
                .HasOne(x => x.Application)
                .WithMany()
                .HasForeignKey(x => x.ApplicationID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ServiceUsageRecord>()
                .HasOne(x => x.Service)
                .WithMany()
                .HasForeignKey(x => x.ServiceID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ServiceUsageRecord>().Property(x => x.PreviousReading).HasPrecision(18, 3);
            modelBuilder.Entity<ServiceUsageRecord>().Property(x => x.CurrentReading).HasPrecision(18, 3);
            modelBuilder.Entity<ServiceUsageRecord>().Property(x => x.UnitsUsed).HasPrecision(18, 3);
            modelBuilder.Entity<ServiceUsageRecord>().Property(x => x.RatePerUnit).HasPrecision(18, 2);
            modelBuilder.Entity<ServiceUsageRecord>().Property(x => x.AmountDue).HasPrecision(18, 2);
            modelBuilder.Entity<ServiceUsageRecord>().HasIndex(x => new { x.ApplicationID, x.ServiceID, x.ReadingDate });

            modelBuilder.Entity<AccommodationCharge>()
                .HasOne(c => c.Accommodation)
    .WithMany(a => a.AccommodationCharges)
    .HasForeignKey(c => c.AccommodationID)
    .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AccommodationCharge>()
                .HasOne(c => c.PaymentType)
                .WithMany(pt => pt.AccommodationCharges)
                .HasForeignKey(c => c.PaymentTypeID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AccommodationCharge>()
                .HasIndex(c => new
                {
                    c.AccommodationID,
                    c.PaymentTypeID
                })
                .IsUnique();

            modelBuilder.Entity<AccommodationCharge>()
                .Property(c => c.Amount)
                .HasPrecision(18, 2);

            // =========================================================
            // APPLICATION → USER
            // =========================================================
            modelBuilder.Entity<Application>()
                .HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserID)
                .HasPrincipalKey(u => u.Id)
                .OnDelete(DeleteBehavior.Restrict);


            // =========================================================
            // APPLICATION → ACCOMMODATION
            // =========================================================
            modelBuilder.Entity<Application>()
                .HasOne(a => a.Accommodation)
                .WithMany(a => a.Applications)
                .HasForeignKey(a => a.AccommodationID)
                .OnDelete(DeleteBehavior.Restrict);


            // =========================================================
            // PAYMENT → PAYMENT TYPE
            // =========================================================
            modelBuilder.Entity<Payment>()
                .HasOne(p => p.PaymentType)
                .WithMany(pt => pt.Payments)
                .HasForeignKey(p => p.PaymentTypeID)
                .OnDelete(DeleteBehavior.Restrict);


            modelBuilder.Entity<FinancialAdvice>()
                .HasOne(a => a.Officer)
                .WithMany()
                .HasForeignKey(a => a.OfficerUserID)
                .HasPrincipalKey(u => u.Id)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<FinancialAdvice>()
                .Property(a => a.OfficerUserID)
                .HasMaxLength(450);

            // =========================================================
            // DECIMAL PRECISION
            // =========================================================
            modelBuilder.Entity<Room>()
                .HasOne(r => r.Accommodation)
                .WithMany(a => a.Rooms)
                .HasForeignKey(r => r.AccommodationID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Room>()
                .HasIndex(r => new { r.AccommodationID, r.RoomNumber })
                .IsUnique();

            modelBuilder.Entity<Application>()
                .HasOne(a => a.Room)
                .WithMany(r => r.Applications)
                .HasForeignKey(a => a.RoomID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CustomerCharge>().Property(c => c.Amount).HasPrecision(18, 2);
            modelBuilder.Entity<CustomerCharge>()
                .HasOne(c => c.User).WithMany().HasForeignKey(c => c.UserID)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<CustomerCharge>()
                .HasOne(c => c.Application).WithMany().HasForeignKey(c => c.ApplicationID)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<CustomerCharge>()
                .HasOne(c => c.Accommodation).WithMany().HasForeignKey(c => c.AccommodationID)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<CustomerCharge>()
                .HasOne(c => c.PaymentType).WithMany().HasForeignKey(c => c.PaymentTypeID)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Payment>()
                .HasOne(p => p.CustomerCharge).WithMany(c => c.Payments)
                .HasForeignKey(p => p.CustomerChargeID).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Payment>()
                .HasOne(p => p.AccommodationCharge)
                .WithMany()
                .HasForeignKey(p => p.AccommodationChargeID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Accommodation>()
                .Property(a => a.MonthlyRent)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Payment>()
                .Property(p => p.Amount)
                .HasPrecision(18, 2);


            // =========================================================
            // UNIQUE PAYMENT REFERENCE
            // =========================================================
            modelBuilder.Entity<Payment>()
                .HasIndex(p => p.ReferenceNumber)
                .IsUnique();

            modelBuilder.Entity<PasswordResetRequest>()
           .HasOne(r => r.User)
           .WithMany()
           .HasForeignKey(r => r.UserID)
           .HasPrincipalKey(u => u.Id)
           .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PasswordResetRequest>()
                .HasOne(r => r.ProcessedByUser)
                .WithMany()
                .HasForeignKey(r => r.ProcessedByUserID)
                .HasPrincipalKey(u => u.Id)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}