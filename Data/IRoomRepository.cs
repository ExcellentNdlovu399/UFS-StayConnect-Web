using UFSStayConnect.Models;

namespace UFSStayConnect.DataAccess
{
    public interface IRoomRepository
    {
        IEnumerable<Room> FindByAccommodation(int accommodationId);
        IEnumerable<Room> FindAvailableByAccommodation(int accommodationId);
        Room? GetRoomById(int id);
        void CreateRoom(Room room);
        void UpdateRoom(Room room);
        void DeleteRoom(Room room);
    }
}
