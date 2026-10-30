using Microsoft.EntityFrameworkCore;
using UFSStayConnect.Data;
using UFSStayConnect.Models;

namespace UFSStayConnect.DataAccess
{
    public class ApplicationRepository
        : RepositoryBase<Application>,
          IApplicationRepository
    {
        public ApplicationRepository(
            AppDbContext appDbContext)
            : base(appDbContext)
        {
        }

        // =========================================================
        // GET ALL APPLICATIONS
        // =========================================================

        public IEnumerable<Application>
            FindAllApplications()
        {
            return _appDbContext.Applications
                .AsNoTracking()
                .Include(a => a.User)
                .Include(a => a.Accommodation)
                .Include(a => a.Room)
                .OrderByDescending(
                    a => a.ApplicationDate)
                .ToList();
        }


        // =========================================================
        // GET APPLICATIONS FOR USER
        // =========================================================

        public IEnumerable<Application>
            FindByUser(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return new List<Application>();
            }

            return _appDbContext.Applications
                .AsNoTracking()
                .Include(a => a.User)
                .Include(a => a.Accommodation)
                .Include(a => a.Room)
                .Where(a => a.UserID == userId)
                .OrderByDescending(
                    a => a.ApplicationDate)
                .ToList();
        }


        // =========================================================
        // GET APPLICATION BY ID
        // =========================================================

        public Application?
            GetApplicationById(int id)
        {
            if (id <= 0)
            {
                return null;
            }

            return _appDbContext.Applications
                .Include(a => a.User)
                .Include(a => a.Accommodation)
                .Include(a => a.Room)
                .FirstOrDefault(
                    a => a.ApplicationID == id);
        }


        // =========================================================
        // CREATE APPLICATION
        // =========================================================

        public void CreateApplication(
            Application application)
        {
            Create(application);
        }


        // =========================================================
        // UPDATE APPLICATION
        // =========================================================

        public void UpdateApplication(
            Application application)
        {
            if (application == null)
            {
                return;
            }

            // Only update the Application entity itself.
            //
            // Do NOT attach/update the User, Accommodation or Room
            // navigation objects. Those entities are handled
            // separately by their own repositories.

            _appDbContext.Entry(application).State =
                EntityState.Modified;

            // Navigation properties must not be updated
            // as part of this operation.
            //
            // The foreign-key values such as UserID,
            // AccommodationID and RoomID remain part of
            // the Application entity.

            _appDbContext.Entry(application)
                .Reference(a => a.User)
                .IsModified = false;

            _appDbContext.Entry(application)
                .Reference(a => a.Accommodation)
                .IsModified = false;

            _appDbContext.Entry(application)
                .Reference(a => a.Room)
                .IsModified = false;
        }


        // =========================================================
        // DELETE APPLICATION
        // =========================================================

        public void DeleteApplication(
            Application application)
        {
            Delete(application);
        }
    }
}