using ReservationService;

namespace Reservation.Interfaces;

public interface IReservationQuery
{
    Task<ReservationDTO> GetReservationByResourceIdAsync(Guid resourceId);
    Task<IEnumerable<ReservationDTO>> GetReservationsAsync(GetReservationsRequest request);
    Task<GetReservationsByResourcesResponse> GetReservationsByResourcesAsync(GetReservationsByResourcesRequest request);
    Task<SearchClientsResponse> SearchClientsByNameAsync(string nameQuery, string organizationId, int maxResults);
}