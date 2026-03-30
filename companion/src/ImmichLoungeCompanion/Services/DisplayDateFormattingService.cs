using System;
using System.Globalization;
using ImmichLoungeCompanion.Models;

namespace ImmichLoungeCompanion.Services;

public class DisplayDateFormattingService : IDisplayDateFormattingService
{
    public string FormatDate(DateOnly date, DisplaySettings display)
        => FormatDate(date, display.DateFormat, display.Locale);

    public string FormatDate(DateOnly date, string? dateFormat, string? locale)
    {
        var effectiveFormat = string.IsNullOrWhiteSpace(dateFormat) ? "d MMMM yyyy" : dateFormat;
        var effectiveLocale = NormalizeLocale(locale);
        var culture = GetSafeCulture(effectiveLocale);

        return effectiveFormat switch
        {
            "MMMM d, yyyy" => date.ToString("MMMM d, yyyy", culture),
            "dd/MM/yyyy" => date.ToString("dd/MM/yyyy", culture),
            "MM/dd/yyyy" => date.ToString("MM/dd/yyyy", culture),
            "yyyy-MM-dd" => date.ToString("yyyy-MM-dd", culture),
            "dddd, d MMMM" => date.ToString("dddd, d MMMM", culture),
            _ => FormatDefaultDate(date, effectiveLocale, culture)
        };
    }

    private static string FormatDefaultDate(DateOnly date, string locale, CultureInfo culture)
    {
        if (string.Equals(locale, "de-DE", StringComparison.OrdinalIgnoreCase))
        {
            return $"{date.Day}. {date.ToString("MMMM yyyy", culture)}";
        }

        return date.ToString("d MMMM yyyy", culture);
    }

    private static string NormalizeLocale(string? locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
        {
            return "en-US";
        }

        var normalized = locale.Replace("_", "-", StringComparison.Ordinal);
        return normalized.Length switch
        {
            2 when normalized.Equals("de", StringComparison.OrdinalIgnoreCase) => "de-DE",
            2 when normalized.Equals("nl", StringComparison.OrdinalIgnoreCase) => "nl-NL",
            2 when normalized.Equals("fr", StringComparison.OrdinalIgnoreCase) => "fr-FR",
            2 when normalized.Equals("es", StringComparison.OrdinalIgnoreCase) => "es-ES",
            2 when normalized.Equals("it", StringComparison.OrdinalIgnoreCase) => "it-IT",
            2 when normalized.Equals("pt", StringComparison.OrdinalIgnoreCase) => "pt-BR",
            2 when normalized.Equals("sv", StringComparison.OrdinalIgnoreCase) => "sv-SE",
            2 when normalized.Equals("da", StringComparison.OrdinalIgnoreCase) => "da-DK",
            2 when normalized.Equals("no", StringComparison.OrdinalIgnoreCase) || normalized.Equals("nb", StringComparison.OrdinalIgnoreCase) => "nb-NO",
            2 when normalized.Equals("fi", StringComparison.OrdinalIgnoreCase) => "fi-FI",
            2 when normalized.Equals("pl", StringComparison.OrdinalIgnoreCase) => "pl-PL",
            2 when normalized.Equals("ja", StringComparison.OrdinalIgnoreCase) => "ja-JP",
            2 when normalized.Equals("zh", StringComparison.OrdinalIgnoreCase) => "zh-CN",
            2 => $"{normalized}-{normalized.ToUpperInvariant()}",
            _ => normalized
        };
    }

    private static CultureInfo GetSafeCulture(string locale)
    {
        try
        {
            return CultureInfo.GetCultureInfo(locale);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.GetCultureInfo("en-US");
        }
    }
}
