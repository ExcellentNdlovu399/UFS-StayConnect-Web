using Microsoft.EntityFrameworkCore;
using UFSStayConnect.Data;
using UFSStayConnect.Models;

namespace UFSStayConnect.DataAccess
{
    public class PaymentRepository : RepositoryBase<Payment>, IPaymentRepository
    {
        public PaymentRepository(AppDbContext appDbContext)
            : base(appDbContext)
        {
        }

        public IEnumerable<Payment> FindAllPayments()
        {
            return _appDbContext.Payments
                .Include(p => p.User)
                .Include(p => p.PaymentType)
                .Include(p => p.Application)
                .ThenInclude(a => a!.Accommodation)
                .OrderByDescending(p => p.PaymentDate)
                .ToList();
        }

        public IEnumerable<Payment> FindByUser(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return new List<Payment>();
            }

            return _appDbContext.Payments
                .Include(p => p.User)
                .Include(p => p.PaymentType)
                .Include(p => p.Application)
                .Where(p => p.UserID == userId)
                .OrderByDescending(p => p.PaymentDate)
                .ToList();
        }

        public Payment? GetPaymentById(int id)
        {
            if (id <= 0)
            {
                return null;
            }

            return _appDbContext.Payments
                .Include(p => p.User)
                .Include(p => p.PaymentType)
                .Include(p => p.Application)
                .FirstOrDefault(p => p.PaymentID == id);
        }

        public void CreatePayment(Payment payment)
        {
            Create(payment);
        }

        public void UpdatePayment(Payment payment)
        {
            Update(payment);
        }

        public void DeletePayment(Payment payment)
        {
            Delete(payment);
        }
    }
}