using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ImmichLoungeCompanion.Models;
using ImmichLoungeCompanion.Storage;

namespace ImmichLoungeCompanion.Playlist;

public class PlaylistBuilder(ISettingsRepository settings, PlaylistAssetCollector collector) : IPlaylistBuilder
{
    public async Task<List<PlaylistEntry>> BuildAsync(Profile profile, CancellationToken ct = default)
    {
        var globalSettings = await settings.LoadAsync();
        var immichSettings = globalSettings.Immich;
        var allAssets = await collector.CollectAsync(profile, immichSettings, ct);
        return PlaylistEntryProjector.CreateEntries(allAssets, profile);
    }
}
