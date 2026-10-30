using System.Linq.Expressions;
using UFSStayConnect.Data;

namespace UFSStayConnect.DataAccess
{
    public class RepositoryBase<T> where T : class
    {
        protected AppDbContext _appDbContext;

        public RepositoryBase(AppDbContext appDbContext)
        {
            _appDbContext = appDbContext;
        }

        public void Create(T entity)
        {
            _appDbContext.Set<T>().Add(entity);
        }

        public void Delete(T entity)
        {
            _appDbContext.Set<T>().Remove(entity);
        }

        public IEnumerable<T> FindAll()
        {
            return _appDbContext.Set<T>();
        }

        public IEnumerable<T> FindByCondition(
            Expression<Func<T, bool>> expression)
        {
            return _appDbContext.Set<T>().Where(expression);
        }

        public T? GetById(int id)
        {
            return _appDbContext.Set<T>().Find(id);
        }

        public void Update(T entity)
        {
            _appDbContext.Set<T>().Update(entity);
        }
    }
}