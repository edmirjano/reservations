namespace Reservation.Models;

/// <summary>
/// Enhanced reservation data model for reporting with meaningful names instead of IDs
/// </summary>
public class EnrichedReservationData
{
    public string ReservationCode { get; set; } = string.Empty;
    public string OrganizationName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public double TotalAmount { get; set; }
    public double NetAmount { get; set; }
    public string ResourceNumbers { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
}
