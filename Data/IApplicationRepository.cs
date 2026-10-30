using UFSStayConnect.Models;

namespace UFSStayConnect.DataAccess
{
    public interface IApplicationRepository
    {
        IEnumerable<Application> FindAllApplications();

        IEnumerable<Application> FindByUser(
            string userId);

        Application? GetApplicationById(
            int id);

        void CreateApplication(
            Application application);

        void UpdateApplication(
            Application application);

        void DeleteApplication(
            Application application);
    }
}