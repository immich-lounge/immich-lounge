using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ImmichLoungeCompanion.Models;

namespace ImmichLoungeCompanion.Storage;

public class JsonSettingsRepository(string dataDirectory) : ISettingsRepository
{
    private readonly string _filePath = Path.Combine(dataDirectory, "settings.json");
    private readonly SemaphoreSlim _lock = new(1, 1);
    private GlobalSettings? _cachedDefaults;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public async Task<GlobalSettings> LoadAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (!File.Exists(_filePath))
            {
                return _cachedDefaults ??= new GlobalSettings();
            }

            var json = await File.ReadAllTextAsync(_filePath);
            var settings = JsonSerializer.Deserialize<GlobalSettings>(json, JsonOptions) ?? new GlobalSettings();
            _cachedDefaults = settings;
            return settings;
        }
        finally { _lock.Release(); }
    }

    public async Task SaveAsync(GlobalSettings settings)
    {
        await _lock.WaitAsync();
        try { await WriteAsync(settings); }
        finally { _lock.Release(); }
    }

    private async Task WriteAsync(GlobalSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        await File.WriteAllTextAsync(_filePath, json);
        _cachedDefaults = settings;
    }
}
