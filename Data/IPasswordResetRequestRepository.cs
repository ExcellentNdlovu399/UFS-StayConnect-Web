using UFSStayConnect.Models;

namespace UFSStayConnect.DataAccess
{
    public interface IPasswordResetRequestRepository
    {
        IEnumerable<PasswordResetRequest> GetAll();

        PasswordResetRequest? GetById(int id);

        IEnumerable<PasswordResetRequest> GetByUserId(string userId);

        PasswordResetRequest? GetPendingByUserId(string userId);

        Task<PasswordResetRequest?> FindPendingByUserAsync(string userId);

        void Create(PasswordResetRequest request);

        void Update(PasswordResetRequest request);

        void Delete(PasswordResetRequest request);
    }
}