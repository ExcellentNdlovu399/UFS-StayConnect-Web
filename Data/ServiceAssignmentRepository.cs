using Microsoft.EntityFrameworkCore;
using UFSStayConnect.Data;
using UFSStayConnect.Models;

namespace UFSStayConnect.DataAccess
{
    public class ServiceAssignmentRepository : IServiceAssignmentRepository
    {
        private readonly AppDbContext _context;
        public ServiceAssignmentRepository(AppDbContext context) => _context = context;

        public IEnumerable<AccommodationService> FindByAccommodation(int accommodationId) =>
            _context.AccommodationServices.Include(x => x.Service).Where(x => x.AccommodationID == accommodationId).OrderBy(x => x.Service!.ServiceName).ToList();

        public bool Exists(int accommodationId, int serviceId) =>
            _context.AccommodationServices.Any(x => x.AccommodationID == accommodationId && x.ServiceID == serviceId);

        public void Add(AccommodationService item) => _context.AccommodationServices.Add(item);
        public void Remove(AccommodationService item) => _context.AccommodationServices.Remove(item);
        public AccommodationService? Get(int accommodationId, int serviceId) =>
            _context.AccommodationServices.FirstOrDefault(x => x.AccommodationID == accommodationId && x.ServiceID == serviceId);
    }
}
