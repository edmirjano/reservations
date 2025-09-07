using Reservation.Exceptions;
using Reservation.Interfaces;

namespace Reservation.Services;

public class ReservationValidationService : IReservationValidationService
{
    private readonly IReservationQuery _reservationQueryRepository;
    private readonly ILogger<ReservationValidationService> _logger;

    public ReservationValidationService(
        IReservationQuery reservationQueryRepository,
        ILogger<ReservationValidationService> logger)
    {
        _reservationQueryRepository = reservationQueryRepository;
        _logger = logger;
    }

    public async Task ValidateCreateReservationAsync(CreateReservationDto dto)
    {
        var validationErrors = new List<string>();

        // Validate required fields
        if (string.IsNullOrWhiteSpace(dto.UserId))
            validationErrors.Add("UserId is required");

        if (string.IsNullOrWhiteSpace(dto.OrganizationId))
            validationErrors.Add("OrganizationId is required");

        if (string.IsNullOrWhiteSpace(dto.StartDate))
            validationErrors.Add("StartDate is required");

        if (string.IsNullOrWhiteSpace(dto.EndDate))
            validationErrors.Add("EndDate is required");

        // Validate dates
        if (!string.IsNullOrWhiteSpace(dto.StartDate) && !DateTime.TryParse(dto.StartDate, out _))
            validationErrors.Add("StartDate format is invalid");

        if (!string.IsNullOrWhiteSpace(dto.EndDate) && !DateTime.TryParse(dto.EndDate, out _))
            validationErrors.Add("EndDate format is invalid");

        // Validate resources
        if (dto.Resources == null || !dto.Resources.Any())
            validationErrors.Add("At least one resource is required");

        // Validate detail
        if (dto.Detail == null)
            validationErrors.Add("Reservation detail is required");

        if (validationErrors.Any())
        {
            throw new InvalidReservationDataException(
                "Validation failed", 
                new { Errors = validationErrors });
        }

        await Task.CompletedTask;
    }

    public async Task ValidateUpdateReservationAsync(ReservationDTO dto)
    {
        var validationErrors = new List<string>();

        // Validate required fields for update
        if (string.IsNullOrWhiteSpace(dto.Id))
            validationErrors.Add("Reservation ID is required");

        if (dto.TotalAmount < 0)
            validationErrors.Add("Total amount cannot be negative");

        if (validationErrors.Any())
        {
            throw new InvalidReservationDataException(
                "Update validation failed", 
                new { Errors = validationErrors });
        }

        await Task.CompletedTask;
    }

    public async Task ValidateReservationDatesAsync(DateTime startDate, DateTime endDate)
    {
        if (endDate <= startDate)
        {
            throw new InvalidReservationDateException(startDate, endDate, "End date must be after start date");
        }

        await Task.CompletedTask;
    }

    public async Task ValidateResourcesAsync(IEnumerable<ResourceItemDto> resources)
    {
        var validationErrors = new List<string>();

        foreach (var resource in resources)
        {
            if (string.IsNullOrWhiteSpace(resource.Id))
                validationErrors.Add("Resource ID is required");

            if (resource.Price < 0)
                validationErrors.Add($"Resource {resource.Id} price cannot be negative");

            if (resource.Quantity <= 0)
                validationErrors.Add($"Resource {resource.Id} quantity must be greater than 0");
        }

        if (validationErrors.Any())
        {
            throw new InvalidReservationDataException(
                "Resource validation failed", 
                new { Errors = validationErrors });
        }

        await Task.CompletedTask;
    }

    public async Task ValidateDetailAsync(DetailDTO detail)
    {
        var validationErrors = new List<string>();

        if (string.IsNullOrWhiteSpace(detail.Name))
            validationErrors.Add("Customer name is required");

        if (string.IsNullOrWhiteSpace(detail.Email))
            validationErrors.Add("Customer email is required");

        if (!string.IsNullOrWhiteSpace(detail.Email) && !IsValidEmail(detail.Email))
            validationErrors.Add("Customer email format is invalid");

        if (detail.NumberOfAdults < 0)
            validationErrors.Add("Number of adults cannot be negative");

        if (detail.NumberOfChildren < 0)
            validationErrors.Add("Number of children cannot be negative");

        if (validationErrors.Any())
        {
            throw new InvalidReservationDataException(
                "Detail validation failed", 
                new { Errors = validationErrors });
        }

        await Task.CompletedTask;
    }

    public async Task<bool> IsReservationCodeUniqueAsync(string code)
    {
        try
        {
            // This would typically call a repository method to check code uniqueness
            // For now, we'll return true (implement based on your repository pattern)
            return await Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking reservation code uniqueness for code {Code}", code);
            return false;
        }
    }

    public async Task<bool> IsResourceAvailableAsync(Guid resourceId, DateTime startDate, DateTime endDate)
    {
        try
        {
            // This would typically call external resource service to check availability
            // For now, we'll return true (implement based on your resource service integration)
            return await Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking resource availability for resource {ResourceId}", resourceId);
            return false;
        }
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
}