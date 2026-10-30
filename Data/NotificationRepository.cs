using Microsoft.EntityFrameworkCore;
using UFSStayConnect.Data;
using UFSStayConnect.Models;

namespace UFSStayConnect.DataAccess
{
    public class NotificationRepository : RepositoryBase<Notification>, INotificationRepository
    {
        public NotificationRepository(AppDbContext appDbContext) : base(appDbContext) { }

        public IEnumerable<Notification> FindAllNotifications() =>
            _appDbContext.Notifications
                .Include(n => n.User)
                .OrderByDescending(n => n.DateCreated)
                .ToList();

        public IEnumerable<Notification> FindByUser(string userId) =>
            _appDbContext.Notifications
                .Include(n => n.User)
                .Where(n => n.UserID == userId)
                .OrderByDescending(n => n.DateCreated)
                .ToList();

        public Notification? GetNotificationById(int id) =>
            _appDbContext.Notifications
                .Include(n => n.User)
                .FirstOrDefault(n => n.NotificationID == id);

        public void CreateNotification(Notification notification) => Create(notification);
        public void UpdateNotification(Notification notification) => Update(notification);
        public void DeleteNotification(Notification notification) => Delete(notification);
    }
}
