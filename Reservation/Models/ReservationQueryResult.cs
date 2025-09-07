namespace Reservation.Models;

public class ReservationQueryResult
{
    public Guid ReservationId { get; set; }
    public string ReservationCode { get; set; } = string.Empty;
    public Guid OrganizationId { get; set; }
    public Guid UserId { get; set; }
    public Guid StatusId { get; set; }
    public string Source { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal NetAmount { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
}
