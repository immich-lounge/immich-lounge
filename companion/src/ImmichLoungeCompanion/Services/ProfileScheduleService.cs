using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ImmichLoungeCompanion.Models;
using ImmichLoungeCompanion.Storage;

namespace ImmichLoungeCompanion.Services;

public class ProfileScheduleService(IProfileRepository profiles, TimeProvider clock)
{
    public static bool ValidId(string? id) => id != null && Regex.IsMatch(id, @"^[a-z0-9][a-z0-9\-]{1,62}[a-z0-9]$");

    public static string? Validate(ProfileSchedule? schedule)
    {
        if (schedule == null || !ValidId(schedule.Id))
        {
            return "Use a schedule ID with lowercase letters, numbers and hyphens, 3–64 characters.";
        }
        if (string.IsNullOrWhiteSpace(schedule.Name) || schedule.Name.Length > 100)
        {
            return "Give the schedule a name of 1–100 characters.";
        }
        if (!ValidId(schedule.FallbackProfileId))
        {
            return "Choose a fallback profile.";
        }
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(schedule.TimeZoneId ?? "", out _))
        {
            return "Choose a valid schedule time zone.";
        }
        if (schedule.Entries == null || schedule.Entries.Count > 32)
        {
            return "A schedule can have at most 32 time slots.";
        }
        foreach (var entry in schedule.Entries)
        {
            if (entry == null || !ValidId(entry.ProfileId))
            {
                return "Choose a profile for every schedule time slot.";
            }
            if (entry.Days == null || entry.Days.Count == 0 || entry.Days.Any(day => !Enum.IsDefined(day)))
            {
                return "Choose at least one valid day for every schedule time slot.";
            }
            if (!TryTime(entry.Start, out _) || !TryTime(entry.End, out _))
            {
                return "Schedule times must use HH:mm (00:00–23:59).";
            }
        }
        return null;
    }

    public async Task<string?> ValidateTargetsAsync(ProfileSchedule schedule)
    {
        var ids = schedule.Entries.Select(entry => entry.ProfileId).Append(schedule.FallbackProfileId).Distinct();
        foreach (var id in ids)
        {
            if (await profiles.GetAsync(id) == null)
            {
                return $"Profile '{id}' is missing or cannot be read.";
            }
        }
        return null;
    }

    public async Task<Profile?> ResolveAsync(ProfileSchedule schedule)
    {
        if (Validate(schedule) != null)
        {
            return null;
        }
        if (schedule.Enabled)
        {
            var local = TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById(schedule.TimeZoneId));
            var time = TimeOnly.FromDateTime(local.DateTime);
            var day = (ScheduleDay)local.DayOfWeek;
            var previousDay = (ScheduleDay)(((int)local.DayOfWeek + 6) % 7);
            foreach (var entry in schedule.Entries)
            {
                TryTime(entry.Start, out var start);
                TryTime(entry.End, out var end);
                var active = start < end
                    ? entry.Days.Contains(day) && time >= start && time < end
                    : (entry.Days.Contains(day) && time >= start) || (entry.Days.Contains(previousDay) && time < end);
                if (active)
                {
                    return await profiles.GetAsync(entry.ProfileId) ?? await profiles.GetAsync(schedule.FallbackProfileId);
                }
            }
        }
        return await profiles.GetAsync(schedule.FallbackProfileId);
    }

    private static bool TryTime(string? value, out TimeOnly time) =>
        TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
}
