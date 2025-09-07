using Core.Interfaces;
using Reservation.Models;
using ReservationService;

namespace Reservation.Interfaces;

public interface IDetail : IGeneric<Detail>
{
    /// <summary>
    /// Gets the detail information for a reservation by its ID asynchronously.
    /// </summary>
    /// <param name="reservationId">The reservation ID.</param>
    /// <returns>The detail DTO.</returns>
    Task<DetailDTO?> GetDetailByReservationIdAsync(Guid reservationId);
}
