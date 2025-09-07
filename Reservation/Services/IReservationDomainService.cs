namespace Reservation.Services;

public interface IReservationDomainService
{
    Task<ReservationDTO> CreateReservationWithBusinessRulesAsync(CreateReservationDto dto);
    Task<ReservationDTO> UpdateReservationWithValidationAsync(ReservationDTO dto);
    Task<ReservationDTO> ProcessReservationCancellationAsync(Guid reservationId, string reason);
    Task<ReservationDTO> ProcessReservationConfirmationAsync(Guid reservationId);
    Task ValidateReservationBusinessRulesAsync(CreateReservationDto dto);
    Task ValidateReservationDatesAsync(DateTime startDate, DateTime endDate);
    Task ValidateResourceAvailabilityAsync(IEnumerable<ResourceItemDto> resources, DateTime startDate, DateTime endDate);
    Task<bool> CanModifyReservationAsync(Guid reservationId, Guid userId);
    Task<ReservationDTO> ProcessStatusTransitionAsync(Guid reservationId, string newStatusName, Guid userId);
}