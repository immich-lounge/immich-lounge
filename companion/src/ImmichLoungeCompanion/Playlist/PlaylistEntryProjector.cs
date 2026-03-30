using System;
using System.Collections.Generic;
using System.Linq;
using ImmichLoungeCompanion.Immich.Dtos;
using ImmichLoungeCompanion.Models;

namespace ImmichLoungeCompanion.Playlist;

public static class PlaylistEntryProjector
{
    public static List<PlaylistEntry> CreateEntries(
        Dictionary<string, (ImmichAsset Asset, string? SourceLabel)> allAssets,
        Profile profile)
    {
        var entries = allAssets
            .Select(kvp => MapToEntry(kvp.Key, kvp.Value.Asset, kvp.Value.SourceLabel, profile.MediaTypes))
            .Where(entry => entry != null)
            .Cast<PlaylistEntry>()
            .ToList();

        if (profile.Slideshow.Shuffle)
        {
            Shuffle(entries);
            return entries;
        }

        return allAssets
            .OrderByDescending(kvp => kvp.Value.Asset.ExifInfo?.DateTimeOriginal ?? "")
            .ThenBy(kvp => kvp.Key, StringComparer.Ordinal)
            .Select(kvp => MapToEntry(kvp.Key, kvp.Value.Asset, kvp.Value.SourceLabel, profile.MediaTypes))
            .Where(entry => entry != null)
            .Cast<PlaylistEntry>()
            .ToList();
    }

    private static void Shuffle(List<PlaylistEntry> entries)
    {
        for (var i = entries.Count - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (entries[i], entries[j]) = (entries[j], entries[i]);
        }
    }

    private static PlaylistEntry? MapToEntry(string id, ImmichAsset asset, string? sourceLabel, MediaTypes filter)
    {
        bool isLivePhoto = asset.LivePhotoVideoId != null;
        bool isVideo = asset.Type == "VIDEO";

        if (isLivePhoto && filter.LivePhotos)
        {
            return new PlaylistEntry(id, "livePhoto", sourceLabel, asset.LivePhotoVideoId);
        }

        if (isLivePhoto && !filter.LivePhotos && filter.Photos)
        {
            return new PlaylistEntry(id, "photo", sourceLabel, null);
        }

        if (isVideo && filter.Videos)
        {
            return new PlaylistEntry(id, "video", sourceLabel, null);
        }

        if (!isVideo && !isLivePhoto && filter.Photos)
        {
            return new PlaylistEntry(id, "photo", sourceLabel, null);
        }

        return null;
    }
}
