using System.Threading.Tasks;
using ImmichLoungeCompanion.Models;
using ImmichLoungeCompanion.Storage;

namespace ImmichLoungeCompanion.Services;

public class ProfileDocumentService(ISettingsRepository settings) : IProfileDocumentService
{
    public Profile Normalize(Profile profile)
    {
        profile.NormalizeAssetFilter();
        return profile;
    }

    public async Task<object> BuildResponseAsync(Profile profile, Profile? selected = null)
    {
        var globalSettings = await settings.LoadAsync();

        return new
        {
            profile.SchemaVersion,
            Id = selected?.Id ?? profile.Id,
            PlaybackProfileId = profile.Id,
            ScheduleEnabled = (selected ?? profile).Schedule?.Enabled == true,
            Schedule = selected == null ? profile.Schedule : null,
            profile.Name,
            profile.Description,
            Immich = new { globalSettings.Immich.ServerUrl, globalSettings.Immich.ApiKey },
            profile.ContentSources,
            profile.AssetFilter,
            profile.MediaTypes,
            profile.DateFilter,
            profile.Slideshow,
            profile.Display,
            profile.Quality,
            profile.ImageQuality,
            profile.Weather
        };
    }
}
