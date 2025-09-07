using System.Data;
using Core.Interfaces;
using Core.Models;
using Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Reservation.Data;
using Reservation.Exceptions;
using Reservation.Interfaces;
using Reservation.Repositories;
using Reservation.Services;
using Reservation.Services.Domain;
using StackExchange.Redis;

namespace Reservation;

public abstract class Program
{
    public static async Task Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();

        // Initialize database and seed data
        await InitializeDatabaseAsync(host.Services);

        await host.RunAsync();
    }

    private static async Task InitializeDatabaseAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        
        try
        {
            logger.LogInformation("Initializing database and seeding data...");
            
            var context = scope.ServiceProvider.GetRequiredService<ReservationContext>();
            
            // Ensure database is created
            await context.Database.EnsureCreatedAsync();
            logger.LogInformation("Database ensured created");
            
            // Run migrations if any
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
            if (pendingMigrations.Any())
            {
                logger.LogInformation("Applying {Count} pending migrations", pendingMigrations.Count());
                await context.Database.MigrateAsync();
                logger.LogInformation("Migrations applied successfully");
            }
            
            // Seed data
            await SeedData.SeedAsync(context);
            logger.LogInformation("Database seeding completed successfully");
        }
        catch (Exception ex)
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "An error occurred while initializing the database");
            throw;
        }
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
                    
                    // Database configuration
                    services.AddTransient<IDbConnection>(sp => new NpgsqlConnection(connectionString));
                    services.AddTransient<Func<IDbConnection>>(sp =>
                        () => new NpgsqlConnection(connectionString)
                    );
                    services.AddDbContext<ReservationContext>(options =>
                        options.UseNpgsql(connectionString)
                    );

                    // Repository registrations
                    services.AddScoped<IReservationNew, ReservationNewRepository>();
                    services.AddScoped<IReservationQuery, ReservationQueryRepository>();
                    services.AddScoped<IReservationAnalytics, ReservationAnalyticsRepository>();
                    services.AddScoped<IReservationReport, ReservationReportRepository>();
                    services.AddScoped<IStatus, StatusRepository>();
                    services.AddScoped<IDetail, DetailRepository>();

                    // Domain services
                    services.AddScoped<IReservationDomainService, ReservationDomainService>();
                    services.AddScoped<IReservationValidationService, ReservationValidationService>();
                    services.AddScoped<IReservationPaymentService, ReservationPaymentService>();

                    // Application services
                    services.AddHostedService<ReservationServer>();
                    services.AddScoped<ReservationServiceImpl>();

                    // Redis configuration
                    var redisConfiguration = ConfigurationOptions.Parse(
                        configuration.GetConnectionString("Redis") ?? "localhost:6379",
                        true
                    );
                    redisConfiguration.AbortOnConnectFail = false;
                    services.AddSingleton<IConnectionMultiplexer>(
                        ConnectionMultiplexer.Connect(redisConfiguration)
                    );
                    services.AddScoped<ICacheService, CacheService>();

                    // External service clients
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
                }
            );

    /// <summary>
    /// Payment service implementation for handling reservation payments
    /// </summary>
    public class ReservationPaymentService : IReservationPaymentService
    {
        private readonly PaymentService.PaymentService.PaymentServiceClient _paymentClient;
        private readonly ILogger<ReservationPaymentService> _logger;

        public ReservationPaymentService(
            PaymentService.PaymentService.PaymentServiceClient paymentClient,
            ILogger<ReservationPaymentService> logger)
        {
            _paymentClient = paymentClient;
            _logger = logger;
        }

        public async Task<PaymentResult> ProcessPaymentAsync(CreateReservationDto dto)
        {
            try
            {
                var paymentRequest = new PaymentService.ProcessPaymentRequest
                {
                    UserId = dto.UserId,
                    OrganizationId = dto.OrganizationId,
                    Amount = dto.Detail.OriginalPrice - dto.Detail.Discount,
                    Currency = dto.Detail.Currency ?? "EUR",
                    Description = $"Reservation payment for {dto.Detail.Name}"
                };

                var response = await _paymentClient.ProcessPaymentAsync(paymentRequest);
                
                return new PaymentResult
                {
                    IsSuccess = response.Success,
                    TransactionId = response.TransactionId,
                    Amount = response.Amount,
                    Currency = response.Currency,
                    ErrorMessage = response.ErrorMessage
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Payment processing failed for user {UserId}", dto.UserId);
                return new PaymentResult
                {
                    IsSuccess = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<PaymentResult> ProcessRefundAsync(Guid reservationId, decimal amount, string reason)
        {
            try
            {
                var refundRequest = new PaymentService.ProcessRefundRequest
                {
                    ReservationId = reservationId.ToString(),
                    Amount = (double)amount,
                    Reason = reason
                };

                var response = await _paymentClient.ProcessRefundAsync(refundRequest);
                
                return new PaymentResult
                {
                    IsSuccess = response.Success,
                    TransactionId = response.TransactionId,
                    Amount = (decimal)response.Amount,
                    ErrorMessage = response.ErrorMessage
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Refund processing failed for reservation {ReservationId}", reservationId);
                return new PaymentResult
                {
                    IsSuccess = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<PaymentResult> ProcessPartialRefundAsync(Guid reservationId, decimal amount, string reason)
        {
            // Similar implementation to ProcessRefundAsync but for partial amounts
            return await ProcessRefundAsync(reservationId, amount, $"Partial refund: {reason}");
        }

        public async Task ValidatePaymentDataAsync(CreateReservationDto dto)
        {
            if (dto.Detail == null)
                throw new InvalidReservationDataException("Payment detail is required");

            if (dto.Detail.OriginalPrice <= 0)
                throw new InvalidReservationDataException("Original price must be greater than 0");

            if (dto.Detail.Discount < 0 || dto.Detail.Discount > dto.Detail.OriginalPrice)
                throw new InvalidReservationDataException("Invalid discount amount");

            if (string.IsNullOrWhiteSpace(dto.Detail.Currency))
                throw new InvalidReservationDataException("Currency is required");

            await Task.CompletedTask;
        }
    }
}