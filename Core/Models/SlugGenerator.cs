using System.Text.RegularExpressions;

namespace Core.Models;

public static class SlugGenerator
{
    public static string GenerateSlug(string input)
    {
        // Remove emojis
        string noEmojis = Regex.Replace(input, @"\p{Cs}", "");

        // Normalize and replace special characters
        string normalized = noEmojis
            .Trim()
            .ToLower()
            .Replace("ë", "e")
            .Replace("ç", "c")
            .Replace("á", "a")
            .Replace("é", "e")
            .Replace("í", "i")
            .Replace("ó", "o")
            .Replace("ú", "u")
            .Replace("ñ", "n")
            .Replace("ä", "a")
            .Replace("ö", "o")
            .Replace("ü", "u")
            .Replace("ß", "ss")
            .Replace(" ", "-");

        // Remove any non-alphanumeric characters except for hyphens
        string sanitized = Regex.Replace(normalized, @"[^a-z0-9-]", "");

        // Collapse multiple hyphens into a single one
        string slug = Regex.Replace(sanitized, @"-+", "-");

        // Remove leading hyphen if present
        if (slug.StartsWith("-"))
        {
            slug = slug.Substring(1);
        }

        return slug;
    }
}
