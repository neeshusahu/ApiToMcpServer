namespace ApiToMcpServer.Persistence;

public interface IRepository<T>
{
    Task InitializeAsync();
    Task AddAsync(T entity);
   
    Task UpdateAsync(T entity);
    Task DeleteAsync(T entity);
    Task<T?> GetByIdAsync(Guid id);
    Task<IEnumerable<T>> GetAllAsync();
}
