using System;
using System.Threading.Tasks;
using ImmichLoungeCompanion.Models;
using ImmichLoungeCompanion.Services;
using ImmichLoungeCompanion.Storage;
using Microsoft.AspNetCore.Mvc;

namespace ImmichLoungeCompanion.Api;

[ApiController]
[Route("api/profiles/{id}/clock")]
public class ClockController(
    IProfileRepository profiles,
    IDisplayDateFormattingService dateFormatting) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(string id)
    {
        var profile = await profiles.GetAsync(id);
        if (profile == null)
        {
            return NotFound(new { error = "Profile not found." });
        }

        var today = DateOnly.FromDateTime(DateTime.Now);
        var dateIso = today.ToString("yyyy-MM-dd");
        string? formattedDate = null;
        if (profile.Display.FormatSource == DisplayFormatSource.Profile)
        {
            formattedDate = dateFormatting.FormatDate(today, profile.Display);
        }

        return Ok(new { dateIso, formattedDate });
    }
}
