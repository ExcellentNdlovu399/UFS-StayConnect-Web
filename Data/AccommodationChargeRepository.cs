using Microsoft.EntityFrameworkCore;
using UFSStayConnect.Data;
using UFSStayConnect.Models;

namespace UFSStayConnect.DataAccess
{
    public class AccommodationChargeRepository
        : RepositoryBase<AccommodationCharge>,
          IAccommodationChargeRepository
    {
        public AccommodationChargeRepository(AppDbContext appDbContext)
            : base(appDbContext)
        {
        }

        public IEnumerable<AccommodationCharge> FindByAccommodation(int accommodationId)
        {
            return _appDbContext.AccommodationCharges
                .Include(c => c.PaymentType)
                .Include(c => c.Accommodation)
                .Where(c => c.AccommodationID == accommodationId)
                .OrderBy(c => c.PaymentType.Name)
                .ToList();
        }

        public AccommodationCharge? GetChargeById(int id)
        {
            return _appDbContext.AccommodationCharges
                .Include(c => c.PaymentType)
                .Include(c => c.Accommodation)
                .FirstOrDefault(c => c.AccommodationChargeID == id);
        }

        public void CreateCharge(AccommodationCharge charge)
        {
            Create(charge);
        }

        public void UpdateCharge(AccommodationCharge charge)
        {
            Update(charge);
        }

        public void DeleteCharge(AccommodationCharge charge)
        {
            Delete(charge);
        }
    }
}