namespace Reservation.Tests;

public static class TestRunner
{
    public static async Task RunAllTestsAsync()
    {
        Console.WriteLine("🧪 Starting gRPC Endpoint Tests\n");
        
        using var client = new GrpcTestClient();
        
        try
        {
            // Test 1: Create Reservation
            Console.WriteLine("1️⃣ Testing CreateReservation...");
            var created = await client.CreateTestReservationAsync();
            Console.WriteLine($"   ✅ Created reservation: {created.Code}");
            Console.WriteLine($"   📋 ID: {created.Id}");
            Console.WriteLine($"   💰 Amount: {created.TotalAmount} EUR\n");

            // Test 2: Get Reservation by ID
            Console.WriteLine("2️⃣ Testing GetReservationById...");
            var retrieved = await client.GetReservationByIdAsync(created.Id);
            Console.WriteLine($"   ✅ Retrieved: {retrieved.Code}");
            Console.WriteLine($"   📊 Status: {retrieved.StatusId}\n");

            // Test 3: Get All Reservations
            Console.WriteLine("3️⃣ Testing GetReservations...");
            var all = await client.GetAllReservationsAsync();
            Console.WriteLine($"   ✅ Found {all.Reservations.Count} reservations\n");

            // Test 4: Search Clients
            Console.WriteLine("4️⃣ Testing SearchClients...");
            var search = await client.SearchClientsAsync("Test", created.OrganizationId);
            Console.WriteLine($"   ✅ Search returned {search.Clients.Count} clients\n");

            Console.WriteLine("🎉 All tests passed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Test failed: {ex.Message}");
            Console.WriteLine($"   Stack: {ex.StackTrace}");
        }
    }
}