using UFSStayConnect.Models;

namespace UFSStayConnect.DataAccess
{
    public interface IPaymentTypeRepository
    {
        IEnumerable<PaymentType> FindAllPaymentTypes();

        PaymentType? GetPaymentTypeById(int id);

        void CreatePaymentType(PaymentType paymentType);

        void UpdatePaymentType(PaymentType paymentType);

        void DeletePaymentType(PaymentType paymentType);
    }
}