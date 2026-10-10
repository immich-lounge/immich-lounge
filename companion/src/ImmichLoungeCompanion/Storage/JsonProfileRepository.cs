using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ImmichLoungeCompanion.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ImmichLoungeCompanion.Storage;

public class JsonProfileRepository(string dataDirectory, ILogger<JsonProfileRepository>? logger = null) : IProfileRepository
{
    private readonly string _profilesDir = Path.Combine(dataDirectory, "profiles");
    private readonly ILogger<JsonProfileRepository> _logger = logger ?? NullLogger<JsonProfileRepository>.Instance;
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
            var p = await ReadProfileAsync(file);
            if (p != null)
            {
                results.Add(p);
            }
        }
        return results;
    }

    public Task<Profile?> GetAsync(string id) => ReadProfileAsync(FilePath(id));

    private async Task<Profile?> ReadProfileAsync(string path)
    {
        try
        {
            // Delete sharing lets saves replace the file while readers finish their old snapshot.
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.Read | FileShare.Delete, 4096, FileOptions.Asynchronous);
            var profile = await JsonSerializer.DeserializeAsync<Profile>(stream, JsonOptions);
            if (profile == null || string.IsNullOrWhiteSpace(profile.Id) || profile.Slideshow == null)
            {
                _logger.LogWarning("Could not read profile file {ProfilePath}; required profile data is missing, leaving it unchanged and skipping it", path);
                return null;
            }
            return profile;
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger.LogWarning(ex, "Could not read profile file {ProfilePath}; leaving it unchanged and skipping it", path);
            return null;
        }
    }

    public Task<bool> ExistsAsync(string id) => Task.FromResult(File.Exists(FilePath(id)));

    public async Task SaveAsync(Profile profile)
    {
        Directory.CreateDirectory(_profilesDir);
        var json = JsonSerializer.Serialize(profile, JsonOptions);
        var path = FilePath(profile.Id);
        var temporaryPath = Path.Combine(_profilesDir, $".{profile.Id}.{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllTextAsync(temporaryPath, json);
            if (File.Exists(path))
            {
                File.Replace(temporaryPath, path, destinationBackupFileName: null);
            }
            else
            {
                File.Move(temporaryPath, path);
            }
        }
        finally
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Could not remove temporary profile file {ProfilePath}", temporaryPath);
            }
        }
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
