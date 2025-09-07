using System.Data;
using Core.Interfaces;
using Core.Models;
using Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Reservation.Data;
using Reservation.Interfaces;
using Reservation.Repositories;
using Reservation.Services;
using StackExchange.Redis;

namespace Reservation;

public abstract class Program
{
    public static void Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();
        host.Run();
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration(
                (hostingContext, config) =>
                {
                    var env = hostingContext.HostingEnvironment;

                    config
                        .SetBasePath(Directory.GetCurrentDirectory())
                        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                        .AddJsonFile(
                            $"appsettings.{env.EnvironmentName}.json",
                            optional: true,
                            reloadOnChange: true
                        )
                        .AddEnvironmentVariables();
                }
            )
            .ConfigureServices(
                (hostContext, services) =>
                {
                    var configuration = hostContext.Configuration;
                    var connectionString =
                        ConnectionStringReceiver.GetConnectionStringFromKubernetes(configuration);
                    services.AddTransient<IDbConnection>(sp => new NpgsqlConnection(
                        connectionString
                    ));
                    // Register the connection factory function
                    services.AddTransient<Func<IDbConnection>>(sp =>
                        () => new NpgsqlConnection(connectionString)
                    );
                    services.AddDbContext<ReservationContext>(options =>
                        options.UseNpgsql(connectionString)
                    );

                    services.AddScoped<IReservation, ReservationRepository>();
                    services.AddScoped<IStatus, StatusRepository>();
                    services.AddScoped<IDetail, DetailRepository>();
                    services.AddHostedService<ReservationServer>();
                    services.AddScoped<ReservationServiceImpl>();

                    // Add Redis configuration
                    var redisConfiguration = ConfigurationOptions.Parse(
                        configuration.GetConnectionString("Redis") ?? "localhost:6379",
                        true
                    );
                    redisConfiguration.AbortOnConnectFail = false;
                    services.AddSingleton<IConnectionMultiplexer>(
                        ConnectionMultiplexer.Connect(redisConfiguration)
                    );
                    services.AddScoped<ICacheService, CacheService>();

                    services.AddGrpcClient<AuthService.AuthService.AuthServiceClient>(options =>
                    {
                        var authUri =
                            configuration.GetValue<string>("Services:Auth")
                            ?? "http://localhost:5001";
                        options.Address = new Uri(authUri);
                    });

                    services.AddGrpcClient<PaymentService.PaymentService.PaymentServiceClient>(
                        options =>
                        {
                            var paymentUri =
                                configuration.GetValue<string>("Services:Payment")
                                ?? "http://localhost:5010";
                            options.Address = new Uri(paymentUri);
                        }
                    );

                    services.AddGrpcClient<ResourceService.ResourceService.ResourceServiceClient>(
                        options =>
                        {
                            var resourceUri =
                                configuration.GetValue<string>("Services:Resource")
                                ?? "http://localhost:5002";
                            options.Address = new Uri(resourceUri);
                        }
                    );

                    services.AddGrpcClient<EmailService.EmailService.EmailServiceClient>(options =>
                    {
                        string emailUri =
                            configuration.GetValue<string>("Services:Email")
                            ?? "http://localhost:5006";
                        options.Address = new Uri(emailUri);
                    });

                    services.AddGrpcClient<OrganizationService.OrganizationService.OrganizationServiceClient>(
                        options =>
                        {
                            var organizationUri =
                                configuration.GetValue<string>("Services:Organization")
                                ?? "http://localhost:5004";
                            options.Address = new Uri(organizationUri);
                        }
                    );

                    services.AddGrpcClient<PaymentService.PaymentService.PaymentServiceClient>(
                        options =>
                        {
                            string paymentUri =
                                configuration.GetValue<string>("Services:Payment")
                                ?? "http://localhost:5010";
                            options.Address = new Uri(paymentUri);
                        }
                    );
                }
            );
}
