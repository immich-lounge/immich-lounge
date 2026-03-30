using System;
using ImmichLoungeCompanion.Models;

namespace ImmichLoungeCompanion.Services;

public interface IDisplayDateFormattingService
{
    string FormatDate(DateOnly date, DisplaySettings display);
    string FormatDate(DateOnly date, string? dateFormat, string? locale);
}
