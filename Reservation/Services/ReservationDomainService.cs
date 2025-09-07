namespace Reservation.Services;

public class ReservationDomainService : IReservationDomainService
{
    private readonly IReservationNew _reservationRepository;
    private readonly IReservationQuery _reservationQueryRepository;
    private readonly IStatus _statusRepository;
    private readonly IDetail _detailRepository;
    private readonly IReservationValidationService _validationService;
    private readonly IReservationPaymentService _paymentService;
    private readonly ILogger<ReservationDomainService> _logger;

    public ReservationDomainService(
        IReservationNew reservationRepository,
        IReservationQuery reservationQueryRepository,
        IStatus statusRepository,
        IDetail detailRepository,
        IReservationValidationService validationService,
        IReservationPaymentService paymentService,
        ILogger<ReservationDomainService> logger)
    {
        _reservationRepository = reservationRepository;
        _reservationQueryRepository = reservationQueryRepository;
        _statusRepository = statusRepository;
        _detailRepository = detailRepository;
        _validationService = validationService;
        _paymentService = paymentService;
        _logger = logger;
    }

    public async Task<ReservationDTO> CreateReservationWithBusinessRulesAsync(CreateReservationDto dto)
    {
        _logger.LogInformation("Creating reservation with business rules for user {UserId} in organization {OrganizationId}", 
            dto.UserId, dto.OrganizationId);

        try
        {
            // 1. Validate business rules
            await ValidateReservationBusinessRulesAsync(dto);

            // 2. Process payment
            var paymentResult = await _paymentService.ProcessPaymentAsync(dto);
            if (!paymentResult.IsSuccess)
            {
                throw new PaymentProcessingException(paymentResult.ErrorMessage, paymentResult);
            }

            // 3. Create reservation
            var reservation = await _reservationRepository.CreateReservationAsync(dto);

            _logger.LogInformation("Successfully created reservation {ReservationId} with code {ReservationCode}", 
                reservation.Id, reservation.Code);

            return reservation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create reservation for user {UserId} in organization {OrganizationId}", 
                dto.UserId, dto.OrganizationId);
            throw;
        }
    }

    public async Task<ReservationDTO> UpdateReservationWithValidationAsync(ReservationDTO dto)
    {
        _logger.LogInformation("Updating reservation {ReservationId} with validation", dto.Id);

        try
        {
            // 1. Check if reservation exists
            var existingReservation = await _reservationRepository.GetReservationByIdAsync(Guid.Parse(dto.Id));
            if (existingReservation == null)
            {
                throw new ReservationNotFoundException(Guid.Parse(dto.Id));
            }

            // 2. Validate update data
            await _validationService.ValidateUpdateReservationAsync(dto);

            // 3. Check if modification is allowed
            var canModify = await CanModifyReservationAsync(Guid.Parse(dto.Id), Guid.Parse(dto.UserId));
            if (!canModify)
            {
                throw new ReservationAuthorizationException(
                    "User is not authorized to modify this reservation", 
                    Guid.Parse(dto.UserId));
            }

            // 4. Update reservation
            var updatedReservation = await _reservationRepository.UpdateReservationAsync(dto);

            _logger.LogInformation("Successfully updated reservation {ReservationId}", dto.Id);
            return updatedReservation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update reservation {ReservationId}", dto.Id);
            throw;
        }
    }

    public async Task<ReservationDTO> ProcessReservationCancellationAsync(Guid reservationId, string reason)
    {
        _logger.LogInformation("Processing cancellation for reservation {ReservationId}. Reason: {Reason}", 
            reservationId, reason);

        try
        {
            // 1. Get reservation
            var reservation = await _reservationRepository.GetReservationByIdAsync(reservationId);
            if (reservation == null)
            {
                throw new ReservationNotFoundException(reservationId);
            }

            // 2. Check if cancellation is allowed
            var cancelledStatus = await GetStatusByNameAsync("Cancelled");
            if (reservation.StatusId == cancelledStatus.Id)
            {
                throw new InvalidStatusTransitionException("Cancelled", "Cancelled");
            }

            // 3. Process refund if applicable
            var refundResult = await _paymentService.ProcessRefundAsync(reservationId, reservation.TotalAmount, reason);

            // 4. Update status to cancelled
            reservation.StatusId = cancelledStatus.Id;
            var updatedReservation = await _reservationRepository.UpdateReservationAsync(reservation);

            _logger.LogInformation("Successfully cancelled reservation {ReservationId}", reservationId);
            return updatedReservation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel reservation {ReservationId}", reservationId);
            throw;
        }
    }

    public async Task<ReservationDTO> ProcessReservationConfirmationAsync(Guid reservationId)
    {
        _logger.LogInformation("Processing confirmation for reservation {ReservationId}", reservationId);

        try
        {
            // 1. Get reservation
            var reservation = await _reservationRepository.GetReservationByIdAsync(reservationId);
            if (reservation == null)
            {
                throw new ReservationNotFoundException(reservationId);
            }

            // 2. Check if confirmation is allowed
            var confirmedStatus = await GetStatusByNameAsync("Confirmed");
            if (reservation.StatusId == confirmedStatus.Id)
            {
                throw new InvalidStatusTransitionException("Confirmed", "Confirmed");
            }

            // 3. Validate resources are still available
            var detail = await _detailRepository.GetDetailByReservationIdAsync(reservationId);
            if (detail != null)
            {
                var startDate = DateTime.Parse(reservation.StartDate);
                var endDate = DateTime.Parse(reservation.EndDate);
                await ValidateResourceAvailabilityAsync([], startDate, endDate);
            }

            // 4. Update status to confirmed
            reservation.StatusId = confirmedStatus.Id;
            var updatedReservation = await _reservationRepository.UpdateReservationAsync(reservation);

            _logger.LogInformation("Successfully confirmed reservation {ReservationId}", reservationId);
            return updatedReservation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to confirm reservation {ReservationId}", reservationId);
            throw;
        }
    }

    public async Task ValidateReservationBusinessRulesAsync(CreateReservationDto dto)
    {
        // 1. Validate basic data
        await _validationService.ValidateCreateReservationAsync(dto);

        // 2. Validate dates
        var startDate = DateTime.Parse(dto.StartDate);
        var endDate = DateTime.Parse(dto.EndDate);
        await ValidateReservationDatesAsync(startDate, endDate);

        // 3. Validate resource availability
        await ValidateResourceAvailabilityAsync(dto.Resources, startDate, endDate);

        // 4. Validate payment data
        await _paymentService.ValidatePaymentDataAsync(dto);

        _logger.LogInformation("Business rules validation passed for reservation request");
    }

    public async Task ValidateReservationDatesAsync(DateTime startDate, DateTime endDate)
    {
        var now = DateTime.UtcNow;

        // Check if start date is in the past
        if (startDate < now.Date)
        {
            throw new InvalidReservationDateException(startDate, endDate, "Start date cannot be in the past");
        }

        // Check if end date is before start date
        if (endDate <= startDate)
        {
            throw new InvalidReservationDateException(startDate, endDate, "End date must be after start date");
        }

        // Check if reservation is too far in the future (e.g., max 2 years)
        if (startDate > now.AddYears(2))
        {
            throw new InvalidReservationDateException(startDate, endDate, "Reservation cannot be made more than 2 years in advance");
        }

        // Check maximum reservation duration (e.g., max 30 days)
        if ((endDate - startDate).TotalDays > 30)
        {
            throw new InvalidReservationDateException(startDate, endDate, "Reservation duration cannot exceed 30 days");
        }

        await Task.CompletedTask;
    }

    public async Task ValidateResourceAvailabilityAsync(IEnumerable<ResourceItemDto> resources, DateTime startDate, DateTime endDate)
    {
        foreach (var resource in resources)
        {
            var isAvailable = await _validationService.IsResourceAvailableAsync(Guid.Parse(resource.Id), startDate, endDate);
            if (!isAvailable)
            {
                throw new ResourceUnavailableException(Guid.Parse(resource.Id), startDate, endDate);
            }
        }
    }

    public async Task<bool> CanModifyReservationAsync(Guid reservationId, Guid userId)
    {
        try
        {
            var reservation = await _reservationRepository.GetReservationByIdAsync(reservationId);
            if (reservation == null)
            {
                return false;
            }

            // User can modify their own reservation
            if (reservation.UserId == userId.ToString())
            {
                return true;
            }

            // Check if user has organization-level permissions
            // This would typically involve calling the auth service
            // For now, we'll implement basic logic
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking modification permissions for reservation {ReservationId} and user {UserId}", 
                reservationId, userId);
            return false;
        }
    }

    public async Task<ReservationDTO> ProcessStatusTransitionAsync(Guid reservationId, string newStatusName, Guid userId)
    {
        _logger.LogInformation("Processing status transition for reservation {ReservationId} to status {StatusName}", 
            reservationId, newStatusName);

        try
        {
            // 1. Get reservation
            var reservation = await _reservationRepository.GetReservationByIdAsync(reservationId);
            if (reservation == null)
            {
                throw new ReservationNotFoundException(reservationId);
            }

            // 2. Get current and new status
            var currentStatus = await _statusRepository.GetStatusByIdAsync(Guid.Parse(reservation.StatusId));
            var newStatus = await GetStatusByNameAsync(newStatusName);

            // 3. Validate transition
            if (!IsValidStatusTransition(currentStatus.Name, newStatusName))
            {
                throw new InvalidStatusTransitionException(currentStatus.Name, newStatusName);
            }

            // 4. Check authorization
            var canModify = await CanModifyReservationAsync(reservationId, userId);
            if (!canModify)
            {
                throw new ReservationAuthorizationException(
                    "User is not authorized to change reservation status", userId);
            }

            // 5. Update status
            reservation.StatusId = newStatus.Id;
            var updatedReservation = await _reservationRepository.UpdateReservationAsync(reservation);

            _logger.LogInformation("Successfully changed status for reservation {ReservationId} from {OldStatus} to {NewStatus}", 
                reservationId, currentStatus.Name, newStatusName);

            return updatedReservation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process status transition for reservation {ReservationId}", reservationId);
            throw;
        }
    }

    private async Task<StatusDTO> GetStatusByNameAsync(string statusName)
    {
        var statuses = await _statusRepository.GetStatusesAsync(new GetStatusesRequest { Keyword = statusName });
        var status = statuses.FirstOrDefault(s => s.Name.Equals(statusName, StringComparison.OrdinalIgnoreCase));
        
        if (status == null)
        {
            throw new StatusNotFoundException(statusName);
        }

        return status;
    }

    private static bool IsValidStatusTransition(string currentStatus, string newStatus)
    {
        // Define valid status transitions
        var validTransitions = new Dictionary<string, List<string>>
        {
            ["Pending"] = ["Confirmed", "Cancelled"],
            ["Confirmed"] = ["Completed", "Cancelled", "No-Show"],
            ["Completed"] = [],
            ["Cancelled"] = [],
            ["No-Show"] = []
        };

        return validTransitions.ContainsKey(currentStatus) && 
               validTransitions[currentStatus].Contains(newStatus);
    }
}
