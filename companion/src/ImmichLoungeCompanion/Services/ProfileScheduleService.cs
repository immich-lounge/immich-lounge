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
    private static readonly Regex ValidId = new(@"^[a-z0-9][a-z0-9\-]{1,62}[a-z0-9]$", RegexOptions.Compiled);

    public static string? Validate(ProfileSchedule? schedule)
    {
        if (schedule == null)
        {
            return null;
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
            if (entry == null || !ValidId.IsMatch(entry.ProfileId ?? ""))
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

    public async Task<string?> ValidateTargetsAsync(Profile profile)
    {
        if (profile.Schedule?.Enabled != true)
        {
            return null;
        }

        foreach (var id in profile.Schedule.Entries.Select(entry => entry.ProfileId).Distinct())
        {
            if (id == profile.Id)
            {
                return "A schedule time slot must use another profile.";
            }

            if (!await profiles.ExistsAsync(id))
            {
                return $"Scheduled profile '{id}' does not exist.";
            }
        }
        return null;
    }

    public async Task<Profile> ResolveAsync(Profile selected)
    {
        var schedule = selected.Schedule;
        if (schedule?.Enabled != true || Validate(schedule) != null)
        {
            return selected;
        }

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
            if (!active)
            {
                continue;
            }
            // Resolve only one level; the target's own schedule is not followed.
            return await profiles.GetAsync(entry.ProfileId) ?? selected;
        }
        return selected;
    }

    private static bool TryTime(string? value, out TimeOnly time) =>
        TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
}
