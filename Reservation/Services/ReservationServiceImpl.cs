using AuthService;
using Core.Constant;
using Core.Interfaces;
using EmailService;
using Grpc.Core;
using MediaService;
using Microsoft.Extensions.Caching.Memory;
using Reservation.Interfaces;
using Reservation.Models;
using ReservationService;
using DetailDTO = ReservationService.DetailDTO;
using Empty = ReservationService.Empty;
using GetByIdRequest = ReservationService.GetByIdRequest;
using GetStatusesRequest = ReservationService.GetStatusesRequest;
using ReservationDTO = ReservationService.ReservationDTO;
using Status = Grpc.Core.Status;
using StatusDTO = ReservationService.StatusDTO;
using StatusDTOList = ReservationService.StatusDTOList;

namespace Reservation.Services;

public class ReservationServiceImpl : ReservationService.ReservationService.ReservationServiceBase
{
    private readonly IReservationNew _reservationRepository; // CRUD cơ bản
    private readonly IReservationQuery _reservationQueryRepository; // Query phức tạp  
    private readonly IReservationAnalytics _reservationAnalyticsRepository; // Analytics
    private readonly IReservationReport _reservationReportRepository; // Reports
    private readonly IStatus _statusRepository;
    private readonly IDetail _detailRepository;
    private readonly ICacheService _cacheService;
    private readonly AuthService.AuthService.AuthServiceClient _authServiceClient;
    private readonly OrganizationService.OrganizationService.OrganizationServiceClient _organizationServiceClient;
    private readonly EmailService.EmailService.EmailServiceClient _emailServiceClient;
    private readonly PaymentService.PaymentService.PaymentServiceClient _paymentServiceClient;

    public ReservationServiceImpl(
        IReservationNew reservationRepository, 
        IReservationQuery reservationQueryRepository, 
        IReservationAnalytics reservationAnalyticsRepository, 
        IReservationReport reservationReportRepository, 
        IStatus statusRepository,
        IDetail detailRepository,
        ICacheService cacheService,
        AuthService.AuthService.AuthServiceClient authServiceClient,
        OrganizationService.OrganizationService.OrganizationServiceClient organizationServiceClient,
        EmailService.EmailService.EmailServiceClient emailServiceClient,
        PaymentService.PaymentService.PaymentServiceClient paymentServiceClient)
    {
        _reservationRepository = reservationRepository;
        _reservationQueryRepository = reservationQueryRepository;
        _reservationAnalyticsRepository = reservationAnalyticsRepository;
        _reservationReportRepository = reservationReportRepository;
        _statusRepository = statusRepository;
        _detailRepository = detailRepository;
        _cacheService = cacheService;
        _authServiceClient = authServiceClient;
        _organizationServiceClient = organizationServiceClient;
        _emailServiceClient = emailServiceClient;
        _paymentServiceClient = paymentServiceClient;
    }

