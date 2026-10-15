using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using ImmichLoungeCompanion.Immich.Dtos;
using ImmichLoungeCompanion.Models;

namespace ImmichLoungeCompanion.Playlist;

public static class PlaylistEntryProjector
{
    public static List<PlaylistEntry> CreateEntries(
        Dictionary<string, CollectedAsset> allAssets,
        Profile profile)
    {
        if (profile.NewestPhotoCount is { } newestCount)
        {
            // Apply the cap only to playable matches, before shuffle and memory priority.
            // Hidden assets and disabled media types must not consume the limit.
            allAssets = allAssets
                .Where(kvp => MapToEntry(kvp.Key, kvp.Value, profile.MediaTypes) != null)
                .OrderByDescending(kvp => CaptureDate(kvp.Value.Asset))
                .ThenBy(kvp => kvp.Key, StringComparer.Ordinal)
                .Take(newestCount)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.Ordinal);
        }

        var entries = allAssets
            .Select(kvp => MapToEntry(kvp.Key, kvp.Value, profile.MediaTypes))
            .Where(entry => entry != null)
            .Cast<PlaylistEntry>()
            .ToList();

        if (profile.Slideshow.Shuffle)
        {
            Shuffle(entries);
            return Prioritize(entries, profile);
        }

        var orderedAssets = profile.NewestPhotoCount != null
            ? profile.Slideshow.DateOrder == "oldestFirst"
                ? allAssets.OrderBy(kvp => CaptureDate(kvp.Value.Asset))
                : allAssets.OrderByDescending(kvp => CaptureDate(kvp.Value.Asset))
            : profile.Slideshow.DateOrder == "oldestFirst"
                ? allAssets.OrderBy(kvp => kvp.Value.Asset.ExifInfo?.DateTimeOriginal ?? "")
                : allAssets.OrderByDescending(kvp => kvp.Value.Asset.ExifInfo?.DateTimeOriginal ?? "");

        return Prioritize(orderedAssets
            .ThenBy(kvp => kvp.Key, StringComparer.Ordinal)
            .Select(kvp => MapToEntry(kvp.Key, kvp.Value, profile.MediaTypes))
            .Where(entry => entry != null)
            .Cast<PlaylistEntry>()
            .ToList(), profile);
    }

    public static List<PlaylistEntry> Prioritize(List<PlaylistEntry> entries, Profile profile) =>
        profile.Memories.Prioritize ? entries.OrderByDescending(entry => entry.IsMemory).ToList() : entries;

    private static DateTimeOffset CaptureDate(ImmichAsset asset)
    {
        foreach (var date in new[] { asset.ExifInfo?.DateTimeOriginal, asset.FileCreatedAt })
        {
            if (DateTimeOffset.TryParse(date, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)) { return parsed; }
        }
        return DateTimeOffset.MinValue;
    }

    private static void Shuffle(List<PlaylistEntry> entries)
    {
        for (var i = entries.Count - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (entries[i], entries[j]) = (entries[j], entries[i]);
        }
    }

    private static PlaylistEntry? MapToEntry(string id, CollectedAsset collected, MediaTypes filter)
    {
        var asset = collected.Asset;
        // Covers regular search, memories, and servers using the legacy search format.
        // Archived photos retain their existing eligibility.
        if (asset.Visibility is "hidden" or "locked") { return null; }
        var sourceLabel = collected.SourceLabel;
        bool isLivePhoto = asset.LivePhotoVideoId != null;
        bool isVideo = asset.Type == "VIDEO";

        if (isLivePhoto && filter.LivePhotos)
        {
            return new PlaylistEntry(id, "livePhoto", sourceLabel, asset.LivePhotoVideoId, collected.BirthdayCaption, collected.IsMemory);
        }

        if (isLivePhoto && !filter.LivePhotos && filter.Photos)
        {
            return new PlaylistEntry(id, "photo", sourceLabel, null, collected.BirthdayCaption, collected.IsMemory);
        }

        if (isVideo && filter.Videos)
        {
            return new PlaylistEntry(id, "video", sourceLabel, null, collected.BirthdayCaption, collected.IsMemory);
        }

        if (!isVideo && !isLivePhoto && filter.Photos)
        {
            return new PlaylistEntry(id, "photo", sourceLabel, null, collected.BirthdayCaption, collected.IsMemory);
        }

        return null;
    }
}
