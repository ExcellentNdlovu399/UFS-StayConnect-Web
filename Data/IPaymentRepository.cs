using UFSStayConnect.Models;

namespace UFSStayConnect.DataAccess
{
    public interface IPaymentRepository
    {
        IEnumerable<Payment> FindAllPayments();

        IEnumerable<Payment> FindByUser(string userId);

        Payment? GetPaymentById(int id);

        void CreatePayment(Payment payment);

        void UpdatePayment(Payment payment);

        void DeletePayment(Payment payment);
    }
}