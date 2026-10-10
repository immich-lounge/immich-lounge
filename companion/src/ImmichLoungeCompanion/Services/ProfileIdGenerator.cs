using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ImmichLoungeCompanion.Services;

public static class ProfileIdGenerator
{
    public static string FromName(string name)
    {
        var letters = new StringBuilder();
        foreach (var character in name.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                letters.Append(char.ToLowerInvariant(character));
            }
        }

        var id = Regex.Replace(letters.ToString(), "[^a-z0-9]+", "-").Trim('-');
        if (id.Length == 0)
        {
            return "profile";
        }
        if (id.Length < 3)
        {
            id = "profile-" + id;
        }
        return id[..Math.Min(id.Length, 64)].TrimEnd('-');
    }
}
