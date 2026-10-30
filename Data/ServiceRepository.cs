using UFSStayConnect.Data;
using UFSStayConnect.Models;

namespace UFSStayConnect.DataAccess
{
    public class ServiceRepository
        : RepositoryBase<Service>,
          IServiceRepository
    {
        public ServiceRepository(AppDbContext appDbContext)
            : base(appDbContext)
        {
        }

        public IEnumerable<Service> FindAllServices()
        {
            return FindAll();
        }

        public Service? GetServiceById(int id)
        {
            return GetById(id);
        }

        public void CreateService(Service service)
        {
            Create(service);
        }

        public void UpdateService(Service service)
        {
            Update(service);
        }

        public void DeleteService(Service service)
        {
            Delete(service);
        }
    }
}