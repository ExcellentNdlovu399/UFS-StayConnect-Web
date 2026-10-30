using UFSStayConnect.Models;

namespace UFSStayConnect.DataAccess
{
    public interface IServiceRepository
    {
        IEnumerable<Service> FindAllServices();

        Service? GetServiceById(int id);

        void CreateService(Service service);

        void UpdateService(Service service);

        void DeleteService(Service service);
    }
}