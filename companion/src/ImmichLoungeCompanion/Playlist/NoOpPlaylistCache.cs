using ImmichLoungeCompanion.Models;

namespace ImmichLoungeCompanion.Playlist;

public class NoOpPlaylistCache : IPlaylistCache
{
    public void Invalidate(string profileId) { }
    public void InvalidateAll() { }
    public PlaylistCacheEntry? Get(string profileId) => null;
    public void Set(string profileId, PlaylistCacheEntry entry) { }
    public long GetGeneration(string profileId) => 0;
    public bool TrySet(string profileId, PlaylistCacheEntry entry, long generation) => true;
    public bool IsBuilding(string profileId) => false;
    public bool TryStartBuilding(string profileId) => true;
    public void ClearBuilding(string profileId) { }
}