    // ===== CRUD METHODS - Sử dụng _reservationRepository =====
    public override async Task<ReservationDTO> CreateReservation(
        CreateReservationDto request,
        ServerCallContext context)
    {
        try
        {
            var newReservation = await _reservationRepository.CreateReservationAsync(request);
            if (newReservation == null)
            {
                throw new RpcException(new Status(StatusCode.Internal, "Failed to create reservation."));
            }
            return newReservation;
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    public override async Task<ReservationDTO> CreateReservationForOrganization(
        CreateReservationForOrganizationDto request,
        ServerCallContext context)
    {
        try
        {
            var newReservation = await _reservationRepository.CreateReservationForOrganizationAsync(request);

            if (newReservation != null && !string.IsNullOrEmpty(newReservation.Id))
            {
                try
                {
                    var transactionDto = new PaymentService.TransactionDTO
                    {
                        OrganizationId = request.OrganizationId,
                        UserId = request.UserId,
                        Currency = !string.IsNullOrWhiteSpace(request.Currency) ? request.Currency : "EUR",
                        Status = "Success",
                        Description = $"Reservation {newReservation.Code} created by organization",
                        ReferenceId = newReservation.Id,
                        ReferenceType = "ReservationForOrganization",
                        TotalValue = newReservation.TotalAmount,
                        ResourceGrossValue = newReservation.TotalAmount,
                    };

                    await _paymentServiceClient.CreateTransactionAsync(transactionDto);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Transaction Warning] Failed to create transaction for reservation {newReservation.Id}: {ex.Message}");
                }
            }

            return newReservation;
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    public override async Task<ReservationDTO> UpdateReservationForOrganization(
        ReservationDTO request,
        ServerCallContext context)
    {
        try
        {
            var updatedReservation = await _reservationRepository.UpdateReservationForOrganizationAsync(request);
            return updatedReservation;
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, $"Update failed: {ex.Message}"));
        }
    }

    public override async Task<ReservationDTO> UpdateReservation(
        ReservationDTO request,
        ServerCallContext context)
    {
        try
        {
            var updatedReservation = await _reservationRepository.UpdateReservationAsync(request);
            return updatedReservation;
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    public override async Task<ReservationService.Empty> DeleteReservation(
        ReservationDTO request,
        ServerCallContext context)
    {
        try
        {
            await _reservationRepository.DeleteReservationAsync(Guid.Parse(request.Id));
            var cacheKey = $"{RedisConstant.Reservation}.Reservation.{request.Id}";
            await _cacheService.RemoveAsync(cacheKey);
            return new ReservationService.Empty();
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    public override async Task<ReservationDTO> GetReservationById(
        GetByIdRequest request,
        ServerCallContext context)
    {
        try
        {
            var cacheKey = $"{RedisConstant.Reservation}.Reservation.{request.Id}";
            return await _cacheService.GetOrCreateAsync(
                cacheKey,
                async () =>
                {
                    return await _reservationRepository.GetReservationByIdAsync(Guid.Parse(request.Id));
                },
                RedisConstant.CacheTimeToLiveDefaultValue
            );
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    public override async Task<ReservationDTO> ValidateTicket(
        GetByCodeRequest request,
        ServerCallContext context)
    {
        try
        {
            var cacheKey = $"{RedisConstant.Reservation}.Ticket.{request.Code}";
            return await _cacheService.GetOrCreateAsync(
                cacheKey,
                async () =>
                {
                    return await _reservationRepository.ValidateTicketAsync(request.Code);
                },
                RedisConstant.CacheTimeToLiveDefaultValue
            );
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    // ===== QUERY METHODS - Sử dụng _reservationQueryRepository =====
    public override async Task<ReservationDTOList> GetReservations(
        GetReservationsRequest request,
        ServerCallContext context)
    {
        try
        {
            var reservations = await _reservationQueryRepository.GetReservationsAsync(request);
            return new ReservationDTOList { Reservations = { reservations } };
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    public override async Task<GetReservationsByResourcesResponse> GetReservationsByResources(
        GetReservationsByResourcesRequest request,
        ServerCallContext context)
    {
        try
        {
            var reservations = await _reservationQueryRepository.GetReservationsByResourcesAsync(request);
            return reservations;
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    public override async Task<SearchClientsResponse> SearchClientsByName(
        SearchClientsRequest request,
        ServerCallContext context)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.NameQuery))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Name query cannot be empty"));
            }

            if (string.IsNullOrWhiteSpace(request.OrganizationId))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Organization ID cannot be empty"));
            }

            var maxResults = request.MaxResults > 0 ? request.MaxResults : 10;
            return await _reservationQueryRepository.SearchClientsByNameAsync(
                request.NameQuery,
                request.OrganizationId,
                maxResults
            );
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, $"An error occurred while searching for clients: {ex.Message}"));
        }
    }

    // ===== ANALYTICS METHODS - Sử dụng _reservationAnalyticsRepository =====
    public override async Task<DateReservationCountList> GetReservationsCountPerDay(
        DateRangeRequest request,
        ServerCallContext context)
    {
        try
        {
            var startDate = DateTime.Parse(request.StartDate).ToUniversalTime();
            var endDate = DateTime.Parse(request.EndDate).ToUniversalTime();

            var cacheKey = $"{RedisConstant.Reservation}.CountPerDay.{request.OrganizationId}.{startDate:yyyyMMdd}.{endDate:yyyyMMdd}";
            return await _cacheService.GetOrCreateAsync(
                cacheKey,
                async () =>
                {
                    var dateReservationCounts = await _reservationAnalyticsRepository.GetReservationsCountPerDayAsync(
                        request.OrganizationId,
                        startDate,
                        endDate
                    );

                    return new DateReservationCountList
                    {
                        DateReservationCounts = { dateReservationCounts },
                    };
                },
                RedisConstant.CacheTimeToLiveOneHour
            );
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    public override async Task<GetReservationsBySourceCountResponse> GetReservationsBySourceCount(
        GetReservationsBySourceCountRequest request,
        ServerCallContext context)
    {
        try
        {
            var startDate = DateTime.Parse(request.StartDate).ToUniversalTime();
            var endDate = DateTime.Parse(request.EndDate).ToUniversalTime();

            var cacheKey = $"{RedisConstant.Reservation}.SourceCount.{request.OrganizationId}.{startDate:yyyyMMdd}.{endDate:yyyyMMdd}";
            return await _cacheService.GetOrCreateAsync(
                cacheKey,
                async () =>
                {
                    return await _reservationAnalyticsRepository.GetReservationsBySourceCountAsync(
                        request.OrganizationId,
                        startDate,
                        endDate
                    );
                },
                RedisConstant.CacheTimeToLiveOneHour
            );
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    public override async Task<GetStatsResponse> GetStats(
        GetStatsRequest request,
        ServerCallContext context)
    {
        try
        {
            var startDate = DateTime.Parse(request.StartDate).ToUniversalTime();
            var endDate = DateTime.Parse(request.EndDate).ToUniversalTime();

            var reservationRequest = new GetReservationsRequest
            {
                OrganizationId = request.OrganizationId,
                StartDate = startDate.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                EndDate = endDate.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                Page = 1,
                PerPage = 10000
            };

            // SỬ DỤNG Query Repository cho việc lấy danh sách
            var reservations = await _reservationQueryRepository.GetReservationsAsync(reservationRequest);

            var totalReservations = reservations.Count();
            var totalEarnings = reservations.Sum(reservation => reservation.TotalAmount);
            var totalDays = (endDate - startDate).Days;
            var averageDailyEarnings = totalDays > 0 ? totalEarnings / totalDays : 0;

            return new GetStatsResponse
            {
                TotalReservations = totalReservations,
                TotalEarnings = totalEarnings,
                AverageDailyEarnings = averageDailyEarnings,
            };
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    // ===== REPORT METHODS - Sử dụng _reservationReportRepository =====
    public override async Task<GenerateReservationReportResponse> GenerateReservationReport(
        GenerateReservationReportRequest request,
        ServerCallContext context)
    {
        try
        {
            var fileContent = await _reservationReportRepository.GenerateReservationReportAsync(
                request.OrganizationId,
                request.StartDate,
                request.EndDate
            );
            var fileName = $"reservations-{request.StartDate}_to_{request.EndDate}.xlsx";
            return new GenerateReservationReportResponse
            {
                FileContent = Google.Protobuf.ByteString.CopyFrom(fileContent),
                FileName = fileName,
            };
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    // ===== DETAIL METHODS - Sử dụng _detailRepository (không đổi) =====
    public override async Task<DetailDTO> GetReservationDetailById(
        GetByIdRequest request,
        ServerCallContext context)
    {
        try
        {
            var cacheKey = $"{RedisConstant.Reservation}.Detail.{request.Id}";
            return await _cacheService.GetOrCreateAsync(
                cacheKey,
                async () =>
                {
                    var detail = await _detailRepository.GetDetailByReservationIdAsync(Guid.Parse(request.Id));
                    if (detail == null)
                    {
                        throw new RpcException(new Status(StatusCode.NotFound, "Detail not found"));
                    }
                    return detail;
                },
                RedisConstant.CacheTimeToLiveDefaultValue
            );
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    // ===== STATUS METHODS - Sử dụng _statusRepository (không đổi) =====
    public override async Task<StatusDTO> CreateStatus(StatusDTO request, ServerCallContext context)
    {
        try
        {
            var newStatus = await _statusRepository.CreateStatusAsync(request);
            return newStatus;
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    public override async Task<StatusDTO> UpdateStatus(StatusDTO request, ServerCallContext context)
    {
        try
        {
            var updatedStatus = await _statusRepository.UpdateStatusAsync(request);
            return updatedStatus;
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    public override async Task<ReservationService.Empty> DeleteStatus(StatusDTO request, ServerCallContext context)
    {
        try
        {
            await _statusRepository.DeleteStatusAsync(Guid.Parse(request.Id));
            var cacheKey = $"{RedisConstant.Reservation}.Status.{request.Id}";
            await _cacheService.RemoveAsync(cacheKey);
            return new ReservationService.Empty();
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    public override async Task<StatusDTO> GetStatusById(GetByIdRequest request, ServerCallContext context)
    {
        try
        {
            var cacheKey = $"{RedisConstant.Reservation}.Status.{request.Id}";
            return await _cacheService.GetOrCreateAsync(
                cacheKey,
                async () =>
                {
                    return await _statusRepository.GetStatusByIdAsync(Guid.Parse(request.Id));
                },
                RedisConstant.CacheTimeToLiveDefaultValue
            );
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    public override async Task<StatusDTOList> GetStatuses(GetStatusesRequest request, ServerCallContext context)
    {
        try
        {
            var statuses = await _statusRepository.GetStatusesAsync(request);
            return new StatusDTOList { Statuses = { statuses } };
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    // ===== HELPER METHODS =====
    private async Task<StatusDTO> GetOrCreateCreatedStatusAsync()
    {
        var statuses = await _statusRepository.GetStatusesAsync(new GetStatusesRequest { Keyword = "Created" });
        var createdStatus = statuses.FirstOrDefault(s => s.Name == "Created");

        if (createdStatus != null)
            return createdStatus;

        return await _statusRepository.CreateStatusAsync(new StatusDTO
        {
            Name = "Created",
            Description = "Initial status for a newly created reservation",
        });
    }

    private async Task SendReservationEmailsAsync(CreateReservationDto request)
    {
        try
        {
            var userProfile = await _authServiceClient.GetUserProfileByIdAsync(new IdOnly { Id = request.UserId });
            var userName = userProfile.Username;

            var organization = await _organizationServiceClient.GetOrganizationByIdAsync(
                new OrganizationService.GetByIdRequest { Id = request.OrganizationId.ToString() });
            var organizationName = organization.Name;

            var emailDtoToUser = new EmailDto
            {
                To = userProfile.Email,
                Subject = "Your Reservation Confirmation",
            };

            await _emailServiceClient.SendEmailWithTemplateAsync(new SendEmailWithTemplateRequest
            {
                TemplateName = EmailTemplateConstants.ReservationConfirmationUser,
                Email = emailDtoToUser,
                Placeholders = { { "UserName", userName }, { "OrganizationName", organizationName } },
            });

            var emailDtoToOrg = new EmailDto
            {
                To = organization.Details.Email,
                Subject = "A new reservation has been made",
            };

            await _emailServiceClient.SendEmailWithTemplateAsync(new SendEmailWithTemplateRequest
            {
                TemplateName = EmailTemplateConstants.ReservationConfirmationOrganization,
                Email = emailDtoToOrg,
                Placeholders = { { "OrganizationName", organizationName } },
            });
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, $"Error sending reservation emails: {ex.Message}"));
        }
    }
}