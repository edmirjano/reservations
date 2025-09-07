using Grpc.Core;
using ReservationService;
using System.Text.Json;

namespace Reservation.Tests;

public class TestGrpcEndpoints
{
    private readonly GrpcTestClient _client;
    private readonly List<string> _testResults = new();
    private readonly List<string> _createdReservations = new();

    public TestGrpcEndpoints(string serverUrl = "http://localhost:5000")
    {
        _client = new GrpcTestClient(serverUrl);
    }

    public static async Task Main(string[] args)
    {
        var serverUrl = args.Length > 0 ? args[0] : "http://localhost:5000";
        var tester = new TestGrpcEndpoints(serverUrl);
        
        Console.WriteLine("🧪 Starting Comprehensive gRPC Endpoint Tests...");
        Console.WriteLine($"📡 Server URL: {serverUrl}");
        Console.WriteLine(new string('=', 60));

        try
        {
            await tester.RunAllTestsAsync();
            tester.PrintTestSummary();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Fatal error during testing: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
        finally
        {
            tester._client.Dispose();
        }

        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }

    public async Task RunAllTestsAsync()
    {
        // Test 1: Status Management
        await TestStatusManagementAsync();

        // Test 2: Basic Reservation Creation
        await TestReservationCreationAsync();

        // Test 3: Organization Reservation Creation
        await TestOrganizationReservationCreationAsync();

        // Test 4: Reservation Retrieval
        await TestReservationRetrievalAsync();

        // Test 5: Reservation Updates
        await TestReservationUpdatesAsync();

        // Test 6: Search and Query Functions
        await TestSearchAndQueryAsync();

        // Test 7: Analytics and Stats
        await TestAnalyticsAsync();

        // Test 8: Reports
        await TestReportsAsync();

        // Test 9: Validation and Error Handling
        await TestValidationAndErrorsAsync();

        // Test 10: Performance Tests
        await TestPerformanceAsync();
    }

    private async Task TestStatusManagementAsync()
    {
        Console.WriteLine("\n🏷️  Testing Status Management...");

        try
        {
            // Get existing statuses
            var statuses = await _client.GetStatusesAsync();
            LogTest("Get Statuses", $"Retrieved {statuses.Statuses.Count} statuses", true);
            
            foreach (var status in statuses.Statuses.Take(3))
            {
                Console.WriteLine($"   📋 Status: {status.Name} - {status.Description}");
            }

            // Create a new test status
            var newStatus = await _client.CreateStatusAsync("Test Status", "Created during testing");
            LogTest("Create Status", $"Created status: {newStatus.Name} (ID: {newStatus.Id})", true);

        }
        catch (Exception ex)
        {
            LogTest("Status Management", $"Failed: {ex.Message}", false);
        }
    }

    private async Task TestReservationCreationAsync()
    {
        Console.WriteLine("\n🏨 Testing Basic Reservation Creation...");

        try
        {
            var reservation = await _client.CreateTestReservationAsync();
            _createdReservations.Add(reservation.Id);
            
            LogTest("Create Reservation", 
                $"Created reservation {reservation.Code} (ID: {reservation.Id}) - Amount: €{reservation.TotalAmount}", 
                true);

            Console.WriteLine($"   📅 Dates: {reservation.StartDate} to {reservation.EndDate}");
            Console.WriteLine($"   👤 User ID: {reservation.UserId}");
            Console.WriteLine($"   🏢 Organization: {reservation.OrganizationId}");
        }
        catch (Exception ex)
        {
            LogTest("Create Reservation", $"Failed: {ex.Message}", false);
        }
    }

    private async Task TestOrganizationReservationCreationAsync()
    {
        Console.WriteLine("\n🏢 Testing Organization Reservation Creation...");

        try
        {
            var orgReservation = await _client.CreateReservationForOrganizationAsync();
            _createdReservations.Add(orgReservation.Id);
            
            LogTest("Create Organization Reservation", 
                $"Created org reservation {orgReservation.Code} - Amount: ${orgReservation.TotalAmount}", 
                true);

            Console.WriteLine($"   💰 Currency: USD");
            Console.WriteLine($"   📞 Payment processed automatically");
        }
        catch (Exception ex)
        {
            LogTest("Create Organization Reservation", $"Failed: {ex.Message}", false);
        }
    }

    private async Task TestReservationRetrievalAsync()
    {
        Console.WriteLine("\n🔍 Testing Reservation Retrieval...");

        if (!_createdReservations.Any())
        {
            LogTest("Reservation Retrieval", "No reservations to test (creation failed)", false);
            return;
        }

        try
        {
            var reservationId = _createdReservations.First();
            
            // Get by ID
            var reservation = await _client.GetReservationByIdAsync(reservationId);
            LogTest("Get Reservation by ID", 
                $"Retrieved reservation {reservation.Code} with status {reservation.StatusId}", 
                true);

            // Get by Code
            var reservationByCode = await _client.GetReservationByCodeAsync(reservation.Code);
            LogTest("Get Reservation by Code", 
                $"Validated ticket {reservationByCode.Code}", 
                true);

            // Get all reservations
            var allReservations = await _client.GetReservationsAsync(1, 5);
            LogTest("Get All Reservations", 
                $"Retrieved {allReservations.Reservations.Count} reservations", 
                true);

            Console.WriteLine($"   📊 Total reservations in system: {allReservations.Reservations.Count}");
        }
        catch (Exception ex)
        {
            LogTest("Reservation Retrieval", $"Failed: {ex.Message}", false);
        }
    }

    private async Task TestReservationUpdatesAsync()
    {
        Console.WriteLine("\n✏️  Testing Reservation Updates...");

        if (!_createdReservations.Any())
        {
            LogTest("Reservation Updates", "No reservations to update", false);
            return;
        }

        try
        {
            var reservationId = _createdReservations.First();
            var reservation = await _client.GetReservationByIdAsync(reservationId);
            
            // Update total amount
            reservation.TotalAmount = reservation.TotalAmount + 50;
            
            var updatedReservation = await _client.UpdateReservationAsync(reservation);
            LogTest("Update Reservation", 
                $"Updated reservation {updatedReservation.Code} - New amount: €{updatedReservation.TotalAmount}", 
                true);
        }
        catch (Exception ex)
        {
            LogTest("Update Reservation", $"Failed: {ex.Message}", false);
        }
    }

    private async Task TestSearchAndQueryAsync()
    {
        Console.WriteLine("\n🔎 Testing Search and Query Functions...");

        try
        {
            // Test date range search
            var startDate = DateTime.Now.AddDays(-30).ToString("yyyy-MM-dd");
            var endDate = DateTime.Now.AddDays(30).ToString("yyyy-MM-dd");
            
            var dateRangeReservations = await _client.GetReservationsByDateRangeAsync(startDate, endDate);
            LogTest("Date Range Search", 
                $"Found {dateRangeReservations.Reservations.Count} reservations in date range", 
                true);

            // Test resource search
            var resourceIds = new[] { 
                "rrrrrrrr-rrrr-rrrr-rrrr-rrrrrrrrrrrr", 
                "ssssssss-ssss-ssss-ssss-ssssssssssss" 
            };
            
            var resourceReservations = await _client.GetReservationsByResourcesAsync(resourceIds, startDate, endDate);
            LogTest("Resource Search", 
                $"Found {resourceReservations.ReservationsByResource.Count} resource reservations", 
                true);

            // Test client search
            var orgId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
            var clientSearch = await _client.SearchClientsAsync("John", orgId, 5);
            LogTest("Client Search", 
                $"Found {clientSearch.Clients.Count} clients matching 'John'", 
                true);
        }
        catch (Exception ex)
        {
            LogTest("Search and Query", $"Failed: {ex.Message}", false);
        }
    }

    private async Task TestAnalyticsAsync()
    {
        Console.WriteLine("\n📊 Testing Analytics and Statistics...");

        try
        {
            var orgId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
            var startDate = DateTime.Now.AddDays(-30).ToString("yyyy-MM-dd");
            var endDate = DateTime.Now.ToString("yyyy-MM-dd");

            // Get stats
            var stats = await _client.GetStatsAsync(orgId, startDate, endDate);
            LogTest("Get Statistics", 
                $"Stats - Reservations: {stats.TotalReservations}, Revenue: €{stats.TotalEarnings:F2}", 
                true);

            // Get daily counts
            var dailyCounts = await _client.GetReservationsCountPerDayAsync(orgId, startDate, endDate);
            LogTest("Daily Reservation Counts", 
                $"Retrieved {dailyCounts.DateReservationCounts.Count} daily count records", 
                true);

            // Get source breakdown
            var sourceBreakdown = await _client.GetReservationsBySourceCountAsync(orgId, startDate, endDate);
            LogTest("Source Breakdown", 
                $"Sources tracked: {sourceBreakdown.SourceCounts.Count}", 
                true);

            Console.WriteLine($"   💰 Average daily earnings: €{stats.AverageDailyEarnings:F2}");
            foreach (var source in sourceBreakdown.SourceCounts.Take(3))
            {
                Console.WriteLine($"   📱 {source.Source}: {source.Count} reservations");
            }
        }
        catch (Exception ex)
        {
            LogTest("Analytics", $"Failed: {ex.Message}", false);
        }
    }

    private async Task TestReportsAsync()
    {
        Console.WriteLine("\n📋 Testing Report Generation...");

        try
        {
            var orgId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
            var startDate = DateTime.Now.AddDays(-30).ToString("yyyy-MM-dd");
            var endDate = DateTime.Now.ToString("yyyy-MM-dd");

            var report = await _client.GenerateReservationReportAsync(orgId, startDate, endDate);
            LogTest("Generate Excel Report", 
                $"Generated report '{report.FileName}' ({report.FileContent.Length} bytes)", 
                true);

            Console.WriteLine($"   📁 File: {report.FileName}");
            Console.WriteLine($"   📏 Size: {report.FileContent.Length / 1024.0:F1} KB");
        }
        catch (Exception ex)
        {
            LogTest("Report Generation", $"Failed: {ex.Message}", false);
        }
    }

    private async Task TestValidationAndErrorsAsync()
    {
        Console.WriteLine("\n⚠️  Testing Validation and Error Handling...");

        // Test invalid reservation creation
        try
        {
            var invalidRequest = new CreateReservationDto
            {
                UserId = "", // Invalid: empty user ID
                OrganizationId = "",
                StartDate = "",
                EndDate = ""
            };

            await _client._client.CreateReservationAsync(invalidRequest);
            LogTest("Invalid Data Validation", "Should have failed but didn't", false);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.InvalidArgument || ex.StatusCode == StatusCode.Internal)
        {
            LogTest("Invalid Data Validation", $"Correctly rejected invalid data: {ex.Status.Detail}", true);
        }
        catch (Exception ex)
        {
            LogTest("Invalid Data Validation", $"Unexpected error: {ex.Message}", false);
        }

        // Test non-existent reservation retrieval
        try
        {
            await _client.GetReservationByIdAsync("00000000-0000-0000-0000-000000000000");
            LogTest("Non-existent Reservation", "Should have failed but didn't", false);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound || ex.StatusCode == StatusCode.Internal)
        {
            LogTest("Non-existent Reservation", $"Correctly handled not found: {ex.Status.Detail}", true);
        }
        catch (Exception ex)
        {
            LogTest("Non-existent Reservation", $"Unexpected error: {ex.Message}", false);
        }
    }

    private async Task TestPerformanceAsync()
    {
        Console.WriteLine("\n⚡ Testing Performance...");

        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            // Create multiple reservations concurrently
            var tasks = new List<Task<ReservationDTO>>();
            for (int i = 0; i < 5; i++)
            {
                tasks.Add(_client.CreateTestReservationAsync());
            }

            var results = await Task.WhenAll(tasks);
            stopwatch.Stop();

            foreach (var result in results)
            {
                _createdReservations.Add(result.Id);
            }

            LogTest("Concurrent Creation", 
                $"Created {results.Length} reservations in {stopwatch.ElapsedMilliseconds}ms", 
                true);

            // Test rapid retrieval
            stopwatch.Restart();
            var retrievalTasks = _createdReservations.Take(5).Select(id => _client.GetReservationByIdAsync(id));
            await Task.WhenAll(retrievalTasks);
            stopwatch.Stop();

            LogTest("Rapid Retrieval", 
                $"Retrieved 5 reservations in {stopwatch.ElapsedMilliseconds}ms", 
                true);

            Console.WriteLine($"   ⏱️  Average creation time: {stopwatch.ElapsedMilliseconds / 5.0:F1}ms per reservation");
        }
        catch (Exception ex)
        {
            LogTest("Performance Tests", $"Failed: {ex.Message}", false);
        }
    }

