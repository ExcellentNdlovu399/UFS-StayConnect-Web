using UFSStayConnect.Data;
using UFSStayConnect.Models;

namespace UFSStayConnect.DataAccess
{
    public class PaymentTypeRepository
        : RepositoryBase<PaymentType>,
          IPaymentTypeRepository
    {
        public PaymentTypeRepository(AppDbContext appDbContext)
            : base(appDbContext)
        {
        }

        public IEnumerable<PaymentType> FindAllPaymentTypes()
        {
            return _appDbContext.PaymentTypes
                .OrderBy(p => p.Name)
                .ToList();
        }

        public PaymentType? GetPaymentTypeById(int id)
        {
            if (id <= 0)
            {
                return null;
            }

            return _appDbContext.PaymentTypes
                .FirstOrDefault(p => p.PaymentTypeID == id);
        }

        public void CreatePaymentType(PaymentType paymentType)
        {
            Create(paymentType);
        }

        public void UpdatePaymentType(PaymentType paymentType)
        {
            Update(paymentType);
        }

        public void DeletePaymentType(PaymentType paymentType)
        {
            Delete(paymentType);
        }
    }
}