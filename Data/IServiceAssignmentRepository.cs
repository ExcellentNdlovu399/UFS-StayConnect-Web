using UFSStayConnect.Models;

namespace UFSStayConnect.DataAccess
{
    public interface IServiceAssignmentRepository
    {
        IEnumerable<AccommodationService> FindByAccommodation(int accommodationId);
        bool Exists(int accommodationId, int serviceId);
        void Add(AccommodationService item);
        void Remove(AccommodationService item);
        AccommodationService? Get(int accommodationId, int serviceId);
    }
}
