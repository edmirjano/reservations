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

public class ReservationServiceImpl(
    IReservation reservationRepository,
    IStatus statusRepository,
    IDetail detailRepository,
    ICacheService cacheService,
    AuthService.AuthService.AuthServiceClient authServiceClient,
    OrganizationService.OrganizationService.OrganizationServiceClient organizationServiceClient,
    EmailService.EmailService.EmailServiceClient emailServiceClient,
    PaymentService.PaymentService.PaymentServiceClient paymentServiceClient
) : ReservationService.ReservationService.ReservationServiceBase
{
    public override async Task<ReservationDTO> CreateReservation(
        CreateReservationDto request,
        ServerCallContext context
    )
    {
        try
        {
            var newReservation = await reservationRepository.CreateReservationAsync(request);

            // await SendReservationEmailsAsync(request);
            if (newReservation == null)
            {
                throw new RpcException(
                    new Status(StatusCode.Internal, "Failed to create reservation.")
                );
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
        ServerCallContext context
    )
    {
        try
        {
            var newReservation =
                await reservationRepository.CreateReservationForOrganizationAsync(request);

            if (newReservation != null && !string.IsNullOrEmpty(newReservation.Id))
            {
                try
                {
                    var transactionDto = new PaymentService.TransactionDTO
                    {
                        OrganizationId = request.OrganizationId,
                        UserId = request.UserId,
                        Currency = !string.IsNullOrWhiteSpace(request.Currency)
                            ? request.Currency
                            : "EUR",
                        Status = "Success",
                        Description =
                            $"Reservation {newReservation.Code} created by organization",
                        ReferenceId = newReservation.Id,
                        ReferenceType = "ReservationForOrganization",
                        TotalValue = newReservation.TotalAmount,
                        ResourceGrossValue = newReservation.TotalAmount,
                    };

                    await paymentServiceClient.CreateTransactionAsync(transactionDto);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"[Transaction Warning] Failed to create transaction for reservation {newReservation.Id}: {ex.Message}"
                    );
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
        ServerCallContext context
    )
    {
        try
        {
            var updatedReservation =
                await reservationRepository.UpdateReservationForOrganizationAsync(request);

            return updatedReservation;
        }
        catch (Exception ex)
        {
            throw new RpcException(
                new Status(StatusCode.Internal, $"Update failed: {ex.Message}")
            );
        }
    }

    public override async Task<ReservationDTO> UpdateReservation(
        ReservationDTO request,
        ServerCallContext context
    )
    {
        try
        {
            var updatedReservation = await reservationRepository.UpdateReservationAsync(
                request
            );

            return updatedReservation;
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    public override async Task<ReservationService.Empty> DeleteReservation(
        ReservationDTO request,
        ServerCallContext context
    )
    {
        try
        {
            await reservationRepository.DeleteReservationAsync(Guid.Parse(request.Id));
            // Remove from cache
            var cacheKey = $"{RedisConstant.Reservation}.Reservation.{request.Id}";
            await cacheService.RemoveAsync(cacheKey);
            return new ReservationService.Empty();
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    public override async Task<ReservationDTO> GetReservationById(
        GetByIdRequest request,
        ServerCallContext context
    )
    {
        try
        {
            var cacheKey = $"{RedisConstant.Reservation}.Reservation.{request.Id}";
            var reservation = await reservationRepository.GetReservationByIdAsync(
                Guid.Parse(request.Id)
            );
            return await cacheService.GetOrCreateAsync(
                cacheKey,
                async () =>
                {
                    return await reservationRepository.GetReservationByIdAsync(
                        Guid.Parse(request.Id)
                    );
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
        ServerCallContext context
    )
    {
        try
        {
            var cacheKey = $"{RedisConstant.Reservation}.Ticket.{request.Code}";
            return await cacheService.GetOrCreateAsync(
                cacheKey,
                async () =>
                {
                    return await reservationRepository.ValidateTicketAsync(request.Code);
                },
                RedisConstant.CacheTimeToLiveDefaultValue
            );
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    public override async Task<ReservationDTOList> GetReservations(
        GetReservationsRequest request,
        ServerCallContext context
    )
    {
        try
        {
            var reservations = await reservationRepository.GetReservationsAsync(request);
            return new ReservationDTOList { Reservations = { reservations } };
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    public override async Task<GetReservationsByResourcesResponse> GetReservationsByResources(
        GetReservationsByResourcesRequest request,
        ServerCallContext context
    )
    {
        try
        {
            var reservations = await reservationRepository.GetReservationsByResourcesAsync(
                request
            );
            return reservations;
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    public override async Task<DateReservationCountList> GetReservationsCountPerDay(
        DateRangeRequest request,
        ServerCallContext context
    )
    {
        try
        {
            // Parse the start and end dates
            var startDate = DateTime.Parse(request.StartDate).ToUniversalTime();
            var endDate = DateTime.Parse(request.EndDate).ToUniversalTime();

            var cacheKey =
                $"{RedisConstant.Reservation}.CountPerDay.{request.OrganizationId}.{startDate:yyyyMMdd}.{endDate:yyyyMMdd}";
            return await cacheService.GetOrCreateAsync(
                cacheKey,
                async () =>
                {
                    // Call the repository method to get the reservation counts per day
                    var dateReservationCounts =
                        await reservationRepository.GetReservationsCountPerDayAsync(
                            request.OrganizationId,
                            startDate,
                            endDate
                        );

                    // Map the result to a DateReservationCountList
                    return new DateReservationCountList
                    {
                        DateReservationCounts = { dateReservationCounts },
                    };
                },
                RedisConstant.CacheTimeToLiveOneHour
            ); // Using shorter cache time for stats
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    public override async Task<DetailDTO> GetReservationDetailById(
        GetByIdRequest request,
        ServerCallContext context
    )
    {
        try
        {
            var cacheKey = $"{RedisConstant.Reservation}.Detail.{request.Id}";
            return await cacheService.GetOrCreateAsync(
                cacheKey,
                async () =>
                {
                    var detail = await detailRepository.GetDetailByReservationIdAsync(
                        Guid.Parse(request.Id)
                    );
                    if (detail == null)
                    {
                        throw new RpcException(
                            new Status(StatusCode.NotFound, "Detail not found")
                        );
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

    public override async Task<StatusDTO> CreateStatus(
        StatusDTO request,
        ServerCallContext context
    )
    {
        try
        {
            var newStatus = await statusRepository.CreateStatusAsync(request);
            return newStatus;
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    public override async Task<StatusDTO> UpdateStatus(
        StatusDTO request,
        ServerCallContext context
    )
    {
        try
        {
            var updatedStatus = await statusRepository.UpdateStatusAsync(request);
            return updatedStatus;
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    public override async Task<ReservationService.Empty> DeleteStatus(
        StatusDTO request,
        ServerCallContext context
    )
    {
        try
        {
            await statusRepository.DeleteStatusAsync(Guid.Parse(request.Id));
            // Remove from cache
            var cacheKey = $"{RedisConstant.Reservation}.Status.{request.Id}";
            await cacheService.RemoveAsync(cacheKey);
            return new ReservationService.Empty();
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    public override async Task<StatusDTO> GetStatusById(
        GetByIdRequest request,
        ServerCallContext context
    )
    {
        try
        {
            var cacheKey = $"{RedisConstant.Reservation}.Status.{request.Id}";
            return await cacheService.GetOrCreateAsync(
                cacheKey,
                async () =>
                {
                    return await statusRepository.GetStatusByIdAsync(Guid.Parse(request.Id));
                },
                RedisConstant.CacheTimeToLiveDefaultValue
            );
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    public override async Task<StatusDTOList> GetStatuses(
        GetStatusesRequest request,
        ServerCallContext context
    )
    {
        try
        {
            var statuses = await statusRepository.GetStatusesAsync(request);
            return new StatusDTOList { Statuses = { statuses } };
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    public override async Task<GetStatsResponse> GetStats(
        GetStatsRequest request,
        ServerCallContext context
    )
    {
        try
        {
            // Convert StartDate and EndDate from the request to UTC
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

            var reservations = await reservationRepository.GetReservationsAsync(
                reservationRequest
            );

            // Calculate total reservations
            var totalReservations = reservations.Count();

            // Calculate total earnings
            var totalEarnings = reservations.Sum(reservation =>
                reservation.TotalAmount
            );

            // Calculate average daily earnings
            var totalDays = (endDate - startDate).Days;
            var averageDailyEarnings = totalDays > 0 ? totalEarnings / totalDays : 0;

            // Prepare the response
            return new GetStatsResponse
            {
                TotalReservations = totalReservations,
                TotalEarnings = totalEarnings,
                AverageDailyEarnings = averageDailyEarnings,
            };
            // var cacheKey =
            //     $"{RedisConstant.Reservation}.Stats.{request.OrganizationId}.{startDate:yyyyMMdd}.{endDate:yyyyMMdd}";
            // return await cacheService.GetOrCreateAsync(
            //     cacheKey,
            //     async () =>
            //     {
            //         // Get the reservations in the specified date range and for the given organization

            //     },
            //     RedisConstant.CacheTimeToLiveOneHour
            // ); // Using shorter cache time for stats
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    private async Task<StatusDTO> GetOrCreateCreatedStatusAsync()
    {
        // Look for "Created" status
        var statuses = await statusRepository.GetStatusesAsync(
            new GetStatusesRequest { Keyword = "Created" }
        );

        var createdStatus = statuses.FirstOrDefault(s => s.Name == "Created");

        if (createdStatus != null)
            return createdStatus;

        // Create the status if it doesn't exist
        return await statusRepository.CreateStatusAsync(
            new StatusDTO
            {
                Name = "Created",
                Description = "Initial status for a newly created reservation",
            }
        );
    }

    private async Task SendReservationEmailsAsync(CreateReservationDto request)
    {
        try
        {
            // Fetch user profile using the User ID
            var userProfile = await authServiceClient.GetUserProfileByIdAsync(
                new IdOnly { Id = request.UserId }
            );
            var userName = userProfile.Username;

            // Fetch organization name using the Organization ID
            var organization = await organizationServiceClient.GetOrganizationByIdAsync(
                new OrganizationService.GetByIdRequest
                {
                    Id = request.OrganizationId.ToString(),
                }
            );
            var organizationName = organization.Name;

            // Send email to the user confirming the reservation
            var emailDtoToUser = new EmailDto
            {
                To = userProfile.Email,
                Subject = "Your Reservation Confirmation",
            };

            await emailServiceClient.SendEmailWithTemplateAsync(
                new SendEmailWithTemplateRequest
                {
                    TemplateName = EmailTemplateConstants.ReservationConfirmationUser,
                    Email = emailDtoToUser,
                    Placeholders =
                    {
                        { "UserName", userName },
                        { "OrganizationName", organizationName },
                    },
                }
            );

            // Send email to the organization notifying them of the booking
            var emailDtoToOrg = new EmailDto
            {
                To = organization.Details.Email,
                Subject = "A new reservation has been made",
            };

            await emailServiceClient.SendEmailWithTemplateAsync(
                new SendEmailWithTemplateRequest
                {
                    TemplateName = EmailTemplateConstants.ReservationConfirmationOrganization,
                    Email = emailDtoToOrg,
                    Placeholders = { { "OrganizationName", organizationName } },
                }
            );
        }
        catch (Exception ex)
        {
            throw new RpcException(
                new Status(
                    StatusCode.Internal,
                    $"Error sending reservation emails: {ex.Message}"
                )
            );
        }
    }

    public override async Task<SearchClientsResponse> SearchClientsByName(
        SearchClientsRequest request,
        ServerCallContext context
    )
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.NameQuery))
            {
                throw new RpcException(
                    new Status(StatusCode.InvalidArgument, "Name query cannot be empty")
                );
            }

            if (string.IsNullOrWhiteSpace(request.OrganizationId))
            {
                throw new RpcException(
                    new Status(StatusCode.InvalidArgument, "Organization ID cannot be empty")
                );
            }

            var maxResults = request.MaxResults > 0 ? request.MaxResults : 10;

            return await reservationRepository.SearchClientsByNameAsync(
                request.NameQuery,
                request.OrganizationId,
                maxResults
            );
        }
        catch (Exception ex)
        {
            throw new RpcException(
                new Status(
                    StatusCode.Internal,
                    $"An error occurred while searching for clients: {ex.Message}"
                )
            );
        }
    }

    public override async Task<GetReservationsBySourceCountResponse> GetReservationsBySourceCount(
        GetReservationsBySourceCountRequest request,
        ServerCallContext context
    )
    {
        try
        {
            // Convert StartDate and EndDate from the request to UTC
            var startDate = DateTime.Parse(request.StartDate).ToUniversalTime();
            var endDate = DateTime.Parse(request.EndDate).ToUniversalTime();

            var cacheKey =
                $"{RedisConstant.Reservation}.SourceCount.{request.OrganizationId}.{startDate:yyyyMMdd}.{endDate:yyyyMMdd}";
            return await cacheService.GetOrCreateAsync(
                cacheKey,
                async () =>
                {
                    return await reservationRepository.GetReservationsBySourceCountAsync(
                        request.OrganizationId,
                        startDate,
                        endDate
                    );
                },
                RedisConstant.CacheTimeToLiveOneHour
            ); // Using shorter cache time for stats
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    public override async Task<GenerateReservationReportResponse> GenerateReservationReport(
        GenerateReservationReportRequest request,
        ServerCallContext context
    )
    {
        try
        {
            var fileContent = await reservationRepository.GenerateReservationReportAsync(
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
}