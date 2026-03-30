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

    public async Task<object> BuildResponseAsync(Profile profile)
    {
        var globalSettings = await settings.LoadAsync();

        return new
        {
            profile.SchemaVersion,
            profile.Id,
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
