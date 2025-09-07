namespace Core.Constant.Enums;

/// <summary>
/// Enums Class
/// </summary>
public static class Enums
{
    /// <summary>
    /// Resource Status Color Enum
    /// </summary>
    public static class ResourceStatusColor
    {
        public static string Reserved = "#1D68FF";

        public static string Available = "#C8C8C8";

        public static string Unavaliable = "#393E44";

        public static string Busy = "#ff0000";

        public static string Scanned = "#E7AF21";
    }

    /// <summary>
    /// Reservation Status Color Enum
    /// </summary>
    public static class ReservationStatusColor
    {
        public static string Pending = "#E7AF21";

        public static string Created = "#50B39C";

        public static string NotCheckedIn = "#C8C8C8";

        public static string CheckedIn = "#1D68FF";

        public static string Finished = "#A3AED0";

        public static string Default = "#C8C8C8";
    }

    /// <summary>
    /// Reservation Status Names Enum
    /// </summary>
    public static class ReservationStatusNames
    {
        public static string Pending = "Pending";

        public static string Created = "Created";

        public static string NotCheckedIn = "NotCheckedIn";

        public static string CheckedIn = "CheckedIn";

        public static string Finished = "Finished";
    }


    /// <summary>
    /// Social Media Providers Enum
    /// </summary>
    public static class SocialMediaProviders
    {
        public const string Apple = "apple";
        public const string Google = "google";
        public const string Facebook = "facebook";
    }

    /// <summary>
    /// User Roles Enum
    /// </summary>
    public static class UserRoles
    {
        public const string Admin = "Admin";
        public const string User = "User";
        public const string Employee = "Employee";
        public const string Organization = "Organization";
        public const string Manager = "Manager";
    }

    public static class ResourceTypes
    {
        public const string Umbrella = "Umbrella";
        public const string Other = "Other";
    }
}