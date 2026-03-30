using System.Collections.Generic;
using System.Threading.Tasks;
using ImmichLoungeCompanion.Models;
using ImmichLoungeCompanion.Storage;

namespace ImmichLoungeCompanion.Services;

/// <summary>Scoped service that caches in-flight data for the current browser session.</summary>
public class CompanionState(ISettingsRepository settingsRepo, IProfileRepository profileRepo)
{
    private GlobalSettings? _settings;

    public async Task<GlobalSettings> GetSettingsAsync()
        => _settings ??= await settingsRepo.LoadAsync();

    public void InvalidateSettings() => _settings = null;

    public async Task<List<Profile>> GetProfilesAsync()
        => await profileRepo.GetAllAsync();
}
