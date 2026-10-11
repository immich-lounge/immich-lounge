using System.Threading.Tasks;
using ImmichLoungeCompanion.Models;
using ImmichLoungeCompanion.Storage;

namespace ImmichLoungeCompanion.Services;

public class ProfileDocumentService(ISettingsRepository settings) : IProfileDocumentService
{
    public Profile Normalize(Profile profile)
    {
        profile.NormalizeAssetFilter();
        profile.Memories ??= new();
        profile.Memories.TimeZoneId = CleanLocation(profile.Memories.TimeZoneId);
        if (profile.LocationFilter is { } location)
        {
            location.Country = CleanLocation(location.Country);
            location.State = CleanLocation(location.State);
            location.City = CleanLocation(location.City);
            if (location.Country == null && location.State == null && location.City == null)
            {
                profile.LocationFilter = null;
            }
        }
        return profile;
    }

    private static string? CleanLocation(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public async Task<object> BuildResponseAsync(Profile profile, string? selectionId = null, bool followingSchedule = false, string? selectionName = null)
    {
        var globalSettings = await settings.LoadAsync();

        return new
        {
            profile.SchemaVersion,
            Id = selectionId ?? profile.Id,
            PlaybackProfileId = profile.Id,
            FollowingSchedule = followingSchedule,
            SelectionName = selectionName,
            profile.Name,
            profile.Description,
            Immich = new { globalSettings.Immich.ServerUrl, globalSettings.Immich.ApiKey },
            profile.ContentSources,
            profile.AssetFilter,
            profile.PhotoSource,
            profile.ExcludedPeople,
            profile.OnlyPhotosWithPeople,
            profile.MediaTypes,
            profile.DateFilter,
            profile.LocationFilter,
            profile.Memories,
            profile.Slideshow,
            profile.Display,
            profile.Quality,
            profile.ImageQuality,
            profile.Weather
        };
    }
}
