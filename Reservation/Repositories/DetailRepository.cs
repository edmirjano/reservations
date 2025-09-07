using System.Data;
using Core.Repositories;
using Reservation.Data;
using Reservation.Interfaces;
using Reservation.Models;
using ReservationService;

namespace Reservation.Repositories;

public class DetailRepository(
    ReservationContext reservationContext,
    Func<IDbConnection> dbConnectionFactory
) : GenericRepository<Detail, ReservationContext>(reservationContext, dbConnectionFactory), IDetail
{
    /// <summary>
    /// Gets the detail information for a reservation by its ID asynchronously.
    /// </summary>
    /// <param name="reservationId">The reservation ID.</param>
    /// <returns>The detail DTO, or null if not found.</returns>
    public async Task<DetailDTO?> GetDetailByReservationIdAsync(Guid reservationId)
    {
        var query =
            @"
            SELECT d.* 
            FROM ""Details"" d
            WHERE d.""ReservationId"" = @ReservationId 
            AND d.""IsDeleted"" = false 
            AND d.""IsActive"" = true";

        // Use the new QueryMultipleAsync method with a processor function
        var detail = await QueryMultipleAsync(
            query,
            new { ReservationId = reservationId },
            async gridReader =>
            {
                // It seems you expect only one result set (Details)
                // and then take the FirstOrDefault from that set.
                // If that's the case, QueryFirstOrDefaultAsync might be more direct.
                // However, sticking to QueryMultipleAsync as per the refactor:
                var details = await gridReader.ReadAsync<Detail>();
                return details.FirstOrDefault();
            }
        );

        if (detail == null)
        {
            return null;
        }

        return new DetailDTO
        {
            Name = detail.Name,
            Email = detail.Email,
            Phone = detail.Phone,
            NumberOfAdults = detail.NumberOfAdults,
            NumberOfChildren = detail.NumberOfChildren,
            NumberOfInfants = detail.NumberOfInfants,
            NumberOfPets = detail.NumberOfPets,
            ResourceQuantity = detail.ResourceQuantity,
            Note = detail.Note,
            OriginalPrice = detail.OriginalPrice,
            Discount = detail.Discount,
            Currency = detail.Currency,
        };
    }
}
