using Reservation.Tests;

namespace Reservation.Tests;

public class TestGrpcEndpoints
{
    public static async Task Main(string[] args)
    {
        var client = new GrpcTestClient();
        
        Console.WriteLine("🧪 Testing gRPC Endpoints for Reservation Service");
        Console.WriteLine("=" * 60);
        Console.WriteLine();

        try
        {
            await RunAllTestsAsync(client);
            Console.WriteLine();
            Console.WriteLine("🎉 All tests passed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Test failed: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
        finally
        {
            client.Dispose();
        }
    }

    private static async Task RunAllTestsAsync(GrpcTestClient client)
    {
        // Test 1: Create Reservation
        await TestCreateReservationAsync(client);

        // Test 2: Create Reservation for Organization
        await TestCreateReservationForOrganizationAsync(client);

        // Test 3: Get Reservations
        await TestGetReservationsAsync(client);

        // Test 4: Get Reservation by ID
        await TestGetReservationByIdAsync(client);

        // Test 5: Get Reservation by Code
        await TestGetReservationByCodeAsync(client);

        // Test 6: Get Reservations by Date Range
        await TestGetReservationsByDateRangeAsync(client);

        // Test 7: Get Reservations by Resources
        await TestGetReservationsByResourcesAsync(client);

        // Test 8: Status Management
        await TestStatusManagementAsync(client);

        // Test 9: Get Statistics
        await TestGetStatisticsAsync(client);

        // Test 10: Get Reservations Count Per Day
        await TestGetReservationsCountPerDayAsync(client);

        // Test 11: Search Clients
        await TestSearchClientsAsync(client);

        // Test 12: Get Reservations by Source Count
        await TestGetReservationsBySourceCountAsync(client);

        // Test 13: Generate Report
        await TestGenerateReportAsync(client);
    }

    private static async Task TestCreateReservationAsync(GrpcTestClient client)
    {
        Console.WriteLine("1. 🏨 Testing CreateReservation...");
        
        var createdReservation = await client.CreateTestReservationAsync();
        
        Console.WriteLine($"   ✅ Created reservation: {createdReservation.Code}");
        Console.WriteLine($"   📋 ID: {createdReservation.Id}");
        Console.WriteLine($"   💰 Total Amount: {createdReservation.TotalAmount:C}");
        Console.WriteLine($"   📅 Start Date: {createdReservation.StartDate}");
        Console.WriteLine($"   📅 End Date: {createdReservation.EndDate}");
        Console.WriteLine($"   👤 Customer: {createdReservation.Detail.Name}");
        Console.WriteLine($"   📧 Email: {createdReservation.Detail.Email}");
        Console.WriteLine();
    }

    private static async Task TestCreateReservationForOrganizationAsync(GrpcTestClient client)
    {
        Console.WriteLine("2. 🏢 Testing CreateReservationForOrganization...");
        
        var createdReservation = await client.CreateReservationForOrganizationAsync();
        
        Console.WriteLine($"   ✅ Created organization reservation: {createdReservation.Code}");
        Console.WriteLine($"   📋 ID: {createdReservation.Id}");
        Console.WriteLine($"   💰 Total Amount: {createdReservation.TotalAmount:C}");
        Console.WriteLine($"   🏢 Organization ID: {createdReservation.OrganizationId}");
        Console.WriteLine();
    }

    private static async Task TestGetReservationsAsync(GrpcTestClient client)
    {
        Console.WriteLine("3. 📋 Testing GetReservations...");
        
        var reservations = await client.GetReservationsAsync(page: 1, perPage: 5);
        
        Console.WriteLine($"   ✅ Retrieved {reservations.Reservations.Count} reservations");
        
        foreach (var reservation in reservations.Reservations.Take(3))
        {
            Console.WriteLine($"   📋 {reservation.Code} - {reservation.Detail.Name} - {reservation.TotalAmount:C}");
        }
        Console.WriteLine();
    }

    private static async Task TestGetReservationByIdAsync(GrpcTestClient client)
    {
        Console.WriteLine("4. 🔍 Testing GetReservationById...");
        
        // First get a reservation to test with
        var reservations = await client.GetReservationsAsync(page: 1, perPage: 1);
        if (reservations.Reservations.Count > 0)
        {
            var reservationId = reservations.Reservations[0].Id;
            var retrievedReservation = await client.GetReservationByIdAsync(reservationId);
            
            Console.WriteLine($"   ✅ Retrieved reservation by ID: {retrievedReservation.Code}");
            Console.WriteLine($"   📋 Status: {retrievedReservation.StatusId}");
            Console.WriteLine($"   💰 Amount: {retrievedReservation.TotalAmount:C}");
        }
        else
        {
            Console.WriteLine("   ⚠️ No reservations found to test with");
        }
        Console.WriteLine();
    }

    private static async Task TestGetReservationByCodeAsync(GrpcTestClient client)
    {
        Console.WriteLine("5. 🎫 Testing GetReservationByCode (ValidateTicket)...");
        
        // First get a reservation to test with
        var reservations = await client.GetReservationsAsync(page: 1, perPage: 1);
        if (reservations.Reservations.Count > 0)
        {
            var reservationCode = reservations.Reservations[0].Code;
            var retrievedReservation = await client.GetReservationByCodeAsync(reservationCode);
            
            Console.WriteLine($"   ✅ Retrieved reservation by code: {retrievedReservation.Code}");
            Console.WriteLine($"   📋 ID: {retrievedReservation.Id}");
            Console.WriteLine($"   👤 Customer: {retrievedReservation.Detail.Name}");
        }
        else
        {
            Console.WriteLine("   ⚠️ No reservations found to test with");
        }
        Console.WriteLine();
    }

    private static async Task TestGetReservationsByDateRangeAsync(GrpcTestClient client)
    {
        Console.WriteLine("6. 📅 Testing GetReservationsByDateRange...");
        
        var startDate = DateTime.Now.AddDays(-30).ToString("yyyy-MM-dd");
        var endDate = DateTime.Now.AddDays(30).ToString("yyyy-MM-dd");
        
        var reservations = await client.GetReservationsByDateRangeAsync(startDate, endDate);
        
        Console.WriteLine($"   ✅ Retrieved {reservations.Reservations.Count} reservations for date range");
        Console.WriteLine($"   📅 From: {startDate} To: {endDate}");
        Console.WriteLine();
    }

    private static async Task TestGetReservationsByResourcesAsync(GrpcTestClient client)
    {
        Console.WriteLine("7. 🏠 Testing GetReservationsByResources...");
        
        var resourceIds = new[] { "rrrrrrrr-rrrr-rrrr-rrrr-rrrrrrrrrrrr", "ssssssss-ssss-ssss-ssss-ssssssssssss" };
        var startDate = DateTime.Now.AddDays(-30).ToString("yyyy-MM-dd");
        var endDate = DateTime.Now.AddDays(30).ToString("yyyy-MM-dd");
        
        var reservations = await client.GetReservationsByResourcesAsync(resourceIds, startDate, endDate);
        
        Console.WriteLine($"   ✅ Retrieved {reservations.Reservations.Count} reservations for resources");
        Console.WriteLine($"   🏠 Resource IDs: {string.Join(", ", resourceIds)}");
        Console.WriteLine();
    }

    private static async Task TestStatusManagementAsync(GrpcTestClient client)
    {
        Console.WriteLine("8. 📊 Testing Status Management...");
        
        // Get existing statuses
        var statuses = await client.GetStatusesAsync();
        Console.WriteLine($"   ✅ Retrieved {statuses.Statuses.Count} existing statuses");
        
        foreach (var status in statuses.Statuses.Take(3))
        {
            Console.WriteLine($"   📊 {status.Name}: {status.Description}");
        }
        Console.WriteLine();
    }

    private static async Task TestGetStatisticsAsync(GrpcTestClient client)
    {
        Console.WriteLine("9. 📈 Testing GetStatistics...");
        
        var organizationId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
        var startDate = DateTime.Now.AddDays(-30).ToString("yyyy-MM-dd");
        var endDate = DateTime.Now.AddDays(30).ToString("yyyy-MM-dd");
        
        var stats = await client.GetStatsAsync(organizationId, startDate, endDate);
        
        Console.WriteLine($"   ✅ Retrieved statistics for organization: {organizationId}");
        Console.WriteLine($"   📊 Total Reservations: {stats.TotalReservations.Value}");
        Console.WriteLine($"   💰 Total Earnings: {stats.TotalEarnings.Value:C}");
        Console.WriteLine($"   📈 Average Daily Earnings: {stats.AverageDailyEarnings.Value:C}");
        Console.WriteLine();
    }

    private static async Task TestGetReservationsCountPerDayAsync(GrpcTestClient client)
    {
        Console.WriteLine("10. 📅 Testing GetReservationsCountPerDay...");
        
        var organizationId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
        var startDate = DateTime.Now.AddDays(-7).ToString("yyyy-MM-dd");
        var endDate = DateTime.Now.AddDays(7).ToString("yyyy-MM-dd");
        
        var dailyCounts = await client.GetReservationsCountPerDayAsync(organizationId, startDate, endDate);
        
        Console.WriteLine($"   ✅ Retrieved daily reservation counts");
        Console.WriteLine($"   📅 Date range: {startDate} to {endDate}");
        
        foreach (var dailyCount in dailyCounts.DateReservationCounts.Take(5))
        {
            Console.WriteLine($"   📅 {dailyCount.Date}: {dailyCount.ReservationCount} reservations");
        }
        Console.WriteLine();
    }

    private static async Task TestSearchClientsAsync(GrpcTestClient client)
    {
        Console.WriteLine("11. 🔍 Testing SearchClients...");
        
        var organizationId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
        var searchQuery = "John";
        
        var clients = await client.SearchClientsAsync(searchQuery, organizationId, maxResults: 5);
        
        Console.WriteLine($"   ✅ Searched for clients with query: '{searchQuery}'");
        Console.WriteLine($"   👥 Found {clients.Clients.Count} client suggestions");
        
        foreach (var clientSuggestion in clients.Clients.Take(3))
        {
            Console.WriteLine($"   👤 {clientSuggestion.Name} - {clientSuggestion.Email} - {clientSuggestion.Phone}");
        }
        Console.WriteLine();
    }

    private static async Task TestGetReservationsBySourceCountAsync(GrpcTestClient client)
    {
        Console.WriteLine("12. 📊 Testing GetReservationsBySourceCount...");
        
        var organizationId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
        var startDate = DateTime.Now.AddDays(-30).ToString("yyyy-MM-dd");
        var endDate = DateTime.Now.AddDays(30).ToString("yyyy-MM-dd");
        
        var sourceCounts = await client.GetReservationsBySourceCountAsync(organizationId, startDate, endDate);
        
        Console.WriteLine($"   ✅ Retrieved reservations by source count");
        Console.WriteLine($"   🖥️ Client Reservations: {sourceCounts.TotalReservationsClient}");
        Console.WriteLine($"   🏢 Business Reservations: {sourceCounts.TotalReservationsBusiness}");
        Console.WriteLine();
    }

    private static async Task TestGenerateReportAsync(GrpcTestClient client)
    {
        Console.WriteLine("13. 📄 Testing GenerateReservationReport...");
        
        var organizationId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
        var startDate = DateTime.Now.AddDays(-30).ToString("yyyy-MM-dd");
        var endDate = DateTime.Now.AddDays(30).ToString("yyyy-MM-dd");
        
        var report = await client.GenerateReservationReportAsync(organizationId, startDate, endDate);
        
        Console.WriteLine($"   ✅ Generated reservation report");
        Console.WriteLine($"   📄 File Name: {report.FileName}");
        Console.WriteLine($"   📊 File Size: {report.FileContent.Length} bytes");
        Console.WriteLine();
    }
}
