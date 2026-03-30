using System.Threading.Tasks;
using ImmichLoungeCompanion.Models;

namespace ImmichLoungeCompanion.Storage;

public interface ISettingsRepository
{
    Task<GlobalSettings> LoadAsync();
    Task SaveAsync(GlobalSettings settings);
}
