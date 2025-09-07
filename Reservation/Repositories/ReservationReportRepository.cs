using System.Data;
using Core.Repositories;
using Reservation.Data;
using Reservation.Interfaces;
using Reservation.Models;

namespace Reservation.Repositories;

public class ReservationReportRepository : GenericRepository<Models.Reservation, ReservationContext>, IReservationReport
{
    private readonly AuthService.AuthService.AuthServiceClient _authServiceClient;
    private readonly ResourceService.ResourceService.ResourceServiceClient _resourceServiceClient;
    private readonly OrganizationService.OrganizationService.OrganizationServiceClient _organizationClient;

    public ReservationReportRepository(
        ReservationContext context,
        Func<IDbConnection> dbConnectionFactory,
        AuthService.AuthService.AuthServiceClient authServiceClient,
        ResourceService.ResourceService.ResourceServiceClient resourceServiceClient,
        OrganizationService.OrganizationService.OrganizationServiceClient organizationServiceClient
    ) : base(context, dbConnectionFactory)
    {
        _authServiceClient = authServiceClient;
        _resourceServiceClient = resourceServiceClient;
        _organizationClient = organizationServiceClient;
    }

    public async Task<byte[]> GenerateReservationReportAsync(string organizationId, string startDate, string endDate)
    {
        var enrichedReservations = await GetEnrichedReservationDataAsync(organizationId, startDate, endDate);
        var analytics = await CalculateReservationAnalyticsAsync(enrichedReservations, startDate, endDate);
        return await GenerateEnrichedReservationExcelWithAnalyticsAsync(enrichedReservations, analytics, organizationId);
    }

    private async Task<byte[]> GenerateEnrichedReservationExcelWithAnalyticsAsync(
        List<EnrichedReservationData> reservations,
        ReservationAnalytics analytics,
        string organizationId)
    {
        using (var workbook = new ClosedXML.Excel.XLWorkbook())
        {
            ExcelReportHelper.CreateSummarySheet(workbook, analytics);
            ExcelReportHelper.CreateChartsSheet(workbook, analytics);
            ExcelReportHelper.CreateResourceDataSheet(workbook, reservations, analytics);
            ExcelReportHelper.CreateSunEasyDataSheet(workbook, reservations, analytics);
            await CreateUsersSheet(workbook, reservations, analytics, organizationId);
            ExcelReportHelper.CreateDataSheet(workbook, reservations);

            using (var stream = new System.IO.MemoryStream())
            {
                workbook.SaveAs(stream);
                return stream.ToArray();
            }
        }
    }

