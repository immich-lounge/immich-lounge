using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ImmichLoungeCompanion.Models;

namespace ImmichLoungeCompanion.Playlist;

public class PlaylistCache(string dataDirectory) : IPlaylistCache
{
    private readonly ConcurrentDictionary<string, PlaylistCacheEntry> _cache = new();
    // Publication, disk access, and invalidation share a gate so deleted entries cannot return.
    private readonly object _gate = new();
    private readonly Dictionary<string, long> _generations = new();
    private long _nextGeneration;
    private long _allGeneration;
    // Tracks profile IDs currently being rebuilt. This behaves like a concurrent set.
    private readonly ConcurrentDictionary<string, byte> _buildingProfiles = new();
    private readonly string _cacheDir = Path.Combine(dataDirectory, "cache");
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public PlaylistCacheEntry? Get(string profileId)
    {
        lock (_gate)
        {
            if (_cache.TryGetValue(profileId, out var entry))
            {
                return entry;
            }

            return TryLoadFromDisk(profileId);
        }
    }

    public void Set(string profileId, PlaylistCacheEntry entry)
    {
        lock (_gate)
        {
            _cache[profileId] = entry;
            _ = Task.Run(() => PersistToDiskAsync(profileId, entry));
        }
    }

    public long GetGeneration(string profileId)
    {
        lock (_gate)
        {
            return _generations.GetValueOrDefault(profileId, _allGeneration);
        }
    }

    public bool TrySet(string profileId, PlaylistCacheEntry entry, long generation)
    {
        lock (_gate)
        {
            if (_generations.GetValueOrDefault(profileId, _allGeneration) != generation)
            {
                return false;
            }
            Set(profileId, entry);
            return true;
        }
    }

    public void Invalidate(string profileId)
    {
        lock (_gate)
        {
            _generations[profileId] = ++_nextGeneration;
            _cache.TryRemove(profileId, out _);
            var path = CachePath(profileId);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    public void InvalidateAll()
    {
        lock (_gate)
        {
            _allGeneration = ++_nextGeneration;
            _generations.Clear();
            _cache.Clear();
            if (Directory.Exists(_cacheDir))
            {
                foreach (var f in Directory.GetFiles(_cacheDir, "*.json"))
                {
                    File.Delete(f);
                }
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

    private Task PersistToDiskAsync(string profileId, PlaylistCacheEntry entry)
    {
        try
        {
            var json = JsonSerializer.Serialize(entry, JsonOptions);
            lock (_gate)
            {
                // A queued write may belong to an invalidated or superseded entry.
                if (_cache.TryGetValue(profileId, out var current) && ReferenceEquals(current, entry))
                {
                    Directory.CreateDirectory(_cacheDir);
                    File.WriteAllText(CachePath(profileId), json);
                }
            }
        }
        catch { /* best-effort */ }
        return Task.CompletedTask;
    }

    private string CachePath(string profileId) => Path.Combine(_cacheDir, $"{profileId}.json");
}
