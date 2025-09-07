namespace Reservation.Helpers;


public static class ExcelReportHelper
{
    public static byte[] GenerateReservationExcel(List<Models.Reservation> reservations)
    {
        using (var workbook = new ClosedXML.Excel.XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("Reservations");
            worksheet.Cell(1, 1).Value = "ReservationId";
            worksheet.Cell(1, 2).Value = "OrganizationId";
            worksheet.Cell(1, 3).Value = "UserId";
            worksheet.Cell(1, 4).Value = "StatusId";
            worksheet.Cell(1, 5).Value = "TotalAmount";
            worksheet.Cell(1, 6).Value = "StartDate";
            worksheet.Cell(1, 7).Value = "EndDate";
            worksheet.Cell(1, 8).Value = "Code";
            int row = 2;
            foreach (var reservation in reservations)
            {
                worksheet.Cell(row, 1).Value = reservation.Id.ToString();
                worksheet.Cell(row, 2).Value = reservation.OrganizationId.ToString();
                worksheet.Cell(row, 3).Value = reservation.UserId.ToString();
                worksheet.Cell(row, 4).Value = reservation.StatusId.ToString();
                worksheet.Cell(row, 5).Value = reservation.TotalAmount;
                worksheet.Cell(row, 6).Value = reservation.StartDate.ToString("u");
                worksheet.Cell(row, 7).Value = reservation.EndDate.ToString("u");
                worksheet.Cell(row, 8).Value = reservation.Code;
                row++;
            }
            using (var stream = new System.IO.MemoryStream())
            {
                workbook.SaveAs(stream);
                return stream.ToArray();
            }
        }
    }

    public static byte[] GenerateEnrichedReservationExcel(List<EnrichedReservationData> reservations)
    {
        using (var workbook = new ClosedXML.Excel.XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("Reservations");

            // Set headers with improved column names
            worksheet.Cell(1, 1).Value = "Reservation Code";
            worksheet.Cell(1, 2).Value = "Organization Name";
            worksheet.Cell(1, 3).Value = "Username";
            worksheet.Cell(1, 4).Value = "User Email";
            worksheet.Cell(1, 5).Value = "Status";
            worksheet.Cell(1, 6).Value = "Source";
            worksheet.Cell(1, 7).Value = "Total Amount";
            worksheet.Cell(1, 8).Value = "Net Amount";
            worksheet.Cell(1, 9).Value = "Currency";
            worksheet.Cell(1, 10).Value = "Resource Numbers";
            worksheet.Cell(1, 11).Value = "Start Date";
            worksheet.Cell(1, 12).Value = "End Date";
            worksheet.Cell(1, 13).Value = "Customer Name";
            worksheet.Cell(1, 14).Value = "Customer Phone";
            worksheet.Cell(1, 15).Value = "Customer Email";
            worksheet.Cell(1, 16).Value = "Notes";

            // Style the headers
            var headerRange = worksheet.Range(1, 1, 1, 16);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGray;
            headerRange.Style.Border.OutsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
            headerRange.Style.Border.InsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;

            int row = 2;
            foreach (var reservation in reservations)
            {
                worksheet.Cell(row, 1).Value = reservation.ReservationCode;
                worksheet.Cell(row, 2).Value = reservation.OrganizationName;
                worksheet.Cell(row, 3).Value = reservation.Username;
                worksheet.Cell(row, 4).Value = reservation.UserEmail;
                worksheet.Cell(row, 5).Value = reservation.StatusName;
                worksheet.Cell(row, 6).Value = reservation.Source;
                worksheet.Cell(row, 7).Value = reservation.TotalAmount;
                worksheet.Cell(row, 8).Value = reservation.NetAmount;
                worksheet.Cell(row, 9).Value = reservation.Currency;
                worksheet.Cell(row, 10).Value = reservation.ResourceNumbers;
                worksheet.Cell(row, 11).Value = reservation.StartDate.ToString("yyyy-MM-dd HH:mm");
                worksheet.Cell(row, 12).Value = reservation.EndDate.ToString("yyyy-MM-dd HH:mm");
                worksheet.Cell(row, 13).Value = reservation.CustomerName;
                worksheet.Cell(row, 14).Value = reservation.CustomerPhone;
                worksheet.Cell(row, 15).Value = reservation.CustomerEmail;
                worksheet.Cell(row, 16).Value = reservation.Notes;
                row++;
            }

            // Auto-fit columns
            worksheet.Columns().AdjustToContents();

            using (var stream = new System.IO.MemoryStream())
            {
                workbook.SaveAs(stream);
                return stream.ToArray();
            }
        }
    }



