using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ImmichLoungeCompanion.Models;

namespace ImmichLoungeCompanion.Playlist;

public class PlaylistCache(string dataDirectory) : IPlaylistCache
{
    private readonly ConcurrentDictionary<string, PlaylistCacheEntry> _cache = new();
    // Tracks profile IDs currently being rebuilt. This behaves like a concurrent set.
    private readonly ConcurrentDictionary<string, byte> _buildingProfiles = new();
    private readonly string _cacheDir = Path.Combine(dataDirectory, "cache");
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public PlaylistCacheEntry? Get(string profileId)
    {
        if (_cache.TryGetValue(profileId, out var entry))
        {
            return entry;
        }

        return TryLoadFromDisk(profileId);
    }

    public void Set(string profileId, PlaylistCacheEntry entry)
    {
        _cache[profileId] = entry;
        _ = Task.Run(() => PersistToDiskAsync(profileId, entry));
    }

    public void Invalidate(string profileId)
    {
        _cache.TryRemove(profileId, out _);
        var path = CachePath(profileId);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public void InvalidateAll()
    {
        _cache.Clear();
        if (Directory.Exists(_cacheDir))
        {
            foreach (var f in Directory.GetFiles(_cacheDir, "*.json"))
            {
                File.Delete(f);
            }
        }
    }

    public bool IsBuilding(string profileId) => _buildingProfiles.ContainsKey(profileId);

    public bool TryStartBuilding(string profileId) => _buildingProfiles.TryAdd(profileId, 0);

    public void ClearBuilding(string profileId) => _buildingProfiles.TryRemove(profileId, out _);

    private PlaylistCacheEntry? TryLoadFromDisk(string profileId)
    {
        var path = CachePath(profileId);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(path);
            var entry = JsonSerializer.Deserialize<PlaylistCacheEntry>(json, JsonOptions);
            if (entry != null)
            {
                _cache[profileId] = entry;
            }

            return entry;
        }
        catch { return null; }
    }

    private async Task PersistToDiskAsync(string profileId, PlaylistCacheEntry entry)
    {
        try
        {
            Directory.CreateDirectory(_cacheDir);
            var json = JsonSerializer.Serialize(entry, JsonOptions);
            await File.WriteAllTextAsync(CachePath(profileId), json);
        }
        catch { /* best-effort */ }
    }

    private string CachePath(string profileId) => Path.Combine(_cacheDir, $"{profileId}.json");
}
