using System;
using System.Collections.Generic;
using ImmichLoungeCompanion.Models;

namespace ImmichLoungeCompanion.Playlist;

public interface IPlaylistCache
{
    void Invalidate(string profileId);
    void InvalidateAll();
    PlaylistCacheEntry? Get(string profileId);
    void Set(string profileId, PlaylistCacheEntry entry);
    // Capture before reading build inputs; publication succeeds only if no invalidation intervened.
    long GetGeneration(string profileId);
    bool TrySet(string profileId, PlaylistCacheEntry entry, long generation);
    bool IsBuilding(string profileId);
    bool TryStartBuilding(string profileId);
    void ClearBuilding(string profileId);
}

public record PlaylistCacheEntry(
    List<PlaylistEntry> Assets,
    DateTimeOffset GeneratedAt,
    string? PlaylistVersion = null
);
