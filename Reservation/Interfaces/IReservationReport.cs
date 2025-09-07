namespace Reservation.Interfaces;

public interface IReservationReport
{
    Task<byte[]> GenerateReservationReportAsync(string organizationId, string startDate, string endDate);
}