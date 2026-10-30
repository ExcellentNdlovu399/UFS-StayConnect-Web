using UFSStayConnect.Models;

namespace UFSStayConnect.DataAccess
{
    public interface IAccommodationRepository
    {
        IEnumerable<Accommodation> FindAllAccommodations();

        IEnumerable<Accommodation> FindAvailableAccommodations();

        IEnumerable<Accommodation> FindByLocation(string location);

        IEnumerable<Accommodation> FindByType(string type);

        IEnumerable<Accommodation> FindByService(int serviceId);

        Accommodation? GetAccommodationById(int id);

        void CreateAccommodation(Accommodation accommodation);

        void UpdateAccommodation(Accommodation accommodation);

        void DeleteAccommodation(Accommodation accommodation);
    }
}