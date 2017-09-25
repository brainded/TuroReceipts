using System.Collections.Generic;

namespace TuroReceipts
{
    public interface IRepository<T>
    {
        T Insert(T model);
        T Update(T model);
        bool Delete(T model);
        bool Exists(int id);
        T Select(int id);
        IEnumerable<T> SelectAll();
    }
}
