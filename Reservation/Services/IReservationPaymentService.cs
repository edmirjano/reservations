namespace Reservation.Services;

public interface IReservationPaymentService
{
    Task<PaymentResult> ProcessPaymentAsync(CreateReservationDto dto);
    Task<PaymentResult> ProcessRefundAsync(Guid reservationId, decimal amount, string reason);
    Task<PaymentResult> ProcessPartialRefundAsync(Guid reservationId, decimal amount, string reason);
    Task ValidatePaymentDataAsync(CreateReservationDto dto);
}