    private async Task<List<EnrichedReservationData>> GetEnrichedReservationDataAsync(
        string organizationId,
        string startDate,
        string endDate
    )
    {
        // First, get basic reservation data with joins
        var reservationData = await QueryAsync<ReservationQueryResult>(
            @"SELECT
                r.""Id"" as ReservationId,
                r.""Code"" as ReservationCode,
                r.""OrganizationId"",
                r.""UserId"",
                r.""StatusId"",
                r.""Source"",
                r.""TotalAmount"",
                r.""TotalAmount"" as NetAmount,
                r.""StartDate"",
                r.""EndDate"",
                s.""Name"" as StatusName,
                d.""Name"" as CustomerName,
                d.""Phone"" as CustomerPhone,
                d.""Email"" as CustomerEmail,
                d.""Note"" as Notes,
                d.""Currency""
            FROM ""Reservations"" r
            LEFT JOIN ""Statuses"" s ON r.""StatusId"" = s.""Id""
            LEFT JOIN ""Details"" d ON r.""Id"" = d.""ReservationId""
            WHERE r.""OrganizationId"" = @OrgId
                AND r.""StartDate"" >= @Start
                AND r.""EndDate"" <= @End
                AND r.""IsDeleted"" = false
                AND r.""IsActive"" = true",
            new
            {
                OrgId = Guid.Parse(organizationId),
                Start = DateTime.Parse(startDate),
                End = DateTime.Parse(endDate),
            }
        );

        var enrichedList = new List<EnrichedReservationData>();

        // Get organization info once
        var organizationRequest = new OrganizationService.GetByIdRequest { Id = organizationId };
        var organization = await _organizationClient.GetOrganizationByIdAsync(organizationRequest);
        string organizationName = organization?.Name ?? "Unknown Organization";

        foreach (var reservation in reservationData)
        {
            var enrichedReservation = new EnrichedReservationData
            {
                ReservationCode = reservation.ReservationCode ?? "",
                OrganizationName = organizationName,
                StatusName = reservation.StatusName ?? "",
                Source = reservation.Source ?? "",
                TotalAmount = (double)reservation.TotalAmount,
                NetAmount = (double)reservation.NetAmount,
                StartDate = reservation.StartDate,
                EndDate = reservation.EndDate,
                CustomerName = reservation.CustomerName ?? "",
                CustomerPhone = reservation.CustomerPhone ?? "",
                CustomerEmail = reservation.CustomerEmail ?? "",
                Notes = reservation.Notes ?? "",
                Currency = reservation.Currency ?? ""
            };

            // Get user details
            try
            {
                var userIdOnly = new AuthService.IdOnly { Id = reservation.UserId.ToString() };
                var userProfile = await authServiceClient.GetUserProfileByIdAsync(userIdOnly);
                enrichedReservation.Username = userProfile?.Username ?? "Unknown User";
                enrichedReservation.UserEmail = userProfile?.Email ?? "";
            }
            catch
            {
                enrichedReservation.Username = "Unknown User";
                enrichedReservation.UserEmail = "";
            }

            // Calculate net amount based on source
            if (reservation.Source == "Organization")
            {
                enrichedReservation.NetAmount = (double)reservation.TotalAmount;
            }
            else
            {
                // For SunEasy reservations, we need to calculate the organization's net value
                // This would be the total amount minus platform fees
                // For now, we'll use a simple calculation - you can adjust this based on your business logic
                enrichedReservation.NetAmount = (double)reservation.TotalAmount * 0.85; // Assuming 15% platform fee
            }

            // Get resource numbers
            try
            {
                var resourceNumbers = await GetResourceNumbersForReservation(reservation.ReservationId);
                enrichedReservation.ResourceNumbers = string.Join(", ", resourceNumbers);
            }
            catch
            {
                enrichedReservation.ResourceNumbers = "";
            }

            enrichedList.Add(enrichedReservation);
        }

        return enrichedList;
    }

    private async Task<ReservationAnalytics> CalculateReservationAnalyticsAsync(
        List<EnrichedReservationData> reservations,
        string startDate,
        string endDate
    )
    {
        var analytics = new ReservationAnalytics
        {
            StartDate = DateTime.Parse(startDate),
            EndDate = DateTime.Parse(endDate)
        };

        analytics.TotalDays = (analytics.EndDate - analytics.StartDate).Days + 1;

        if (!reservations.Any())
        {
            return analytics;
        }

        // Basic Statistics
        analytics.TotalReservations = reservations.Count;
        analytics.TotalRevenue = reservations.Sum(r => r.TotalAmount);
        analytics.NetRevenue = reservations.Sum(r => r.NetAmount);
        analytics.AverageReservationValue = analytics.TotalRevenue / analytics.TotalReservations;
        analytics.OrganizationReservations = reservations.Count(r => r.Source == "Organization");
        analytics.SunEasyReservations = reservations.Count(r => r.Source != "Organization");
        analytics.DailyAverageRevenue = analytics.TotalRevenue / analytics.TotalDays;

        // Determine primary currency (most common one)
        analytics.Currency = reservations
            .Where(r => !string.IsNullOrEmpty(r.Currency))
            .GroupBy(r => r.Currency)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault()?.Key ?? "EUR";

        // Status Analytics
        var statusGroups = reservations.GroupBy(r => r.StatusName).ToList();
        analytics.StatusBreakdown = statusGroups.Select(g => new StatusStat
        {
            StatusName = g.Key,
            Count = g.Count(),
            Percentage = (double)g.Count() / analytics.TotalReservations * 100,
            Revenue = g.Sum(r => r.TotalAmount)
        }).ToList();

        // Resource Analytics
        await CalculateResourceAnalyticsAsync(reservations, analytics);

        // Daily Sales Data
        analytics.DailySalesData = reservations
            .GroupBy(r => r.StartDate.Date)
            .Select(g => new DailySales
            {
                Date = g.Key,
                Revenue = g.Sum(r => r.TotalAmount),
                NetRevenue = g.Sum(r => r.NetAmount),
                ReservationCount = g.Count()
            })
            .OrderBy(d => d.Date)
            .ToList();

        // Peak Analysis
        if (analytics.DailySalesData.Any())
        {
            var busiestDay = analytics.DailySalesData.OrderByDescending(d => d.ReservationCount).First();
            var bestRevenueDay = analytics.DailySalesData.OrderByDescending(d => d.Revenue).First();

            analytics.BusiestDay = $"{busiestDay.Date:yyyy-MM-dd} ({busiestDay.ReservationCount} reservations)";
            analytics.BestRevenueDay = $"{bestRevenueDay.Date:yyyy-MM-dd} ({analytics.Currency}{bestRevenueDay.Revenue:F2})";
        }

        return analytics;
    }

