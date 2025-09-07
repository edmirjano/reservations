using Core.Interfaces;

namespace Reservation.Interfaces;

public interface IReservationNew : IGeneric<Models.Reservation>
{
    Task<ReservationDTO> CreateReservationAsync(CreateReservationDto createDto);
    Task<ReservationDTO> CreateReservationForOrganizationAsync(CreateReservationForOrganizationDto createDto);
    Task<ReservationDTO> UpdateReservationAsync(ReservationDTO reservationDto);
    Task<ReservationDTO> UpdateReservationForOrganizationAsync(ReservationDTO reservationDto);
    Task DeleteReservationAsync(Guid id);
    Task<ReservationDTO> GetReservationByIdAsync(Guid id);
    Task<IEnumerable<ReservationDTO>> GetReservationsAsync(GetReservationsRequest request);
    Task<GetReservationsByResourcesResponse> GetReservationsByResourcesAsync(GetReservationsByResourcesRequest request);
    Task<IEnumerable<DateReservationCountDTO>> GetReservationsCountPerDayAsync(string organizationId, DateTime startDate, DateTime endDate);
    Task<ReservationDTO> ValidateTicketAsync(string code);
    Task<ReservationDTO> GetReservationByResourceIdAsync(Guid resourceId);
    Task<SearchClientsResponse> SearchClientsByNameAsync(string nameQuery, string organizationId, int maxResults);
    Task<GetReservationsBySourceCountResponse> GetReservationsBySourceCountAsync(string organizationId, DateTime startDate, DateTime endDate);
    Task<byte[]> GenerateReservationReportAsync(string organizationId, string startDate, string endDate);
}