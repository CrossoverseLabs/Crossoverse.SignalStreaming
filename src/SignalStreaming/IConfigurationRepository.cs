using System.Threading.Tasks;

namespace Crossoverse.Core.Configuration
{
    public interface IConfigurationRepository<T>
    {
        T Find(string key);
        Task<T> FindAsync(string key);
        void Save(string key, T value);
        Task<T> SaveAsync(string key, T value);
    }
}