    private async Task<List<string>> GetResourceNumbersForReservation(Guid reservationId)
    {
        var resourceIds = await QueryAsync<Guid>(
            @"SELECT ""ResourceId"" FROM ""ReservationResources""
              WHERE ""ReservationId"" = @ReservationId
                AND ""IsDeleted"" = false",
            new { ReservationId = reservationId }
        );

        var resourceNumbers = new List<string>();

        foreach (var resourceId in resourceIds)
        {
            try
            {
                var resourceRequest = new ResourceService.GetByIdRequest
                {
                    Id = resourceId.ToString()
                };
                var resource = await _resourceServiceClient.GetResourceByIdAsync(resourceRequest);
                if (resource != null && !string.IsNullOrEmpty(resource.Number))
                {
                    resourceNumbers.Add(resource.Number);
                }
            }
            catch
            {
                // If we can't get resource details, skip it
                continue;
            }
        }

        return resourceNumbers;
    }
    
    
    private async Task CreateUsersSheet(ClosedXML.Excel.XLWorkbook workbook, List<EnrichedReservationData> reservations, ReservationAnalytics analytics, string organizationId)
    {
        var worksheet = workbook.Worksheets.Add("Users");
        
        // Title
        worksheet.Cell(1, 1).Value = "Organization Users Performance";
        worksheet.Cell(1, 1).Style.Font.FontSize = 18;
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Range(1, 1, 1, 6).Merge();

        // Period info
        worksheet.Cell(2, 1).Value = $"Period: {analytics.StartDate:yyyy-MM-dd} to {analytics.EndDate:yyyy-MM-dd}";
        worksheet.Cell(2, 1).Style.Font.Italic = true;
        worksheet.Range(2, 1, 2, 6).Merge();

        var row = 4;

        // Get organization users from Organization service
        var organizationUserIds = new HashSet<string>(); // User IDs of organization users
        var organizationUserDetails = new Dictionary<string, (string Username, string Email)>();
        
        try
        {
            var orgUsersRequest = new OrganizationService.GetUserOfOrganizationRequest
            {
                OrganizationId = organizationId
            };
            var orgUsersResponse = await _organizationClient.GetUserOfOrganizationsAsync(orgUsersRequest);
            
            if (orgUsersResponse?.OrganizationUsers != null)
            {
                foreach (var orgUser in orgUsersResponse.OrganizationUsers)
                {
                    organizationUserIds.Add(orgUser.UserId);
                    if (orgUser.User != null)
                    {
                        organizationUserDetails[orgUser.UserId] = (
                            orgUser.User.Username ?? "Unknown User",
                            orgUser.User.Email ?? ""
                        );
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Log error but continue with empty list (will show no users)
            Console.WriteLine($"Error getting organization users: {ex.Message}");
        }

        // Get reservations with UserIds to match against organization users
        var reservationUserIds = await QueryAsync<dynamic>(
            @"SELECT ""Id"", ""UserId"", ""Code"" FROM ""Reservations"" 
              WHERE ""OrganizationId"" = @OrgId AND ""IsDeleted"" = false",
            new { OrgId = Guid.Parse(organizationId) }
        );

        // Create lookup from reservation code to userId
        var reservationUserLookup = reservationUserIds.ToDictionary(
            r => r.Code?.ToString() ?? "",
            r => r.UserId?.ToString() ?? ""
        );

        // Filter reservations to only include those made by organization users
        var orgStaffReservations = reservations
            .Where(r => 
                reservationUserLookup.ContainsKey(r.ReservationCode) &&
                organizationUserIds.Contains(reservationUserLookup[r.ReservationCode])
            )
            .ToList();

        // Calculate user performance from organization staff reservations only
        var userPerformance = new List<dynamic>();

        if (orgStaffReservations.Any())
        {
            // Group by userId (from lookup) and calculate performance
            var userGroups = orgStaffReservations
                .GroupBy(r => reservationUserLookup[r.ReservationCode])
                .ToList();

            userPerformance = userGroups.Select(g => {
                var userId = g.Key;
                var userDetails = organizationUserDetails.ContainsKey(userId) 
                    ? organizationUserDetails[userId] 
                    : ("Unknown User", "");

                return new {
                    Username = userDetails.Item1,
                    Email = userDetails.Item2,
                    ReservationCount = g.Count(),
                    TotalRevenue = g.Sum(r => r.TotalAmount),
                    NetRevenue = g.Sum(r => r.NetAmount),
                    AverageRevenue = g.Average(r => r.TotalAmount),
                    AverageNetRevenue = g.Average(r => r.NetAmount),
                    Currency = g.FirstOrDefault()?.Currency ?? analytics.Currency
                };
            })
            .OrderByDescending(u => u.TotalRevenue)
            .ToList<dynamic>();
        }

        if (userPerformance.Any())
        {
            // Add info about organization staff
            worksheet.Cell(row, 1).Value = $"📊 ORGANIZATION STAFF PERFORMANCE ({userPerformance.Count} staff members)";
            worksheet.Cell(row, 1).Style.Font.Bold = true;
            worksheet.Cell(row, 1).Style.Font.FontSize = 12;
            worksheet.Cell(row, 1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGreen;
            worksheet.Range(row, 1, row, 6).Merge();
            row += 2;

            // Headers
            worksheet.Cell(row, 1).Value = "Username";
            worksheet.Cell(row, 2).Value = "Email";
            worksheet.Cell(row, 3).Value = "Reservations";
            worksheet.Cell(row, 4).Value = "Total Revenue";
            worksheet.Cell(row, 5).Value = "Net Revenue";
            worksheet.Cell(row, 6).Value = "Avg Revenue/Reservation";

            // Style headers
            var headerRange = worksheet.Range(row, 1, row, 6);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightBlue;
            headerRange.Style.Border.OutsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
            headerRange.Style.Border.InsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
            row++;

            // Data rows
            foreach (var user in userPerformance)
            {
                worksheet.Cell(row, 1).Value = user.Username;
                worksheet.Cell(row, 2).Value = user.Email;
                worksheet.Cell(row, 3).Value = user.ReservationCount;
                worksheet.Cell(row, 4).Value = $"{user.Currency}{user.TotalRevenue:F2}";
                worksheet.Cell(row, 5).Value = $"{user.Currency}{user.NetRevenue:F2}";
                worksheet.Cell(row, 6).Value = $"{user.Currency}{user.AverageRevenue:F2}";
                
                // Alternate row colors for better readability
                if ((row - 5) % 2 == 0)
                {
                    worksheet.Range(row, 1, row, 6).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGray;
                }
                
                row++;
            }

            // Summary section
            row += 2;
            worksheet.Cell(row, 1).Value = "📊 USERS SUMMARY";
            worksheet.Cell(row, 1).Style.Font.Bold = true;
            worksheet.Cell(row, 1).Style.Font.FontSize = 14;
            worksheet.Cell(row, 1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.Orange;
            worksheet.Range(row, 1, row, 6).Merge();
            row += 2;

            var totalUsers = userPerformance.Count;
            var totalUserRevenue = userPerformance.Sum(u => (double)u.TotalRevenue);
            var totalUserNetRevenue = userPerformance.Sum(u => (double)u.NetRevenue);
            var totalUserReservations = userPerformance.Sum(u => (int)u.ReservationCount);
            var avgRevenuePerUser = totalUserRevenue / totalUsers;
            var avgNetRevenuePerUser = totalUserNetRevenue / totalUsers;

            worksheet.Cell(row, 1).Value = "Active Users:";
            worksheet.Cell(row, 2).Value = totalUsers;
            worksheet.Cell(row, 3).Value = "Total User Revenue:";
            worksheet.Cell(row, 4).Value = $"{analytics.Currency}{totalUserRevenue:F2}";
            row++;

            worksheet.Cell(row, 1).Value = "Total User Net Revenue:";
            worksheet.Cell(row, 2).Value = $"{analytics.Currency}{totalUserNetRevenue:F2}";
            worksheet.Cell(row, 3).Value = "Total User Reservations:";
            worksheet.Cell(row, 4).Value = totalUserReservations;
            row++;

            worksheet.Cell(row, 1).Value = "Avg Revenue/User:";
            worksheet.Cell(row, 2).Value = $"{analytics.Currency}{avgRevenuePerUser:F2}";
            worksheet.Cell(row, 3).Value = "Avg Net Revenue/User:";
            worksheet.Cell(row, 4).Value = $"{analytics.Currency}{avgNetRevenuePerUser:F2}";
            row += 2;

            // Top performers
            if (userPerformance.Any())
            {
                worksheet.Cell(row, 1).Value = "🏆 TOP PERFORMERS";
                worksheet.Cell(row, 1).Style.Font.Bold = true;
                worksheet.Cell(row, 1).Style.Font.FontSize = 12;
                worksheet.Cell(row, 1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.Gold;
                worksheet.Range(row, 1, row, 6).Merge();
                row += 2;

                var topRevenue = userPerformance.First();
                var topReservations = userPerformance.OrderByDescending(u => u.ReservationCount).First();
                var topAverage = userPerformance.OrderByDescending(u => u.AverageRevenue).First();
                var topNet = userPerformance.OrderByDescending(u => u.NetRevenue).First();

                worksheet.Cell(row, 1).Value = "Highest Revenue:";
                worksheet.Cell(row, 2).Value = $"{topRevenue.Username} ({topRevenue.Currency}{topRevenue.TotalRevenue:F2})";
                worksheet.Range(row, 2, row, 6).Merge();
                row++;

                worksheet.Cell(row, 1).Value = "Highest Net Revenue:";
                worksheet.Cell(row, 2).Value = $"{topNet.Username} ({topNet.Currency}{topNet.NetRevenue:F2})";
                worksheet.Range(row, 2, row, 6).Merge();
                row++;

                worksheet.Cell(row, 1).Value = "Most Reservations:";
                worksheet.Cell(row, 2).Value = $"{topReservations.Username} ({topReservations.ReservationCount} reservations)";
                worksheet.Range(row, 2, row, 6).Merge();
                row++;

                worksheet.Cell(row, 1).Value = "Highest Average:";
                worksheet.Cell(row, 2).Value = $"{topAverage.Username} ({topAverage.Currency}{topAverage.AverageRevenue:F2}/reservation)";
                worksheet.Range(row, 2, row, 6).Merge();
            }
        }
        else
        {
            worksheet.Cell(row, 1).Value = "No organization staff reservations found for this period.";
            worksheet.Cell(row, 1).Style.Font.Italic = true;
            worksheet.Cell(row, 1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightYellow;
            worksheet.Range(row, 1, row, 6).Merge();
            row += 2;
            
            worksheet.Cell(row, 1).Value = "ℹ️ Note: This sheet only shows reservations made by organization staff members, not customer reservations.";
            worksheet.Cell(row, 1).Style.Font.Italic = true;
            worksheet.Cell(row, 1).Style.Font.FontSize = 10;
            worksheet.Range(row, 1, row, 6).Merge();
        }

        // Auto-fit columns
        worksheet.Columns().AdjustToContents();
    }
    
    
    private async Task CalculateResourceAnalyticsAsync(
        List<EnrichedReservationData> reservations,
        ReservationAnalytics analytics
    )
    {
        // Group by resource numbers
        var resourceGroups = reservations
            .Where(r => !string.IsNullOrEmpty(r.ResourceNumbers))
            .SelectMany(r => r.ResourceNumbers.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(resourceNum => new { ResourceNumber = resourceNum.Trim(), Reservation = r }))
            .GroupBy(x => x.ResourceNumber)
            .ToList();

        if (resourceGroups.Any())
        {
            // Most Reserved Resource
            var mostReserved = resourceGroups
                .OrderByDescending(g => g.Count())
                .First();

            analytics.MostReservedResource = new ResourceStat
            {
                ResourceNumber = mostReserved.Key,
                ReservationCount = mostReserved.Count(),
                TotalRevenue = mostReserved.Sum(x => x.Reservation.TotalAmount),
                AverageValue = mostReserved.Average(x => x.Reservation.TotalAmount)
            };

            // Most Profitable Resource
            var mostProfitable = resourceGroups
                .OrderByDescending(g => g.Sum(x => x.Reservation.TotalAmount))
                .First();

            analytics.MostProfitableResource = new ResourceStat
            {
                ResourceNumber = mostProfitable.Key,
                ReservationCount = mostProfitable.Count(),
                TotalRevenue = mostProfitable.Sum(x => x.Reservation.TotalAmount),
                AverageValue = mostProfitable.Average(x => x.Reservation.TotalAmount)
            };
        }
    }
}