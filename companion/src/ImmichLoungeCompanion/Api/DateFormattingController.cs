using System;
using System.Globalization;
using System.Threading.Tasks;
using ImmichLoungeCompanion.Models;
using ImmichLoungeCompanion.Services;
using ImmichLoungeCompanion.Storage;
using Microsoft.AspNetCore.Mvc;

namespace ImmichLoungeCompanion.Api;

[ApiController]
[Route("api/profiles/{id}/format-date")]
public class DateFormattingController(
    IProfileRepository profiles,
    IDisplayDateFormattingService dateFormatting) : ControllerBase
{
    public record DateFormatRequest(string Value);

    [HttpPost]
    public async Task<IActionResult> Post(string id, [FromBody] DateFormatRequest request)
    {
        var profile = await profiles.GetAsync(id);
        if (profile == null)
        {
            return NotFound(new { error = "Profile not found." });
        }

        if (profile.Display.FormatSource != DisplayFormatSource.Profile)
        {
            return Ok(new { formattedDate = (string?)null });
        }

        if (!TryParseDateOnly(request.Value, out var date))
        {
            return BadRequest(new { error = "Invalid date value." });
        }

        return Ok(new
        {
            formattedDate = dateFormatting.FormatDate(date, profile.Display)
        });
    }

    private static bool TryParseDateOnly(string? value, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var datePortion = value;
        var timeSeparator = datePortion.IndexOf('T');
        if (timeSeparator >= 0)
        {
            datePortion = datePortion[..timeSeparator];
        }

        var spaceSeparator = datePortion.IndexOf(' ');
        if (spaceSeparator >= 0)
        {
            datePortion = datePortion[..spaceSeparator];
        }

        return DateOnly.TryParse(datePortion, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }
}
