using UFSStayConnect.Data;
using UFSStayConnect.Models;
using Microsoft.EntityFrameworkCore;

namespace UFSStayConnect.DataAccess
{
    public class AccommodationRepository
        : RepositoryBase<Accommodation>,
          IAccommodationRepository
    {
        public AccommodationRepository(AppDbContext appDbContext)
            : base(appDbContext)
        {
        }

        public IEnumerable<Accommodation> FindAllAccommodations()
        {
            return FindAll();
        }

        public IEnumerable<Accommodation> FindAvailableAccommodations()
        {
            return _appDbContext.Accommodations
                .Include(a => a.AccommodationServices)
                .ThenInclude(s => s.Service)
                .Include(a => a.Rooms)
                .Where(a => a.Available)
                .ToList();
        }

        public IEnumerable<Accommodation> FindByLocation(string location)
        {
            return FindByCondition(a =>
                a.Location.Contains(location));
        }

        public IEnumerable<Accommodation> FindByType(string type)
        {
            return FindByCondition(a =>
                a.Type.Contains(type));
        }

        public IEnumerable<Accommodation> FindByService(int serviceId)
        {
            return _appDbContext.Accommodations
                .Where(a => a.AccommodationServices
                    .Any(s => s.ServiceID == serviceId));
        }

        public Accommodation? GetAccommodationById(int id)
        {
            return _appDbContext.Accommodations
                .Include(a => a.AccommodationServices)
                .ThenInclude(s => s.Service)
                .Include(a => a.Rooms)
                .FirstOrDefault(a => a.AccommodationID == id);
        }

        public void CreateAccommodation(
            Accommodation accommodation)
        {
            Create(accommodation);
        }

        public void UpdateAccommodation(
            Accommodation accommodation)
        {
            Update(accommodation);
        }

        public void DeleteAccommodation(
            Accommodation accommodation)
        {
            Delete(accommodation);
        }
    }
}