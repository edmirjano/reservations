namespace Reservation.Exceptions;

public abstract class ReservationBaseException : Exception
{
    public string ErrorCode { get; }
    public object? ErrorData { get; }

    protected ReservationBaseException(string message, string errorCode, object? errorData = null) 
        : base(message)
    {
        ErrorCode = errorCode;
        ErrorData = errorData;
    }

    protected ReservationBaseException(string message, string errorCode, Exception innerException, object? errorData = null) 
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        ErrorData = errorData;
    }
}

public class ReservationNotFoundException : ReservationBaseException
{
    public ReservationNotFoundException(Guid id) 
        : base($"Reservation with ID {id} not found", "RESERVATION_NOT_FOUND", new { ReservationId = id })
    {
    }

    public ReservationNotFoundException(string code) 
        : base($"Reservation with code '{code}' not found", "RESERVATION_CODE_NOT_FOUND", new { ReservationCode = code })
    {
    }
}


public class StatusNotFoundException : ReservationBaseException
{
    public StatusNotFoundException(Guid id) 
        : base($"Status with ID {id} not found", "STATUS_NOT_FOUND", new { StatusId = id })
    {
    }

    public StatusNotFoundException(string name) 
        : base($"Status with name '{name}' not found", "STATUS_NAME_NOT_FOUND", new { StatusName = name })
    {
    }
}


public class InvalidReservationDataException : ReservationBaseException
{
    public InvalidReservationDataException(string message, object? validationErrors = null) 
        : base(message, "INVALID_RESERVATION_DATA", validationErrors)
    {
    }

    public InvalidReservationDataException(string message, Exception innerException, object? validationErrors = null) 
        : base(message, "INVALID_RESERVATION_DATA", innerException, validationErrors)
    {
    }
}


public class ReservationConflictException : ReservationBaseException
{
    public ReservationConflictException(string message, object? conflictData = null) 
        : base(message, "RESERVATION_CONFLICT", conflictData)
    {
    }

    public ReservationConflictException(string message, Exception innerException, object? conflictData = null) 
        : base(message, "RESERVATION_CONFLICT", innerException, conflictData)
    {
    }
}


public class ResourceUnavailableException : ReservationBaseException
{
    public ResourceUnavailableException(Guid resourceId, DateTime startDate, DateTime endDate) 
        : base(
            $"Resource {resourceId} is not available from {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}", 
            "RESOURCE_UNAVAILABLE", 
            new { ResourceId = resourceId, StartDate = startDate, EndDate = endDate })
    {
    }

    public ResourceUnavailableException(string resourceId, DateTime startDate, DateTime endDate, string reason) 
        : base(
            $"Resource {resourceId} is not available from {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}. Reason: {reason}", 
            "RESOURCE_UNAVAILABLE", 
            new { ResourceId = resourceId, StartDate = startDate, EndDate = endDate, Reason = reason })
    {
    }
}


public class InvalidReservationDateException : ReservationBaseException
{
    public InvalidReservationDateException(DateTime startDate, DateTime endDate, string reason) 
        : base(
            $"Invalid reservation dates: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}. {reason}", 
            "INVALID_RESERVATION_DATE", 
            new { StartDate = startDate, EndDate = endDate, Reason = reason })
    {
    }
}
public class PaymentProcessingException : ReservationBaseException
{
    public PaymentProcessingException(string message, object? paymentData = null) 
        : base(message, "PAYMENT_PROCESSING_FAILED", paymentData)
    {
    }

    public PaymentProcessingException(string message, Exception innerException, object? paymentData = null) 
        : base(message, "PAYMENT_PROCESSING_FAILED", innerException, paymentData)
    {
    }
}

public class InvalidStatusTransitionException : ReservationBaseException
{
    public InvalidStatusTransitionException(string currentStatus, string targetStatus) 
        : base(
            $"Cannot transition from status '{currentStatus}' to '{targetStatus}'", 
            "INVALID_STATUS_TRANSITION", 
            new { CurrentStatus = currentStatus, TargetStatus = targetStatus })
    {
    }
}

public class ReservationAuthorizationException : ReservationBaseException
{
    public ReservationAuthorizationException(string message, Guid? userId = null, Guid? organizationId = null) 
        : base(message, "RESERVATION_AUTHORIZATION_FAILED", new { UserId = userId, OrganizationId = organizationId })
    {
    }
}


public class OrganizationQuotaExceededException : ReservationBaseException
{
    public OrganizationQuotaExceededException(Guid organizationId, int currentCount, int maxAllowed) 
        : base(
            $"Organization {organizationId} has exceeded reservation quota. Current: {currentCount}, Max: {maxAllowed}", 
            "ORGANIZATION_QUOTA_EXCEEDED", 
            new { OrganizationId = organizationId, CurrentCount = currentCount, MaxAllowed = maxAllowed })
    {
    }
}

public class ReservationDetailNotFoundException : ReservationBaseException
{
    public ReservationDetailNotFoundException(Guid reservationId) 
        : base($"Reservation detail for reservation {reservationId} not found", "RESERVATION_DETAIL_NOT_FOUND", new { ReservationId = reservationId })
    {
    }
}

public class ExternalServiceException : ReservationBaseException
{
    public ExternalServiceException(string serviceName, string message, Exception? innerException = null) 
        : base($"External service '{serviceName}' error: {message}", "EXTERNAL_SERVICE_ERROR", innerException, new { ServiceName = serviceName })
    {
    }
}

public class ReservationDataException : ReservationBaseException
{
    public ReservationDataException(string message, Exception? innerException = null) 
        : base($"Database operation failed: {message}", "DATABASE_ERROR", innerException)
    {
    }
}


public class DuplicateReservationCodeException : ReservationBaseException
{
    public DuplicateReservationCodeException(string code) 
        : base($"Reservation with code '{code}' already exists", "DUPLICATE_RESERVATION_CODE", new { ReservationCode = code })
    {
    }
}