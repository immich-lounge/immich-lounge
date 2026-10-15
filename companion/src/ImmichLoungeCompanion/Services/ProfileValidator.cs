using System;
using ImmichLoungeCompanion.Models;

namespace ImmichLoungeCompanion.Services;

public class ProfileValidator : IProfileValidator
{
    public string? Validate(Profile profile)
    {
        if (profile.OnlyPhotosWithPeople && profile.OnlyPhotosWithoutPeople)
        {
            return "Choose either photos with people or photos without people, not both.";
        }

        if (profile.MinimumRating is < 1 or > 5)
        {
            return "minimumRating must be 1–5, or null for any rating.";
        }

        if (profile.PhotoSource is not (null or "all" or "selected" or "memories"))
        {
            return "Invalid photoSource. Choose all, selected, or memories.";
        }
        if (profile.PhotoSource == "selected" && profile.AssetFilter == null &&
            ProfileAssetFilterExtensions.BuildAssetFilterFromContentSources(profile.ContentSources) == null)
        {
            return "Choose at least one album, person, or tag for Selected sources.";
        }
        if (profile.PhotoSource == "memories" && !profile.HasMemoriesSource())
        {
            return "Enable On This Day, birthday, or saved memories for Memories only.";
        }

        if (!string.IsNullOrWhiteSpace(profile.Memories.TimeZoneId) &&
            !TimeZoneInfo.TryFindSystemTimeZoneById(profile.Memories.TimeZoneId, out _))
        {
            return "Invalid memories time zone. Choose a time zone supported by the companion.";
        }

        if (!IsMediaTypesValid(profile.MediaTypes))
        {
            return "At least one of photos, videos, or livePhotos must be true.";
        }

        if (!IsTransitionEffectValid(profile.Slideshow.TransitionEffect))
        {
            return "Invalid transitionEffect. Must be fade, none, slide, zoom, or random.";
        }

        if (profile.Slideshow.DateOrder is not ("newestFirst" or "oldestFirst"))
        {
            return "Invalid dateOrder. Must be newestFirst or oldestFirst.";
        }

        if (!IsPhotoMotionValid(profile.Slideshow.PhotoMotion))
        {
            return "Invalid photoMotion. Must be none or kenBurns.";
        }

        if (!IsBackgroundEffectValid(profile.Display.BackgroundEffect))
        {
            return "Invalid backgroundEffect. Must be none, blur, or ambilight.";
        }

        if (profile.Display.LocationFormat is not ("cityCountry" or "cityStateCountry" or "city" or "stateCountry" or "country"))
        {
            return "Invalid locationFormat. Must be cityCountry, cityStateCountry, city, stateCountry, or country.";
        }

        if (!IsWeatherUnitValid(profile.Display.WeatherUnit))
        {
            return "Invalid weatherUnit. Must be celsius or fahrenheit.";
        }

        if (!IsSlideshowValid(profile.Slideshow))
        {
            return "intervalSeconds must be 3–3600; refreshIntervalMinutes must be 5–1440.";
        }

        if (!IsImageQualityValid(profile.ImageQuality))
        {
            return "Invalid imageQuality. Must be preview or original.";
        }

        if (!double.IsFinite(profile.Slideshow.TransitionDurationSeconds) ||
            profile.Slideshow.TransitionDurationSeconds is < 0.1 or > 5 ||
            profile.Slideshow.TransitionDurationSeconds >= profile.Slideshow.IntervalSeconds)
        {
            return "transitionDurationSeconds must be 0.1–5 and shorter than intervalSeconds.";
        }

        if (!IsQualityValid(profile.Quality))
        {
            return "Minimum file size must be 0 KB or greater.";
        }

        if (profile.ExcludedPeople.Exists(person => person.Type != "person" || string.IsNullOrWhiteSpace(person.Id)))
        {
            return "Excluded people must have type person and a non-empty id.";
        }

        return ValidateDateFilter(profile.DateFilter) ?? profile.ValidateAssetFilter();
    }

    private static string? ValidateDateFilter(DateFilter? filter)
    {
        if (filter == null) { return null; }
        if (filter.Type == "rolling")
        {
            if (filter.Amount is not (>= 1 and <= 100)) { return "Rolling date filter amount must be 1–100."; }
            return filter.Unit is "days" or "weeks" or "months" or "years" ? null
                : "Rolling date filter unit must be days, weeks, months, or years.";
        }
        if (filter.Type != "range") { return "Invalid date filter type. Choose range or rolling."; }
        if (string.IsNullOrWhiteSpace(filter.From) && string.IsNullOrWhiteSpace(filter.To))
        { return "Date range needs a from or to date."; }
        if (!TryDateBound(filter.From, out var from) || !TryDateBound(filter.To, out var to))
        { return "Date filter bounds must be yyyy-MM-dd dates or ISO timestamps."; }
        // Date-only upper bounds include the whole day; timestamps keep their exact value.
        if (filter.To is { Length: 10 } && to.HasValue) { to = to.Value.AddHours(23).AddMinutes(59).AddSeconds(59).AddMilliseconds(999); }
        return from.HasValue && to.HasValue && from > to ? "Date filter from date must not be after the to date." : null;
    }

    private static bool TryDateBound(string? value, out DateTimeOffset? bound)
    {
        bound = null;
        if (string.IsNullOrWhiteSpace(value)) { return true; }
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", culture, System.Globalization.DateTimeStyles.None, out var date))
        { bound = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero); return true; }
        var hasOffset = value.EndsWith('Z') || (value.Length >= 6 && value[^6] is '+' or '-' && value[^3] == ':');
        if (value.Contains('T') && hasOffset && DateTimeOffset.TryParse(value, culture, System.Globalization.DateTimeStyles.None, out var timestamp))
        { bound = timestamp; return true; }
        return false;
    }

    private static bool IsMediaTypesValid(MediaTypes m) => m.Photos || m.Videos || m.LivePhotos;
    private static bool IsTransitionEffectValid(string e) =>
        e is "fade" or "none" or "slide" or "zoom" or "random";
    private static bool IsPhotoMotionValid(string e) => e is "none" or "kenBurns";
    private static bool IsBackgroundEffectValid(string e) => e is "none" or "blur" or "ambilight";
    private static bool IsWeatherUnitValid(string unit) => unit is "celsius" or "fahrenheit";
    private static bool IsImageQualityValid(string q) => q is "preview" or "original";
    private static bool IsQualityValid(QualitySettings quality) =>
        quality.MinFileSizeKb is null or >= 0;
    private static bool IsSlideshowValid(SlideshowSettings s) =>
        s.IntervalSeconds is >= 3 and <= 3600 &&
        s.RefreshIntervalMinutes is >= 5 and <= 1440;
}
