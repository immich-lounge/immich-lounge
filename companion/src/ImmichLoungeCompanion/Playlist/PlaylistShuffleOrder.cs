using System;
using System.Collections.Generic;
using System.Linq;
using ImmichLoungeCompanion.Models;

namespace ImmichLoungeCompanion.Playlist;

public static class PlaylistShuffleOrder
{
    // A device's cycle seed travels in its playlist version. Repeated windows and
    // refreshes use the same permutation without changing the shared cache.
    public static List<PlaylistEntry> ForCycle(IReadOnlyList<PlaylistEntry> assets, int seed)
    {
        var entries = assets.ToList();
        var random = new Random(seed);
        for (var i = entries.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (entries[i], entries[j]) = (entries[j], entries[i]);
        }
        return entries;
    }

    public static List<PlaylistEntry> PreserveExistingOrder(
        IReadOnlyList<PlaylistEntry> existingOrder,
        List<PlaylistEntry> rebuiltAssets)
    {
        if (existingOrder.Count == 0 || rebuiltAssets.Count <= 1)
        {
            return rebuiltAssets;
        }

        var remaining = rebuiltAssets.ToDictionary(entry => entry.Id, StringComparer.Ordinal);
        var ordered = new List<PlaylistEntry>(rebuiltAssets.Count);

        foreach (var entry in existingOrder)
        {
            if (remaining.Remove(entry.Id, out var rebuilt))
            {
                ordered.Add(rebuilt);
            }
        }

        if (remaining.Count == 0)
        {
            return ordered;
        }

        var additions = remaining.Values.ToList();
        Shuffle(additions);
        ordered.AddRange(additions);
        return ordered;
    }

    private static void Shuffle(List<PlaylistEntry> entries)
    {
        for (var i = entries.Count - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (entries[i], entries[j]) = (entries[j], entries[i]);
        }
    }
}