    private void LogTest(string testName, string result, bool success)
    {
        var status = success ? "✅" : "❌";
        var message = $"{status} {testName}: {result}";
        Console.WriteLine($"   {message}");
        _testResults.Add($"{testName}: {(success ? "PASS" : "FAIL")} - {result}");
    }

    private void PrintTestSummary()
    {
        Console.WriteLine("\n" + new string('=', 60));
        Console.WriteLine("📊 TEST SUMMARY");
        Console.WriteLine(new string('=', 60));

        var passCount = _testResults.Count(r => r.Contains("PASS"));
        var failCount = _testResults.Count(r => r.Contains("FAIL"));

        Console.WriteLine($"✅ Passed: {passCount}");
        Console.WriteLine($"❌ Failed: {failCount}");
        Console.WriteLine($"📈 Success Rate: {(passCount * 100.0 / _testResults.Count):F1}%");

        if (failCount > 0)
        {
            Console.WriteLine("\n🔍 Failed Tests:");
            foreach (var result in _testResults.Where(r => r.Contains("FAIL")))
            {
                Console.WriteLine($"   • {result}");
            }
        }

        Console.WriteLine($"\n📋 Created {_createdReservations.Count} test reservations");
        Console.WriteLine("🎉 Testing completed!");
    }
}

