using Microsoft.Extensions.Configuration;

namespace Core.Models;

public static class ConnectionStringReceiver
{
    public static string GetConnectionStringFromKubernetes(IConfiguration configuration)
    {
        var host =
            Environment.GetEnvironmentVariable("DB_HOST")
            ?? configuration.GetConnectionString("DB_HOST");
        var port =
            Environment.GetEnvironmentVariable("DB_PORT")
            ?? configuration.GetConnectionString("DB_PORT");
        var database =
            Environment.GetEnvironmentVariable("DB_NAME")
            ?? configuration.GetConnectionString("DB_NAME");
        var username =
            Environment.GetEnvironmentVariable("DB_USER")
            ?? configuration.GetConnectionString("DB_USER");
        var password =
            Environment.GetEnvironmentVariable("DB_PASSWORD")
            ?? configuration.GetConnectionString("DB_PASSWORD");

        return $"Host={host};Port={port};Database={database};Username={username};Password={password}";
    }
}
