using System;
using ImmichLoungeCompanion.Models;

namespace ImmichLoungeCompanion.Playlist;

public static class MemoryDate
{
    public static DateOnly For(Profile profile, DateTimeOffset instant)
    {
        var zone = string.IsNullOrWhiteSpace(profile.Memories.TimeZoneId)
            ? TimeZoneInfo.Local : TimeZoneInfo.FindSystemTimeZoneById(profile.Memories.TimeZoneId);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime);
    }
}
