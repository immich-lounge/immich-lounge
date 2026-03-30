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
    bool IsBuilding(string profileId);
    bool TryStartBuilding(string profileId);
    void ClearBuilding(string profileId);
}

public record PlaylistCacheEntry(
    List<PlaylistEntry> Assets,
    DateTimeOffset GeneratedAt
);
