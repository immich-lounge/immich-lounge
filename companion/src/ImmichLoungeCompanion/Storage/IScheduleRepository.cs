using System.Collections.Generic;
using System.Threading.Tasks;
using ImmichLoungeCompanion.Models;

namespace ImmichLoungeCompanion.Storage;

public interface IScheduleRepository
{
    Task<List<ProfileSchedule>> GetAllAsync();
    Task<ProfileSchedule?> GetAsync(string id);
    Task SaveAsync(ProfileSchedule schedule);
    Task<bool> DeleteAsync(string id);
}
