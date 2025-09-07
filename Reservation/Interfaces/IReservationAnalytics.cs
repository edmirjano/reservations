using ReservationService;

namespace Reservation.Interfaces;

public interface IReservationAnalytics
{
    Task<IEnumerable<DateReservationCountDTO>> GetReservationsCountPerDayAsync(string organizationId, DateTime startDate, DateTime endDate);
    Task<GetReservationsBySourceCountResponse> GetReservationsBySourceCountAsync(string organizationId, DateTime startDate, DateTime endDate);
}