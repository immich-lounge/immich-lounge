using System.Threading.Tasks;
using ImmichLoungeCompanion.Models;
using ImmichLoungeCompanion.Services;
using ImmichLoungeCompanion.Storage;
using Microsoft.AspNetCore.Mvc;

namespace ImmichLoungeCompanion.Api;

[ApiController]
[Route("api/schedules")]
public class SchedulesController(IScheduleRepository schedules, ProfileScheduleService validation) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List() => Ok(await schedules.GetAllAsync());

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id)
    {
        if (!ProfileScheduleService.ValidId(id)) { return NotFound(); }
        var schedule = await schedules.GetAsync(id);
        return schedule == null ? NotFound(new { error = "Schedule not found." }) : Ok(schedule);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ProfileSchedule schedule)
    {
        var error = ProfileScheduleService.Validate(schedule) ?? await validation.ValidateTargetsAsync(schedule);
        if (error != null) { return BadRequest(new { error }); }
        if (await schedules.GetAsync(schedule.Id) != null) { return Conflict(new { error = "Schedule ID already exists." }); }
        await schedules.SaveAsync(schedule);
        return Created($"/api/schedules/{schedule.Id}", schedule);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] ProfileSchedule schedule)
    {
        if (!ProfileScheduleService.ValidId(id) || await schedules.GetAsync(id) == null) { return NotFound(new { error = "Schedule not found." }); }
        schedule.Id = id;
        var error = ProfileScheduleService.Validate(schedule) ?? await validation.ValidateTargetsAsync(schedule);
        if (error != null) { return BadRequest(new { error }); }
        await schedules.SaveAsync(schedule);
        return Ok(schedule);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!ProfileScheduleService.ValidId(id)) { return NotFound(); }
        return await schedules.DeleteAsync(id) ? NoContent() : NotFound();
    }
}
