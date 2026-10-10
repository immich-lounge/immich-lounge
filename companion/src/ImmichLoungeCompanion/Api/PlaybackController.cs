using System.Linq;
using System.Threading.Tasks;
using ImmichLoungeCompanion.Services;
using ImmichLoungeCompanion.Storage;
using Microsoft.AspNetCore.Mvc;

namespace ImmichLoungeCompanion.Api;

[ApiController]
[Route("api/playback")]
public class PlaybackController(IProfileRepository profiles, IScheduleRepository schedules,
    ProfileScheduleService resolver, IProfileDocumentService documents) : ControllerBase
{
    [HttpGet("selections")]
    public async Task<IActionResult> Selections()
    {
        var profileItems = (await profiles.GetAllAsync()).Select(profile => new
        {
            profile.Id, profile.Name, profile.Description, Kind = "profile"
        });
        var scheduleItems = (await schedules.GetAllAsync()).Select(schedule => new
        {
            Id = "schedule:" + schedule.Id, schedule.Name,
            Description = (string?)(schedule.Enabled ? "Weekly schedule" : "Disabled — fallback profile"), Kind = "schedule"
        });
        return Ok(profileItems.Concat(scheduleItems));
    }

    [HttpGet("selections/{selectionId}")]
    public async Task<IActionResult> Get(string selectionId)
    {
        if (selectionId.StartsWith("schedule:"))
        {
            var id = selectionId[9..];
            if (!ProfileScheduleService.ValidId(id)) { return NotFound(); }
            var schedule = await schedules.GetAsync(id);
            if (schedule == null) { return NotFound(new { error = "Schedule not found." }); }
            var active = await resolver.ResolveAsync(schedule);
            if (active == null) { return StatusCode(503, new { error = "The schedule's active and fallback profiles are unavailable." }); }
            return Ok(await documents.BuildResponseAsync(active, selectionId, followingSchedule: true, selectionName: schedule.Name));
        }
        if (!ProfileScheduleService.ValidId(selectionId)) { return NotFound(); }
        var profile = await profiles.GetAsync(selectionId);
        return profile == null ? NotFound(new { error = "Profile not found." }) : Ok(await documents.BuildResponseAsync(profile));
    }
}
