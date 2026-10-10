using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using ImmichLoungeCompanion.Immich.Dtos;

namespace ImmichLoungeCompanion.Immich;

internal static class SearchRequestSerializer
{
    // Keep the playlist collector independent of the server's wire format.
    public static string Serialize(SearchAssetsRequest request, bool structured, int page, string? cursor,
        JsonSerializerOptions options)
    {
        var after = NormalizeDate(request.TakenAfter);
        var before = NormalizeDate(request.TakenBefore);
        if (!structured)
        {
            return JsonSerializer.Serialize(new
            {
                request.AlbumIds, request.PersonIds, request.TagIds,
                TakenAfter = after, TakenBefore = before, request.Type,
                request.Country, request.State, request.City,
                request.WithExif, Page = page, request.Size
            }, options);
        }

        var filter = new Dictionary<string, object>();
        // Legacy ID lists require membership in ALL entries, not ANY.
        AddIds(filter, "albumIds", request.AlbumIds);
        AddIds(filter, "personIds", request.PersonIds);
        AddIds(filter, "tagIds", request.TagIds);
        AddEquals(filter, "type", request.Type);
        AddEquals(filter, "country", request.Country);
        AddEquals(filter, "state", request.State);
        AddEquals(filter, "city", request.City);
        var dates = new Dictionary<string, string>();
        if (after != null)
        {
            dates["gte"] = after;
        }

        if (before != null)
        {
            dates["lte"] = before;
        }

        if (dates.Count > 0)
        {
            filter["takenAt"] = dates;
        }

        // Sending filter (even when empty) selects v2. Never mix flat fields/page with it.
        return JsonSerializer.Serialize(new { Filter = filter, Cursor = cursor, request.WithExif, request.Size }, options);
    }

    private static void AddIds(Dictionary<string, object> filter, string name, List<string>? ids)
    {
        if (ids is { Count: > 0 })
        {
            filter[name] = new { All = ids };
        }
    }

    private static void AddEquals(Dictionary<string, object> filter, string name, string? value)
    {
        if (value != null)
        {
            filter[name] = new { Eq = value };
        }
    }

    private static string? NormalizeDate(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }
        // Older profiles store date-only values. Preserve midnight UTC and inclusive boundaries.
        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "T00:00:00.000Z";
        }

        return value;
    }
}