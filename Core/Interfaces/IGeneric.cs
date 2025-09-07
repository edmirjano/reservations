using Core.Models;

namespace Core.Interfaces;

public interface IGeneric<T>
    where T : GenericModel
{
    IEnumerable<T> GetAll();
    T GetById(Guid id);
    T Create(T entity, bool isActive = true);
    T Update(T entity);
    T Delete(Guid id);
}