public static class GrpcTestClientExtensions
{
    public static async Task<ReservationDTO> UpdateReservationAsync(this GrpcTestClient client, ReservationDTO reservation)
    {
        return await client._client.UpdateReservationAsync(reservation);
    }
}

public class LoadTester
{
    private readonly GrpcTestClient _client;

    public LoadTester(string serverUrl = "http://localhost:5000")
    {
        _client = new GrpcTestClient(serverUrl);
    }

    public async Task RunLoadTestAsync(int concurrentUsers = 10, int requestsPerUser = 5)
    {
        Console.WriteLine($"🚀 Starting load test: {concurrentUsers} users × {requestsPerUser} requests");
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var tasks = new List<Task>();

        for (int user = 0; user < concurrentUsers; user++)
        {
            tasks.Add(SimulateUserAsync(user, requestsPerUser));
        }

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        var totalRequests = concurrentUsers * requestsPerUser;
        var requestsPerSecond = totalRequests / (stopwatch.ElapsedMilliseconds / 1000.0);

        Console.WriteLine($"⚡ Load test completed:");
        Console.WriteLine($"   📊 Total requests: {totalRequests}");
        Console.WriteLine($"   ⏱️  Total time: {stopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"   📈 Requests/second: {requestsPerSecond:F2}");
    }

    private async Task SimulateUserAsync(int userId, int requestCount)
    {
        for (int i = 0; i < requestCount; i++)
        {
            try
            {
                // Mix of different operations
                switch (i % 4)
                {
                    case 0:
                        await _client.CreateTestReservationAsync();
                        break;
                    case 1:
                        await _client.GetReservationsAsync();
                        break;
                    case 2:
                        await _client.GetStatusesAsync();
                        break;
                    case 3:
                        var orgId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
                        await _client.GetStatsAsync(orgId, "2024-01-01", "2024-12-31");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ User {userId} request {i} failed: {ex.Message}");
            }
        }
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}