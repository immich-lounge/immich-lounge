using ImmichLoungeCompanion.Models;

namespace ImmichLoungeCompanion.Playlist;

public class NoOpPlaylistCache : IPlaylistCache
{
    public void Invalidate(string profileId) { }
    public void InvalidateAll() { }
    public PlaylistCacheEntry? Get(string profileId) => null;
    public void Set(string profileId, PlaylistCacheEntry entry) { }
    public bool IsBuilding(string profileId) => false;
    public bool TryStartBuilding(string profileId) => true;
    public void ClearBuilding(string profileId) { }
}
