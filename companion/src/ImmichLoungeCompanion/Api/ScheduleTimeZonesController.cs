using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;

namespace ImmichLoungeCompanion.Api;

[ApiController]
[Route("api/schedule-time-zones")]
public class ScheduleTimeZonesController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(TimeZoneInfo.GetSystemTimeZones().Select(zone => new { zone.Id, zone.DisplayName }));
}
