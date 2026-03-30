using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ImmichLoungeCompanion.Models;

namespace ImmichLoungeCompanion.Storage;

public class JsonProfileRepository(string dataDirectory) : IProfileRepository
{
    private readonly string _profilesDir = Path.Combine(dataDirectory, "profiles");
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private string FilePath(string id) => Path.Combine(_profilesDir, $"{id}.json");

    public async Task<List<Profile>> GetAllAsync()
    {
        if (!Directory.Exists(_profilesDir))
        {
            return [];
        }

        var files = Directory.GetFiles(_profilesDir, "*.json");
        var results = new List<Profile>();
        foreach (var file in files)
        {
            var json = await File.ReadAllTextAsync(file);
            var p = JsonSerializer.Deserialize<Profile>(json, JsonOptions);
            if (p != null)
            {
                results.Add(p);
            }
        }
        return results;
    }

    public async Task<Profile?> GetAsync(string id)
    {
        var path = FilePath(id);
        if (!File.Exists(path))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<Profile>(json, JsonOptions);
    }

    public Task<bool> ExistsAsync(string id) => Task.FromResult(File.Exists(FilePath(id)));

    public async Task SaveAsync(Profile profile)
    {
        Directory.CreateDirectory(_profilesDir);
        var json = JsonSerializer.Serialize(profile, JsonOptions);
        await File.WriteAllTextAsync(FilePath(profile.Id), json);
    }

    public Task<bool> DeleteAsync(string id)
    {
        var path = FilePath(id);
        if (!File.Exists(path))
        {
            return Task.FromResult(false);
        }

        File.Delete(path);
        return Task.FromResult(true);
    }
}
