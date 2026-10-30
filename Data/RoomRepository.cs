using Microsoft.EntityFrameworkCore;
using UFSStayConnect.Data;
using UFSStayConnect.Models;

namespace UFSStayConnect.DataAccess
{
    public class RoomRepository : RepositoryBase<Room>, IRoomRepository
    {
        public RoomRepository(AppDbContext appDbContext) : base(appDbContext) { }

        public IEnumerable<Room> FindByAccommodation(int accommodationId) =>
            _appDbContext.Rooms.Where(r => r.AccommodationID == accommodationId).OrderBy(r => r.RoomNumber).ToList();

        public IEnumerable<Room> FindAvailableByAccommodation(int accommodationId) =>
            _appDbContext.Rooms.Where(r => r.AccommodationID == accommodationId && r.Status == "Available").OrderBy(r => r.RoomNumber).ToList();

        public Room? GetRoomById(int id) =>
            _appDbContext.Rooms.Include(r => r.Accommodation).FirstOrDefault(r => r.RoomID == id);

        public void CreateRoom(Room room) => Create(room);
        public void UpdateRoom(Room room) => Update(room);
        public void DeleteRoom(Room room) => Delete(room);
    }
}
