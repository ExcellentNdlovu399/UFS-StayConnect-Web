using UFSStayConnect.Models;

namespace UFSStayConnect.DataAccess
{
    public interface IAccommodationChargeRepository
    {
        IEnumerable<AccommodationCharge> FindByAccommodation(int accommodationId);

        AccommodationCharge? GetChargeById(int id);

        void CreateCharge(AccommodationCharge charge);

        void UpdateCharge(AccommodationCharge charge);

        void DeleteCharge(AccommodationCharge charge);
    }
}