namespace Core.Constant;

public class RedisConstant
{
    // Cache Time-To-Live values
    public const int CacheTimeToLiveDefaultValue = 1440;
    public const int CacheTimeToLiveHalfDay = 720;
    public const int CacheTimeToLiveOneHour = 60;

    // Microservice
    public const string Analytic = "Analytic";
    public const string ApiGateway = "ApiGateway";
    public const string Auth = "Auth";
    public const string Config = "Config";
    public const string Email = "Email";
    public const string Geolocation = "Geolocation";
    public const string Log = "Log";
    public const string Media = "Media";
    public const string Notification = "Notification";
    public const string Organization = "Organization";
    public const string Payment = "Payment";
    public const string Reservation = "Reservation";
    public const string Resource = "Resource";
    public const string Review = "Review";
    public const string Sms = "Sms";
}
