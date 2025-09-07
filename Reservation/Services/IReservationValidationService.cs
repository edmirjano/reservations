namespace Reservation.Services;

public interface IReservationValidationService
{
    Task ValidateCreateReservationAsync(CreateReservationDto dto);
    Task ValidateUpdateReservationAsync(ReservationDTO dto);
    Task ValidateReservationDatesAsync(DateTime startDate, DateTime endDate);
    Task ValidateResourcesAsync(IEnumerable<ResourceItemDto> resources);
    Task ValidateDetailAsync(DetailDTO detail);
    Task<bool> IsReservationCodeUniqueAsync(string code);
    Task<bool> IsResourceAvailableAsync(Guid resourceId, DateTime startDate, DateTime endDate);
}