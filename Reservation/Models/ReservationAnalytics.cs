namespace Reservation.Models;

/// <summary>
/// Analytics and summary data for reservation reports
/// </summary>
public class ReservationAnalytics
{
    // Basic Statistics
    public int TotalReservations { get; set; }
    public double TotalRevenue { get; set; }
    public double NetRevenue { get; set; }
    public double AverageReservationValue { get; set; }
    public int OrganizationReservations { get; set; }
    public int SunEasyReservations { get; set; }

    // Date Range
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalDays { get; set; }
    public double DailyAverageRevenue { get; set; }

    // Currency
    public string Currency { get; set; } = string.Empty;

    // Resource Analytics
    public ResourceStat MostReservedResource { get; set; } = new();
    public ResourceStat MostProfitableResource { get; set; } = new();

    // Status Analytics
    public List<StatusStat> StatusBreakdown { get; set; } = new();

    // Daily Sales Data for Charts
    public List<DailySales> DailySalesData { get; set; } = new();

    // Peak Analysis
    public string BusiestDay { get; set; } = string.Empty;
    public string BestRevenueDay { get; set; } = string.Empty;
}

public class ResourceStat
{
    public string ResourceNumber { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public int ReservationCount { get; set; }
    public double TotalRevenue { get; set; }
    public double AverageValue { get; set; }
}

public class StatusStat
{
    public string StatusName { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Percentage { get; set; }
    public double Revenue { get; set; }
}

public class DailySales
{
    public DateTime Date { get; set; }
    public double Revenue { get; set; }
    public double NetRevenue { get; set; }
    public int ReservationCount { get; set; }
}
