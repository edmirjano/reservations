using Reservation.Tests;

namespace Reservation.Tests;

public class Program
{
    public static async Task Main(string[] args)
    {
        await TestGrpcEndpoints.Main(args);
    }
}
