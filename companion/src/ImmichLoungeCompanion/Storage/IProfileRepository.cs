using System.Collections.Generic;
using System.Threading.Tasks;
using ImmichLoungeCompanion.Models;

namespace ImmichLoungeCompanion.Storage;

public interface IProfileRepository
{
    Task<List<Profile>> GetAllAsync();
    Task<Profile?> GetAsync(string id);
    Task<bool> ExistsAsync(string id);
    Task SaveAsync(Profile profile);
    Task<bool> DeleteAsync(string id);
}
