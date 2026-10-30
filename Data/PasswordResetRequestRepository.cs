using Microsoft.EntityFrameworkCore;
using UFSStayConnect.Data;
using UFSStayConnect.Models;

namespace UFSStayConnect.DataAccess
{
    public class PasswordResetRequestRepository : IPasswordResetRequestRepository
    {
        private readonly AppDbContext _appDbContext;

        public PasswordResetRequestRepository(AppDbContext appDbContext)
        {
            _appDbContext = appDbContext;
        }

        public IEnumerable<PasswordResetRequest> GetAll()
        {
            return _appDbContext.PasswordResetRequests
                .Include(r => r.User)
                .Include(r => r.ProcessedByUser)
                .OrderByDescending(r => r.RequestDate)
                .ToList();
        }

        public PasswordResetRequest? GetById(int id)
        {
            return _appDbContext.PasswordResetRequests
                .Include(r => r.User)
                .Include(r => r.ProcessedByUser)
                .FirstOrDefault(r => r.PasswordResetRequestID == id);
        }

        public IEnumerable<PasswordResetRequest> GetByUserId(string userId)
        {
            return _appDbContext.PasswordResetRequests
                .Where(r => r.UserID == userId)
                .OrderByDescending(r => r.RequestDate)
                .ToList();
        }

        public PasswordResetRequest? GetPendingByUserId(string userId)
        {
            return _appDbContext.PasswordResetRequests
                .FirstOrDefault(r =>
                    r.UserID == userId &&
                    (r.Status == "Pending" || r.Status == "Processing"));
        }

        public void Create(PasswordResetRequest request)
        {
            _appDbContext.PasswordResetRequests.Add(request);
        }

        public void Update(PasswordResetRequest request)
        {
            _appDbContext.PasswordResetRequests.Update(request);
        }

        public void Delete(PasswordResetRequest request)
        {
            _appDbContext.PasswordResetRequests.Remove(request);
        }

        public async Task<PasswordResetRequest?> FindPendingByUserAsync(string userId)
        {
            return await _appDbContext.PasswordResetRequests
                .FirstOrDefaultAsync(r =>
                    r.UserID == userId &&
                    (r.Status == "Pending" || r.Status == "Processing"));
        }
    }
}