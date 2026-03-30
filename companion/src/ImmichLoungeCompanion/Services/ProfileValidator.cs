using ImmichLoungeCompanion.Models;

namespace ImmichLoungeCompanion.Services;

public class ProfileValidator : IProfileValidator
{
    public string? Validate(Profile profile)
    {
        if (!IsMediaTypesValid(profile.MediaTypes))
        {
            return "At least one of photos, videos, or livePhotos must be true.";
        }

        if (!IsTransitionEffectValid(profile.Slideshow.TransitionEffect))
        {
            return "Invalid transitionEffect. Must be fade, none, slide, zoom, or random.";
        }

        if (!IsPhotoMotionValid(profile.Slideshow.PhotoMotion))
        {
            return "Invalid photoMotion. Must be none or kenBurns.";
        }

        if (!IsBackgroundEffectValid(profile.Display.BackgroundEffect))
        {
            return "Invalid backgroundEffect. Must be none, blur, or ambilight.";
        }

        if (!IsSlideshowValid(profile.Slideshow))
        {
            return "intervalSeconds must be 3–3600; refreshIntervalMinutes must be 5–1440.";
        }

        if (!IsImageQualityValid(profile.ImageQuality))
        {
            return "Invalid imageQuality. Must be preview or original.";
        }

        if (!IsQualityValid(profile.Quality))
        {
            return "Minimum file size must be 0 KB or greater.";
        }

        return profile.ValidateAssetFilter();
    }

    private static bool IsMediaTypesValid(MediaTypes m) => m.Photos || m.Videos || m.LivePhotos;
    private static bool IsTransitionEffectValid(string e) =>
        e is "fade" or "none" or "slide" or "zoom" or "random";
    private static bool IsPhotoMotionValid(string e) => e is "none" or "kenBurns";
    private static bool IsBackgroundEffectValid(string e) => e is "none" or "blur" or "ambilight";
    private static bool IsImageQualityValid(string q) => q is "preview" or "original";
    private static bool IsQualityValid(QualitySettings quality) =>
        quality.MinFileSizeKb is null or >= 0;
    private static bool IsSlideshowValid(SlideshowSettings s) =>
        s.IntervalSeconds is >= 3 and <= 3600 &&
        s.RefreshIntervalMinutes is >= 5 and <= 1440;
}