    public static void CreateSummarySheet(ClosedXML.Excel.XLWorkbook workbook, ReservationAnalytics analytics)
    {
        var worksheet = workbook.Worksheets.Add("Summary & Analytics");

        // Title
        worksheet.Cell(1, 1).Value = "Reservation Report Summary";
        worksheet.Cell(1, 1).Style.Font.FontSize = 18;
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Range(1, 1, 1, 4).Merge();

        // Date Range
        worksheet.Cell(2, 1).Value = $"Report Period: {analytics.StartDate:yyyy-MM-dd} to {analytics.EndDate:yyyy-MM-dd}";
        worksheet.Cell(2, 1).Style.Font.Italic = true;
        worksheet.Range(2, 1, 2, 4).Merge();

        var row = 4;

        // Key Metrics Section
        worksheet.Cell(row, 1).Value = "KEY METRICS";
        worksheet.Cell(row, 1).Style.Font.Bold = true;
        worksheet.Cell(row, 1).Style.Font.FontSize = 14;
        worksheet.Cell(row, 1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightBlue;
        worksheet.Range(row, 1, row, 4).Merge();
        row += 2;

        worksheet.Cell(row, 1).Value = "Total Reservations:";
        worksheet.Cell(row, 2).Value = analytics.TotalReservations;
        worksheet.Cell(row, 3).Value = "Total Revenue:";
        worksheet.Cell(row, 4).Value = $"{analytics.Currency}{analytics.TotalRevenue:F2}";
        row++;

        worksheet.Cell(row, 1).Value = "Net Revenue:";
        worksheet.Cell(row, 2).Value = $"{analytics.Currency}{analytics.NetRevenue:F2}";
        worksheet.Cell(row, 3).Value = "Average Value:";
        worksheet.Cell(row, 4).Value = $"{analytics.Currency}{analytics.AverageReservationValue:F2}";
        row++;

        worksheet.Cell(row, 1).Value = "Daily Average:";
        worksheet.Cell(row, 2).Value = $"{analytics.Currency}{analytics.DailyAverageRevenue:F2}";
        worksheet.Cell(row, 3).Value = "Total Days:";
        worksheet.Cell(row, 4).Value = analytics.TotalDays;
        row += 2;

        // Source Breakdown
        worksheet.Cell(row, 1).Value = "RESERVATION SOURCES";
        worksheet.Cell(row, 1).Style.Font.Bold = true;
        worksheet.Cell(row, 1).Style.Font.FontSize = 14;
        worksheet.Cell(row, 1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGreen;
        worksheet.Range(row, 1, row, 4).Merge();
        row += 2;

        worksheet.Cell(row, 1).Value = "Organization Bookings:";
        worksheet.Cell(row, 2).Value = analytics.OrganizationReservations;
        worksheet.Cell(row, 3).Value = "SunEasy Bookings:";
        worksheet.Cell(row, 4).Value = analytics.SunEasyReservations;
        row += 2;

        // Resource Analytics
        worksheet.Cell(row, 1).Value = "TOP PERFORMING RESOURCES";
        worksheet.Cell(row, 1).Style.Font.Bold = true;
        worksheet.Cell(row, 1).Style.Font.FontSize = 14;
        worksheet.Cell(row, 1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightYellow;
        worksheet.Range(row, 1, row, 4).Merge();
        row += 2;

        if (!string.IsNullOrEmpty(analytics.MostReservedResource.ResourceNumber))
        {
            worksheet.Cell(row, 1).Value = "Most Reserved:";
            worksheet.Cell(row, 2).Value = $"{analytics.MostReservedResource.ResourceNumber} ({analytics.MostReservedResource.ReservationCount} bookings)";
            worksheet.Range(row, 2, row, 4).Merge();
            row++;

            worksheet.Cell(row, 1).Value = "Most Profitable:";
            worksheet.Cell(row, 2).Value = $"{analytics.MostProfitableResource.ResourceNumber} ({analytics.Currency}{analytics.MostProfitableResource.TotalRevenue:F2})";
            worksheet.Range(row, 2, row, 4).Merge();
            row += 2;
        }

        // Peak Performance
        worksheet.Cell(row, 1).Value = "PEAK PERFORMANCE";
        worksheet.Cell(row, 1).Style.Font.Bold = true;
        worksheet.Cell(row, 1).Style.Font.FontSize = 14;
        worksheet.Cell(row, 1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightCoral;
        worksheet.Range(row, 1, row, 4).Merge();
        row += 2;

        worksheet.Cell(row, 1).Value = "Busiest Day:";
        worksheet.Cell(row, 2).Value = analytics.BusiestDay;
        worksheet.Range(row, 2, row, 4).Merge();
        row++;

        worksheet.Cell(row, 1).Value = "Best Revenue Day:";
        worksheet.Cell(row, 2).Value = analytics.BestRevenueDay;
        worksheet.Range(row, 2, row, 4).Merge();
        row += 2;

        // Status Breakdown
        if (analytics.StatusBreakdown.Any())
        {
            worksheet.Cell(row, 1).Value = "STATUS BREAKDOWN";
            worksheet.Cell(row, 1).Style.Font.Bold = true;
            worksheet.Cell(row, 1).Style.Font.FontSize = 14;
            worksheet.Cell(row, 1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGray;
            worksheet.Range(row, 1, row, 4).Merge();
            row += 2;

            worksheet.Cell(row, 1).Value = "Status";
            worksheet.Cell(row, 2).Value = "Count";
            worksheet.Cell(row, 3).Value = "Percentage";
            worksheet.Cell(row, 4).Value = "Revenue";

            var headerRange = worksheet.Range(row, 1, row, 4);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGray;
            row++;

            foreach (var status in analytics.StatusBreakdown.OrderByDescending(s => s.Count))
            {
                worksheet.Cell(row, 1).Value = status.StatusName;
                worksheet.Cell(row, 2).Value = status.Count;
                worksheet.Cell(row, 3).Value = $"{status.Percentage:F1}%";
                worksheet.Cell(row, 4).Value = $"{analytics.Currency}{status.Revenue:F2}";
                row++;
            }
        }

        // Auto-fit columns
        worksheet.Columns().AdjustToContents();
    }

    public static void CreateChartsSheet(ClosedXML.Excel.XLWorkbook workbook, ReservationAnalytics analytics)
    {
        var worksheet = workbook.Worksheets.Add("Sales Chart");

        // Title
        worksheet.Cell(1, 1).Value = "Daily Sales Performance";
        worksheet.Cell(1, 1).Style.Font.FontSize = 16;
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Range(1, 1, 1, 4).Merge();

        // Chart data headers
        var row = 3;
        worksheet.Cell(row, 1).Value = "Date";
        worksheet.Cell(row, 2).Value = "Revenue";
        worksheet.Cell(row, 3).Value = "Net Revenue";
        worksheet.Cell(row, 4).Value = "Reservations";

        var headerRange = worksheet.Range(row, 1, row, 4);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightBlue;
        headerRange.Style.Border.OutsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
        row++;

        // Chart data
        foreach (var dailySale in analytics.DailySalesData)
        {
            worksheet.Cell(row, 1).Value = dailySale.Date.ToString("yyyy-MM-dd");
            worksheet.Cell(row, 2).Value = dailySale.Revenue;
            worksheet.Cell(row, 3).Value = dailySale.NetRevenue;
            worksheet.Cell(row, 4).Value = dailySale.ReservationCount;
            row++;
        }

        // Add a simple chart (ClosedXML has limited charting, but we can create data for external charting)
        var chartStartRow = row + 2;
        worksheet.Cell(chartStartRow, 1).Value = "📊 SALES TRENDS";
        worksheet.Cell(chartStartRow, 1).Style.Font.Bold = true;
        worksheet.Cell(chartStartRow, 1).Style.Font.FontSize = 14;

        chartStartRow += 2;
        if (analytics.DailySalesData.Any())
        {
            var maxRevenue = analytics.DailySalesData.Max(d => d.Revenue);
            var minRevenue = analytics.DailySalesData.Min(d => d.Revenue);
            var avgRevenue = analytics.DailySalesData.Average(d => d.Revenue);

            worksheet.Cell(chartStartRow, 1).Value = "Highest Daily Revenue:";
            worksheet.Cell(chartStartRow, 2).Value = $"{analytics.Currency}{maxRevenue:F2}";
            chartStartRow++;

            worksheet.Cell(chartStartRow, 1).Value = "Lowest Daily Revenue:";
            worksheet.Cell(chartStartRow, 2).Value = $"{analytics.Currency}{minRevenue:F2}";
            chartStartRow++;

            worksheet.Cell(chartStartRow, 1).Value = "Average Daily Revenue:";
            worksheet.Cell(chartStartRow, 2).Value = $"{analytics.Currency}{avgRevenue:F2}";
            chartStartRow++;

            // Simple trend indicator
            var firstHalf = analytics.DailySalesData.Take(analytics.DailySalesData.Count / 2).Average(d => d.Revenue);
            var secondHalf = analytics.DailySalesData.Skip(analytics.DailySalesData.Count / 2).Average(d => d.Revenue);
            var trend = secondHalf > firstHalf ? "📈 Increasing" : secondHalf < firstHalf ? "📉 Decreasing" : "➡️ Stable";

            worksheet.Cell(chartStartRow, 1).Value = "Trend:";
            worksheet.Cell(chartStartRow, 2).Value = trend;
        }

        // Auto-fit columns
        worksheet.Columns().AdjustToContents();
    }



    public static void CreateDataSheet(ClosedXML.Excel.XLWorkbook workbook, List<EnrichedReservationData> reservations)
    {
        var worksheet = workbook.Worksheets.Add("Reservations Data");

        // Set headers with improved column names
        worksheet.Cell(1, 1).Value = "Reservation Code";
        worksheet.Cell(1, 2).Value = "Organization Name";
        worksheet.Cell(1, 3).Value = "Username";
        worksheet.Cell(1, 4).Value = "User Email";
        worksheet.Cell(1, 5).Value = "Status";
        worksheet.Cell(1, 6).Value = "Source";
        worksheet.Cell(1, 7).Value = "Total Amount";
        worksheet.Cell(1, 8).Value = "Net Amount";
        worksheet.Cell(1, 9).Value = "Currency";
        worksheet.Cell(1, 10).Value = "Resource Numbers";
        worksheet.Cell(1, 11).Value = "Start Date";
        worksheet.Cell(1, 12).Value = "End Date";
        worksheet.Cell(1, 13).Value = "Customer Name";
        worksheet.Cell(1, 14).Value = "Customer Phone";
        worksheet.Cell(1, 15).Value = "Customer Email";
        worksheet.Cell(1, 16).Value = "Notes";

        // Style the headers
        var headerRange = worksheet.Range(1, 1, 1, 16);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGray;
        headerRange.Style.Border.OutsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
        headerRange.Style.Border.InsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;

        int row = 2;
        foreach (var reservation in reservations)
        {
            worksheet.Cell(row, 1).Value = reservation.ReservationCode;
            worksheet.Cell(row, 2).Value = reservation.OrganizationName;
            worksheet.Cell(row, 3).Value = reservation.Username;
            worksheet.Cell(row, 4).Value = reservation.UserEmail;
            worksheet.Cell(row, 5).Value = reservation.StatusName;
            worksheet.Cell(row, 6).Value = reservation.Source;
            worksheet.Cell(row, 7).Value = reservation.TotalAmount;
            worksheet.Cell(row, 8).Value = reservation.NetAmount;
            worksheet.Cell(row, 9).Value = reservation.Currency;
            worksheet.Cell(row, 10).Value = reservation.ResourceNumbers;
            worksheet.Cell(row, 11).Value = reservation.StartDate.ToString("yyyy-MM-dd HH:mm");
            worksheet.Cell(row, 12).Value = reservation.EndDate.ToString("yyyy-MM-dd HH:mm");
            worksheet.Cell(row, 13).Value = reservation.CustomerName;
            worksheet.Cell(row, 14).Value = reservation.CustomerPhone;
            worksheet.Cell(row, 15).Value = reservation.CustomerEmail;
            worksheet.Cell(row, 16).Value = reservation.Notes;
            row++;
        }

        // Auto-fit columns
        worksheet.Columns().AdjustToContents();
    }

    public static void CreateResourceDataSheet(ClosedXML.Excel.XLWorkbook workbook, List<EnrichedReservationData> reservations, ReservationAnalytics analytics)
    {
        var worksheet = workbook.Worksheets.Add("Resource Data");

        // Title
        worksheet.Cell(1, 1).Value = "Resource Performance Analysis";
        worksheet.Cell(1, 1).Style.Font.FontSize = 18;
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Range(1, 1, 1, 6).Merge();

        // Period info
        worksheet.Cell(2, 1).Value = $"Period: {analytics.StartDate:yyyy-MM-dd} to {analytics.EndDate:yyyy-MM-dd}";
        worksheet.Cell(2, 1).Style.Font.Italic = true;
        worksheet.Range(2, 1, 2, 6).Merge();

        var row = 4;

        // Calculate resource performance data
        var resourcePerformance = reservations
            .Where(r => !string.IsNullOrEmpty(r.ResourceNumbers))
            .SelectMany(r => r.ResourceNumbers.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(resourceNum => new {
                    ResourceNumber = resourceNum.Trim(),
                    TotalAmount = r.TotalAmount,
                    NetAmount = r.NetAmount,
                    Currency = r.Currency
                }))
            .GroupBy(x => x.ResourceNumber)
            .Select(g => new {
                ResourceNumber = g.Key,
                SalesCount = g.Count(),
                TotalRevenue = g.Sum(x => x.TotalAmount),
                NetRevenue = g.Sum(x => x.NetAmount),
                AverageRevenue = g.Average(x => x.TotalAmount),
                AverageNetRevenue = g.Average(x => x.NetAmount)
            })
            .OrderByDescending(x => x.TotalRevenue)
            .ToList();

        // Headers
        worksheet.Cell(row, 1).Value = "Resource Number";
        worksheet.Cell(row, 2).Value = "Sales Count";
        worksheet.Cell(row, 3).Value = "Total Revenue";
        worksheet.Cell(row, 4).Value = "Net Revenue";
        worksheet.Cell(row, 5).Value = "Avg Revenue/Sale";
        worksheet.Cell(row, 6).Value = "Avg Net/Sale";

        // Style headers
        var headerRange = worksheet.Range(row, 1, row, 6);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightBlue;
        headerRange.Style.Border.OutsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
        headerRange.Style.Border.InsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
        row++;

        // Data rows
        foreach (var resource in resourcePerformance)
        {
            worksheet.Cell(row, 1).Value = resource.ResourceNumber;
            worksheet.Cell(row, 2).Value = resource.SalesCount;
            worksheet.Cell(row, 3).Value = $"{analytics.Currency}{resource.TotalRevenue:F2}";
            worksheet.Cell(row, 4).Value = $"{analytics.Currency}{resource.NetRevenue:F2}";
            worksheet.Cell(row, 5).Value = $"{analytics.Currency}{resource.AverageRevenue:F2}";
            worksheet.Cell(row, 6).Value = $"{analytics.Currency}{resource.AverageNetRevenue:F2}";

            // Alternate row colors for better readability
            if ((row - 5) % 2 == 0)
            {
                worksheet.Range(row, 1, row, 6).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGray;
            }

            row++;
        }

        // Summary section
        row += 2;
        worksheet.Cell(row, 1).Value = "RESOURCE SUMMARY";
        worksheet.Cell(row, 1).Style.Font.Bold = true;
        worksheet.Cell(row, 1).Style.Font.FontSize = 14;
        worksheet.Cell(row, 1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.Orange;
        worksheet.Range(row, 1, row, 6).Merge();
        row += 2;

        if (resourcePerformance.Any())
        {
            var totalResources = resourcePerformance.Count;
            var totalSales = resourcePerformance.Sum(r => (int)r.SalesCount);
            var totalRevenue = resourcePerformance.Sum(r => (double)r.TotalRevenue);
            var avgSalesPerResource = totalSales / (double)totalResources;
            var avgRevenuePerResource = totalRevenue / totalResources;

            worksheet.Cell(row, 1).Value = "Total Resources:";
            worksheet.Cell(row, 2).Value = totalResources;
            worksheet.Cell(row, 3).Value = "Total Sales:";
            worksheet.Cell(row, 4).Value = totalSales;
            row++;

            worksheet.Cell(row, 1).Value = "Total Revenue:";
            worksheet.Cell(row, 2).Value = $"{analytics.Currency}{totalRevenue:F2}";
            worksheet.Cell(row, 3).Value = "Avg Sales/Resource:";
            worksheet.Cell(row, 4).Value = $"{avgSalesPerResource:F1}";
            row++;

            worksheet.Cell(row, 1).Value = "Avg Revenue/Resource:";
            worksheet.Cell(row, 2).Value = $"{analytics.Currency}{avgRevenuePerResource:F2}";
            row += 2;

            // Top performers
            worksheet.Cell(row, 1).Value = "🏆 TOP PERFORMERS";
            worksheet.Cell(row, 1).Style.Font.Bold = true;
            worksheet.Cell(row, 1).Style.Font.FontSize = 12;
            worksheet.Cell(row, 1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.Gold;
            worksheet.Range(row, 1, row, 6).Merge();
            row += 2;

            worksheet.Cell(row, 1).Value = "Highest Revenue:";
            worksheet.Cell(row, 2).Value = $"{resourcePerformance.First().ResourceNumber} ({analytics.Currency}{resourcePerformance.First().TotalRevenue:F2})";
            worksheet.Range(row, 2, row, 6).Merge();
            row++;

            var mostSales = resourcePerformance.OrderByDescending(r => r.SalesCount).First();
            worksheet.Cell(row, 1).Value = "Most Sales:";
            worksheet.Cell(row, 2).Value = $"{mostSales.ResourceNumber} ({mostSales.SalesCount} sales)";
            worksheet.Range(row, 2, row, 6).Merge();
            row++;

            var highestAvg = resourcePerformance.OrderByDescending(r => r.AverageRevenue).First();
            worksheet.Cell(row, 1).Value = "Highest Avg/Sale:";
            worksheet.Cell(row, 2).Value = $"{highestAvg.ResourceNumber} ({analytics.Currency}{highestAvg.AverageRevenue:F2})";
            worksheet.Range(row, 2, row, 6).Merge();
        }

        // Auto-fit columns
        worksheet.Columns().AdjustToContents();
    }

    public static void CreateSunEasyDataSheet(ClosedXML.Excel.XLWorkbook workbook, List<EnrichedReservationData> reservations, ReservationAnalytics analytics)
    {
        var worksheet = workbook.Worksheets.Add("From SunEasy");

        // Filter only SunEasy reservations (source != "Organization")
        var sunEasyReservations = reservations.Where(r => r.Source != "Organization").ToList();

        // Title
        worksheet.Cell(1, 1).Value = "SunEasy Platform Reservations & Resources";
        worksheet.Cell(1, 1).Style.Font.FontSize = 18;
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Range(1, 1, 1, 8).Merge();

        // Period and summary info
        worksheet.Cell(2, 1).Value = $"Period: {analytics.StartDate:yyyy-MM-dd} to {analytics.EndDate:yyyy-MM-dd} | Total SunEasy Reservations: {sunEasyReservations.Count}";
        worksheet.Cell(2, 1).Style.Font.Italic = true;
        worksheet.Range(2, 1, 2, 8).Merge();

        var row = 4;

        // SECTION 1: SunEasy Reservations Table
        worksheet.Cell(row, 1).Value = "📱 SUNEASY PLATFORM RESERVATIONS";
        worksheet.Cell(row, 1).Style.Font.Bold = true;
        worksheet.Cell(row, 1).Style.Font.FontSize = 14;
        worksheet.Cell(row, 1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightBlue;
        worksheet.Range(row, 1, row, 8).Merge();
        row += 2;

        if (sunEasyReservations.Any())
        {
            // Reservations table headers
            worksheet.Cell(row, 1).Value = "Reservation Code";
            worksheet.Cell(row, 2).Value = "Username";
            worksheet.Cell(row, 3).Value = "User Email";
            worksheet.Cell(row, 4).Value = "Status";
            worksheet.Cell(row, 5).Value = "Total Amount";
            worksheet.Cell(row, 6).Value = "Net Amount";
            worksheet.Cell(row, 7).Value = "Resource Numbers";
            worksheet.Cell(row, 8).Value = "Start Date";

            // Style reservation headers
            var reservationHeaderRange = worksheet.Range(row, 1, row, 8);
            reservationHeaderRange.Style.Font.Bold = true;
            reservationHeaderRange.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGray;
            reservationHeaderRange.Style.Border.OutsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
            reservationHeaderRange.Style.Border.InsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
            row++;

            // Reservations data
            foreach (var reservation in sunEasyReservations.OrderByDescending(r => r.TotalAmount))
            {
                worksheet.Cell(row, 1).Value = reservation.ReservationCode;
                worksheet.Cell(row, 2).Value = reservation.Username;
                worksheet.Cell(row, 3).Value = reservation.UserEmail;
                worksheet.Cell(row, 4).Value = reservation.StatusName;
                worksheet.Cell(row, 5).Value = $"{analytics.Currency}{reservation.TotalAmount:F2}";
                worksheet.Cell(row, 6).Value = $"{analytics.Currency}{reservation.NetAmount:F2}";
                worksheet.Cell(row, 7).Value = reservation.ResourceNumbers;
                worksheet.Cell(row, 8).Value = reservation.StartDate.ToString("yyyy-MM-dd");

                // Alternate row colors
                if ((row - (sunEasyReservations.Count > 0 ? row - sunEasyReservations.Count + 1 : row)) % 2 == 0)
                {
                    worksheet.Range(row, 1, row, 8).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGray;
                }

                row++;
            }
        }
        else
        {
            worksheet.Cell(row, 1).Value = "No SunEasy reservations found in this period.";
            worksheet.Cell(row, 1).Style.Font.Italic = true;
            worksheet.Range(row, 1, row, 8).Merge();
            row++;
        }

        row += 3;

        // SECTION 2: SunEasy Resource Performance
        worksheet.Cell(row, 1).Value = "🏖️ SUNEASY RESOURCE PERFORMANCE";
        worksheet.Cell(row, 1).Style.Font.Bold = true;
        worksheet.Cell(row, 1).Style.Font.FontSize = 14;
        worksheet.Cell(row, 1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGreen;
        worksheet.Range(row, 1, row, 6).Merge();
        row += 2;

        if (sunEasyReservations.Any())
        {
            // Calculate SunEasy resource performance
            var sunEasyResourcePerformance = sunEasyReservations
                .Where(r => !string.IsNullOrEmpty(r.ResourceNumbers))
                .SelectMany(r => r.ResourceNumbers.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(resourceNum => new {
                        ResourceNumber = resourceNum.Trim(),
                        TotalAmount = r.TotalAmount,
                        NetAmount = r.NetAmount
                    }))
                .GroupBy(x => x.ResourceNumber)
                .Select(g => new {
                    ResourceNumber = g.Key,
                    SalesCount = g.Count(),
                    TotalRevenue = g.Sum(x => x.TotalAmount),
                    NetRevenue = g.Sum(x => x.NetAmount),
                    AverageRevenue = g.Average(x => x.TotalAmount),
                    AverageNetRevenue = g.Average(x => x.NetAmount)
                })
                .OrderByDescending(x => x.TotalRevenue)
                .ToList();

            // Resource performance table headers
            worksheet.Cell(row, 1).Value = "Resource Number";
            worksheet.Cell(row, 2).Value = "SunEasy Sales";
            worksheet.Cell(row, 3).Value = "Total Revenue";
            worksheet.Cell(row, 4).Value = "Net Revenue";
            worksheet.Cell(row, 5).Value = "Avg Revenue/Sale";
            worksheet.Cell(row, 6).Value = "Avg Net/Sale";

            // Style resource headers
            var resourceHeaderRange = worksheet.Range(row, 1, row, 6);
            resourceHeaderRange.Style.Font.Bold = true;
            resourceHeaderRange.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGreen;
            resourceHeaderRange.Style.Border.OutsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
            resourceHeaderRange.Style.Border.InsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
            row++;

            // Resource performance data
            foreach (var resource in sunEasyResourcePerformance)
            {
                worksheet.Cell(row, 1).Value = resource.ResourceNumber;
                worksheet.Cell(row, 2).Value = resource.SalesCount;
                worksheet.Cell(row, 3).Value = $"{analytics.Currency}{resource.TotalRevenue:F2}";
                worksheet.Cell(row, 4).Value = $"{analytics.Currency}{resource.NetRevenue:F2}";
                worksheet.Cell(row, 5).Value = $"{analytics.Currency}{resource.AverageRevenue:F2}";
                worksheet.Cell(row, 6).Value = $"{analytics.Currency}{resource.AverageNetRevenue:F2}";

                // Alternate row colors
                if ((row - (sunEasyResourcePerformance.Count > 0 ? row - sunEasyResourcePerformance.Count + 1 : row)) % 2 == 0)
                {
                    worksheet.Range(row, 1, row, 6).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGray;
                }

                row++;
            }

            // SunEasy Summary Statistics
            row += 2;
            worksheet.Cell(row, 1).Value = "📊 SUNEASY SUMMARY";
            worksheet.Cell(row, 1).Style.Font.Bold = true;
            worksheet.Cell(row, 1).Style.Font.FontSize = 12;
            worksheet.Cell(row, 1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.Orange;
            worksheet.Range(row, 1, row, 6).Merge();
            row += 2;

            var totalSunEasyRevenue = sunEasyReservations.Sum(r => r.TotalAmount);
            var totalSunEasyNetRevenue = sunEasyReservations.Sum(r => r.NetAmount);
            var avgSunEasyReservationValue = sunEasyReservations.Average(r => r.TotalAmount);
            var totalSunEasyResources = sunEasyResourcePerformance.Count;
            var totalSunEasySales = sunEasyResourcePerformance.Sum(r => (int)r.SalesCount);

            worksheet.Cell(row, 1).Value = "Total SunEasy Revenue:";
            worksheet.Cell(row, 2).Value = $"{analytics.Currency}{totalSunEasyRevenue:F2}";
            worksheet.Cell(row, 3).Value = "Total Net Revenue:";
            worksheet.Cell(row, 4).Value = $"{analytics.Currency}{totalSunEasyNetRevenue:F2}";
            row++;

            worksheet.Cell(row, 1).Value = "Avg Reservation Value:";
            worksheet.Cell(row, 2).Value = $"{analytics.Currency}{avgSunEasyReservationValue:F2}";
            worksheet.Cell(row, 3).Value = "Platform Fee Revenue:";
            worksheet.Cell(row, 4).Value = $"{analytics.Currency}{(totalSunEasyRevenue - totalSunEasyNetRevenue):F2}";
            row++;

            worksheet.Cell(row, 1).Value = "Resources Used:";
            worksheet.Cell(row, 2).Value = totalSunEasyResources;
            worksheet.Cell(row, 3).Value = "Total Bookings:";
            worksheet.Cell(row, 4).Value = totalSunEasySales;
            row += 2;

            // Top SunEasy Performers
            if (sunEasyResourcePerformance.Any())
            {
                worksheet.Cell(row, 1).Value = "🏆 TOP SUNEASY PERFORMERS";
                worksheet.Cell(row, 1).Style.Font.Bold = true;
                worksheet.Cell(row, 1).Style.Font.FontSize = 12;
                worksheet.Cell(row, 1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.Gold;
                worksheet.Range(row, 1, row, 6).Merge();
                row += 2;

                var topRevenue = sunEasyResourcePerformance.First();
                var topSales = sunEasyResourcePerformance.OrderByDescending(r => r.SalesCount).First();
                var topAverage = sunEasyResourcePerformance.OrderByDescending(r => r.AverageRevenue).First();

                worksheet.Cell(row, 1).Value = "Top Revenue:";
                worksheet.Cell(row, 2).Value = $"{topRevenue.ResourceNumber} ({analytics.Currency}{topRevenue.TotalRevenue:F2})";
                worksheet.Range(row, 2, row, 6).Merge();
                row++;

                worksheet.Cell(row, 1).Value = "Most Bookings:";
                worksheet.Cell(row, 2).Value = $"{topSales.ResourceNumber} ({topSales.SalesCount} sales)";
                worksheet.Range(row, 2, row, 6).Merge();
                row++;

                worksheet.Cell(row, 1).Value = "Best Average:";
                worksheet.Cell(row, 2).Value = $"{topAverage.ResourceNumber} ({analytics.Currency}{topAverage.AverageRevenue:F2}/sale)";
                worksheet.Range(row, 2, row, 6).Merge();
            }
        }
        else
        {
            worksheet.Cell(row, 1).Value = "No SunEasy resource data available.";
            worksheet.Cell(row, 1).Style.Font.Italic = true;
            worksheet.Range(row, 1, row, 6).Merge();
        }

        // Auto-fit columns
        worksheet.Columns().AdjustToContents();
    }
}