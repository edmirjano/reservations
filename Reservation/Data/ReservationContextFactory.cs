using Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Reservation.Data;

public class ReservationContextFactory : IDesignTimeDbContextFactory<ReservationContext>
{
    public ReservationContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<ReservationContext>();
        var connectionString = ConnectionStringReceiver.GetConnectionStringFromKubernetes(
            configuration
        );
        optionsBuilder.UseNpgsql(connectionString);

        return new ReservationContext(optionsBuilder.Options);
    }
}
