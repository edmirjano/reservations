using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Reservation.Services;

public class ReservationServer(
    IConfiguration configuration,
    ReservationServiceImpl reservationServiceImpl,
    ILogger<ReservationServer> logger
) : IHostedService
{
    private Server _server;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var serverAddress = configuration["Urls:Server"];
        var serverPort = int.Parse(configuration["Urls:Port"]);

        logger.LogInformation(
            "Starting gRPC server on {Address}:{Port}",
            serverAddress,
            serverPort
        );

        try
        {
            _server = new Server
            {
                Services =
                {
                    ReservationService.ReservationService.BindService(reservationServiceImpl),
                },
                Ports = { new ServerPort(serverAddress, serverPort, ServerCredentials.Insecure) },
            };
            _server.Start();
            logger.LogInformation(
                "gRPC server started successfully on {Address}:{Port}",
                serverAddress,
                serverPort
            );
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to start gRPC server on {Address}:{Port}",
                serverAddress,
                serverPort
            );
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_server != null)
        {
            logger.LogInformation("Shutting down gRPC server");
            try
            {
                await _server.ShutdownAsync();
                logger.LogInformation("gRPC server shutdown completed");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error shutting down gRPC server");
            }
        }
    }
}
