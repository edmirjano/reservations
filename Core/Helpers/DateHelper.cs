using System.Globalization;

namespace Core.Helpers;

/// <summary>
/// Provides date parsing utilities for the backend.
/// Frontend handles all formatting and timezone conversion.
/// Backend works exclusively with UTC timestamps in ISO 8601 format.
/// </summary>
public static class DateHelper
{
    /// <summary>
    /// The culture used for date parsing
    /// </summary>
    public static readonly CultureInfo DateCulture = CultureInfo.InvariantCulture;

    /// <summary>
    /// Parses an ISO 8601 date string from the frontend.
    /// </summary>
    /// <param name="dateString">The ISO 8601 date string to parse</param>
    /// <returns>A UTC DateTime object</returns>
    /// <exception cref="FormatException">Thrown when the date string cannot be parsed</exception>
    public static DateTime ParseDate(string dateString)
    {
        if (string.IsNullOrEmpty(dateString))
        {
            throw new ArgumentNullException(
                nameof(dateString),
                "Date string cannot be null or empty"
            );
        }

        // Parse ISO 8601 format
        if (DateTime.TryParse(dateString, DateCulture, DateTimeStyles.RoundtripKind, out DateTime result))
        {
            return result.ToUniversalTime();
        }

        throw new FormatException(
            $"Date string '{dateString}' was not recognized as a valid ISO 8601 DateTime."
        );
    }


    /// <summary>
    /// Safely tries to parse a date from a string, returning the default value if parsing fails
    /// </summary>
    /// <param name="dateString">The date string to parse</param>
    /// <param name="defaultValue">The default value to return if parsing fails</param>
    /// <returns>The parsed date or the default value</returns>
    public static DateTime TryParseDate(string dateString, DateTime defaultValue)
    {
        if (string.IsNullOrEmpty(dateString))
        {
            return defaultValue;
        }

        try
        {
            return ParseDate(dateString);
        }
        catch
        {
            return defaultValue;
        }
    }
}
