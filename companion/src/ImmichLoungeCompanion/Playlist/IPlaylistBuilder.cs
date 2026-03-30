using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ImmichLoungeCompanion.Models;

namespace ImmichLoungeCompanion.Playlist;

public interface IPlaylistBuilder
{
    Task<List<PlaylistEntry>> BuildAsync(Profile profile, CancellationToken ct = default);
}
