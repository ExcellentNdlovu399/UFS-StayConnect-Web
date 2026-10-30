using UFSStayConnect.Models;

namespace UFSStayConnect.DataAccess
{
    public interface INotificationRepository
    {
        IEnumerable<Notification> FindAllNotifications();
        IEnumerable<Notification> FindByUser(string userId);
        Notification? GetNotificationById(int id);
        void CreateNotification(Notification notification);
        void UpdateNotification(Notification notification);
        void DeleteNotification(Notification notification);
    }
}
