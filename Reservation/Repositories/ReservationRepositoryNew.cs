using System.Data;
using System.Reflection;
using Core.Constant.Enums;
using Core.Helpers;
using Core.Models;
using Core.Repositories;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Reservation.Data;
using Reservation.Interfaces;
using Reservation.Models;
using ReservationService;
using static Core.Constant.Enums.Enums;

namespace Reservation.Repositories;

public class ReservationRepositoryNew : GenericRepository<Models.Reservation, ReservationContext>, IReservationNew
{
    private readonly ReservationContext _context;
    private readonly PaymentService.PaymentService.PaymentServiceClient _paymentServiceClient;
    private readonly AuthService.AuthService.AuthServiceClient _authServiceClient;
    private readonly ResourceService.ResourceService.ResourceServiceClient _resourceServiceClient; 
    private readonly OrganizationService.OrganizationService.OrganizationServiceClient _organizationClient;
    
    public ReservationRepositoryNew(
        ReservationContext context,
        Func<IDbConnection> dbConnectionFactory,
        PaymentService.PaymentService.PaymentServiceClient paymentServiceClient,
        AuthService.AuthService.AuthServiceClient authServiceClient,
        ResourceService.ResourceService.ResourceServiceClient resourceServiceClient,
        OrganizationService.OrganizationService.OrganizationServiceClient organizationClient
    ) : base(context, dbConnectionFactory)
    {
        _context = context;
        _paymentServiceClient = paymentServiceClient;
        _authServiceClient = authServiceClient;
        _resourceServiceClient = resourceServiceClient;
        _organizationClient = organizationClient;
    }

    public async Task<ReservationDTO> CreateReservationAsync(CreateReservationDto createReservationDto)
    {
        var createdStatus = await AddOrCreateInitialReservationStatus();
        var reservation = new Models.Reservation
        {
            UserId = Guid.Parse(createReservationDto.UserId),
            OrganizationId = Guid.Parse(createReservationDto.OrganizationId),
            StatusId = createdStatus,
            Source = createReservationDto.Source,
            StartDate = DateTime.Parse(createReservationDto.StartDate).Date.ToUniversalTime(),
            EndDate = DateTime.Parse(createReservationDto.EndDate).Date.ToUniversalTime(),
        };

        var dbResources = await _resourceServiceClient.GetResourcesAsync(
            new ResourceService.GetResourcesRequest
            {
                OrganizationId = createReservationDto.OrganizationId,
            }
        );

        var getFullPricesRequest = new PaymentService.GetFullPricesRequest
        {
            Resources =
            {
                createReservationDto.Resources.Select(r => new PaymentService.ResourcePriceRequest
                {
                    Date = r.Date,
                    OriginalPrice = r.Price,
                }),
            },
        };

        reservation.Code = GenerateReservationCode();
        var resourcePrices = _paymentServiceClient.GetFullPrices(getFullPricesRequest);
        reservation.TotalAmount = resourcePrices.Prices.Sum(p => p.FullPrice);

        var createdReservation = await CreateAsync(reservation);

        var detail = new Models.Detail
        {
            Id = Guid.NewGuid(),
            ReservationId = createdReservation.Id,
            Name = createReservationDto.Detail.Name,
            Email = createReservationDto.Detail.Email,
            Phone = createReservationDto.Detail.Phone,
            NumberOfAdults = createReservationDto.Detail.NumberOfAdults,
            NumberOfChildren = createReservationDto.Detail.NumberOfChildren,
            NumberOfInfants = createReservationDto.Detail.NumberOfInfants,
            NumberOfPets = createReservationDto.Detail.NumberOfPets,
            ResourceQuantity = createReservationDto.Detail.ResourceQuantity,
            Note = createReservationDto.Detail.Note,
            OriginalPrice = createReservationDto.Detail.OriginalPrice,
            Discount = createReservationDto.Detail.Discount,
            Currency = createReservationDto.Detail.Currency,
            IsActive = true,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        await ExecuteAsync(
            @"INSERT INTO ""Details"" (""Id"", ""ReservationId"", ""Name"", ""Email"", ""Phone"", ""NumberOfAdults"", ""NumberOfChildren"", ""NumberOfInfants"", ""NumberOfPets"", ""ResourceQuantity"", ""Note"", ""OriginalPrice"", ""Discount"", ""Currency"", ""IsActive"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"")
               VALUES(@Id, @ReservationId, @Name, @Email, @Phone, @NumberOfAdults, @NumberOfChildren, @NumberOfInfants, @NumberOfPets, @ResourceQuantity, @Note, @OriginalPrice, @Discount, @Currency, @IsActive, @IsDeleted, @CreatedAt, @UpdatedAt)",
            detail
        );
        
        if (createReservationDto.Resources != null)
        {
            foreach (var resourceDto in createReservationDto.Resources)
            {
                await SaveReservationResource(createdReservation.Id, resourceDto);
            }
        }

        _ = BroadcastReservationChangeAsync(createdReservation);
        return await MapToDTO(createdReservation);
    }

    public async Task<ReservationDTO> CreateReservationForOrganizationAsync(CreateReservationForOrganizationDto createReservationDto)
    {
        if (!createReservationDto.Resources.Any())
            throw new Exception("At least 1 resource is required for making a reservation");

        string reservationCode = GenerateReservationCode();

        var reservation = new Models.Reservation
        {
            UserId = Guid.Parse(createReservationDto.UserId),
            OrganizationId = Guid.Parse(createReservationDto.OrganizationId),
            StatusId = Guid.Parse(createReservationDto.StatusId),
            StartDate = DateHelper.ParseDate(createReservationDto.StartDate),
            EndDate = DateHelper.ParseDate(createReservationDto.EndDate),
            Code = reservationCode,
            Source = "Organization",
            TotalAmount = createReservationDto.TotalAmount,
        };

        var createdReservation = await CreateAsync(reservation);

        var detail = new Models.Detail
        {
            Id = Guid.NewGuid(),
            ReservationId = createdReservation.Id,
            Name = !string.IsNullOrWhiteSpace(createReservationDto.CustomerName) ? createReservationDto.CustomerName : "",
            Email = !string.IsNullOrWhiteSpace(createReservationDto.Email) ? createReservationDto.Email : "",
            Phone = !string.IsNullOrWhiteSpace(createReservationDto.Phone) ? createReservationDto.Phone : "",
            NumberOfAdults = 0,
            NumberOfChildren = 0,
            NumberOfInfants = 0,
            NumberOfPets = 0,
            ResourceQuantity = createReservationDto.Resources?.Sum(r => r.Quantity) ?? 0,
            Note = createReservationDto.Notes,
            OriginalPrice = createdReservation.TotalAmount,
            Discount = 0,
            Currency = !string.IsNullOrWhiteSpace(createReservationDto.Currency) ? createReservationDto.Currency : "",
            IsActive = true,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        await ExecuteAsync(
            @"INSERT INTO ""Details"" (""Id"", ""ReservationId"", ""Name"", ""Email"", ""Phone"", ""NumberOfAdults"", ""NumberOfChildren"", ""NumberOfInfants"", ""NumberOfPets"", ""ResourceQuantity"", ""Note"", ""OriginalPrice"", ""Discount"", ""Currency"", ""IsActive"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"")
               VALUES(@Id, @ReservationId, @Name, @Email, @Phone, @NumberOfAdults, @NumberOfChildren, @NumberOfInfants, @NumberOfPets, @ResourceQuantity, @Note, @OriginalPrice, @Discount, @Currency, @IsActive, @IsDeleted, @CreatedAt, @UpdatedAt)",
            detail
        );

        if (createReservationDto.Resources != null)
        {
            foreach (var resourceDto in createReservationDto.Resources)
            {
                if (!String.IsNullOrEmpty(resourceDto.Slug) && String.IsNullOrEmpty(resourceDto.Id))
                {
                    var slugreq = new ResourceService.GetBySlugRequest { Slug = resourceDto.Slug };
                    var resourcebyslug = await _resourceServiceClient.GetResourceBySlugAsync(slugreq);
                    if (resourcebyslug != null)
                    {
                        resourceDto.Id = resourcebyslug.Id;
                    }
                    await SaveReservationResource(createdReservation.Id, resourceDto);
                }
                else
                {
                    await SaveReservationResource(createdReservation.Id, resourceDto);
                }
            }
        }

        _ = BroadcastReservationChangeAsync(createdReservation);
        return await MapToDTO(createdReservation);
    }

    public async Task<ReservationDTO> UpdateReservationAsync(ReservationDTO reservationDto)
    {
        var reservation = GetById(Guid.Parse(reservationDto.Id));
        reservation.UserId = Guid.Parse(reservationDto.UserId);
        reservation.OrganizationId = Guid.Parse(reservationDto.OrganizationId);
        reservation.StatusId = Guid.Parse(reservationDto.StatusId);
        reservation.TotalAmount = reservationDto.TotalAmount;
        reservation.Code = reservationDto.Code;
        reservation.StartDate = DateHelper.ParseDate(reservationDto.StartDate);
        reservation.EndDate = DateHelper.ParseDate(reservationDto.EndDate);
        reservation.Source = reservationDto.Source;

        var updatedReservation = await UpdateAsync(reservation);
        return await MapToDTO(updatedReservation);
    }

    public async Task<ReservationDTO> UpdateReservationForOrganizationAsync(ReservationDTO dto)
    {
        var reservation = await QueryFirstOrDefaultAsync<Models.Reservation>(
            @"SELECT * FROM ""Reservations"" WHERE ""Id"" = @Id AND ""IsDeleted"" = false",
            new { Id = Guid.Parse(dto.Id) });

        if (reservation == null)
            throw new Exception("Reservation not found");

        reservation.StartDate = DateHelper.ParseDate(dto.StartDate);
        reservation.EndDate = DateHelper.ParseDate(dto.EndDate);
        reservation.TotalAmount = dto.TotalAmount;
        reservation.UpdatedAt = DateTime.UtcNow;

        if (!String.IsNullOrEmpty(dto.StatusId))
        {
            reservation.StatusId = Guid.Parse(dto.StatusId);
        }

        await ExecuteAsync(
            @"UPDATE ""Reservations""
            SET ""StartDate"" = @StartDate, ""EndDate"" = @EndDate, ""TotalAmount"" = @TotalAmount, 
                ""StatusId"" = @StatusId, ""UpdatedAt"" = @UpdatedAt
            WHERE ""Id"" = @Id",
            new { reservation.Id, reservation.StartDate, reservation.EndDate, reservation.TotalAmount, reservation.StatusId, reservation.UpdatedAt });

        return await MapToDTO(reservation);
    }

    public async Task DeleteReservationAsync(Guid id)
    {
        await DeleteAsync(id);
    }

    public async Task<ReservationDTO> GetReservationByIdAsync(Guid id)
    {
        var reservation = await _context
            .Reservations.Where(r => r.Id == id && r.IsDeleted == false && r.IsActive == true)
            .Include(r => r.Status)
            .FirstOrDefaultAsync();

        if (reservation == null) return null;

        AuthService.IdOnly idOnly = new AuthService.IdOnly { Id = reservation.UserId.ToString() };
        var userProfile = await _authServiceClient.GetUserProfileByIdAsync(idOnly);

        var reservationDTO = await MapToDTO(reservation);
        reservationDTO.UserProfile = userProfile;
        return reservationDTO;
    }

    public async Task<ReservationDTO> ValidateTicketAsync(string code)
    {
        var reservation = _context.Reservations.FirstOrDefault(x => x.Code == code);
        if (reservation != null)
        {
            var statuses = await GetStatuses();
            var actualStatus = statuses?.FirstOrDefault(x => x.Id == reservation.StatusId.ToString())?.Name;
            if (actualStatus != "CheckedIn")
            {
                var checkedInStatusId = statuses.FirstOrDefault(x => x.Name == "CheckedIn")?.Id;
                if (checkedInStatusId != null)
                {
                    reservation.StatusId = Guid.Parse(checkedInStatusId);
                    await UpdateAsync(reservation);
                }
            }
            return await GetReservationByIdAsync(reservation.Id);
        }
        return new ReservationDTO();
    }

    // MISSING METHODS - CẦN THÊM TỪ INTERFACE IReservation
    public Task<ReservationDTO> GetReservationByResourceIdAsync(Guid resourceId)
    {
        throw new NotImplementedException("Cần implement method này");
    }

    public Task<IEnumerable<ReservationDTO>> GetReservationsAsync(GetReservationsRequest request)
    {
        throw new NotImplementedException("Cần implement method này");
    }

    public Task<GetReservationsByResourcesResponse> GetReservationsByResourcesAsync(GetReservationsByResourcesRequest request)
    {
        throw new NotImplementedException("Cần implement method này");
    }

    public Task<IEnumerable<DateReservationCountDTO>> GetReservationsCountPerDayAsync(string organizationId, DateTime startDate, DateTime endDate)
    {
        throw new NotImplementedException("Cần implement method này");
    }

    public Task<SearchClientsResponse> SearchClientsByNameAsync(string nameQuery, string organizationId, int maxResults)
    {
        throw new NotImplementedException("Cần implement method này");
    }

    public Task<GetReservationsBySourceCountResponse> GetReservationsBySourceCountAsync(string organizationId, DateTime startDate, DateTime endDate)
    {
        throw new NotImplementedException("Cần implement method này");
    }

    public Task<byte[]> GenerateReservationReportAsync(string organizationId, string startDate, string endDate)
    {
        throw new NotImplementedException("Cần implement method này");
    }

    // Helper methods
    private string GenerateReservationCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        string randomPart = new string(Enumerable.Repeat(chars, 5).Select(s => s[random.Next(s.Length)]).ToArray());
        return $"ORG-{randomPart}";
    }

    private async Task<Guid> AddOrCreateInitialReservationStatus()
    {
        var createdStatus = QueryAsync<Models.Status>(@"SELECT * FROM ""Statuses"" WHERE ""Name"" = 'Created' AND ""IsDeleted"" = false AND ""IsActive"" = true").Result.FirstOrDefault();
        if (createdStatus != null) return createdStatus.Id;

        var status = new Models.Status
        {
            Id = Guid.NewGuid(), 
            Name = "Created", 
            Description = "Reservation has been created by user, but it is not payed yet.",
            IsActive = true, 
            IsDeleted = false, 
            CreatedAt = DateTime.UtcNow, 
            UpdatedAt = DateTime.UtcNow
        };

        await ExecuteAsync(@"INSERT INTO ""Statuses"" (""Id"", ""Name"", ""Description"", ""IsActive"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"")
                       VALUES(@Id, @Name, @Description, @IsActive, @IsDeleted, @CreatedAt, @UpdatedAt)", status);
        return status.Id;
    }

    private async Task<IEnumerable<StatusDTO>> GetStatuses()
    {
        var query = @"SELECT ""Id""::text AS ""Id"", ""Name"", ""Description"" FROM ""Statuses"" WHERE ""IsDeleted"" = false AND ""IsActive"" = true";
        return await QueryAsync<StatusDTO>(query);
    }

    private async Task SaveReservationResource(Guid reservationId, ResourceItemDto resourceItem)
    {
        var resource = new ReservationResource
        {
            Id = Guid.NewGuid(),
            ReservationId = reservationId,
            ResourceId = Guid.Parse(resourceItem.Id),
            IsActive = true,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        await ExecuteAsync(
            @"INSERT INTO ""ReservationResources"" (""Id"", ""ReservationId"", ""ResourceId"", ""IsActive"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"")
               VALUES(@Id, @ReservationId, @ResourceId, @IsActive, @IsDeleted, @CreatedAt, @UpdatedAt)",
            resource
        );
    }

    private async Task BroadcastReservationChangeAsync(Models.Reservation reservation)
    {
        try
        {
            var organizationRequest = new OrganizationService.GetByIdRequest
            {
                Id = reservation.OrganizationId.ToString(),
            };
            var organization = await _organizationClient.GetOrganizationByIdAsync(organizationRequest);

            if (organization == null) return;

            var resources = await QueryAsync<ReservationResource>(
                @"SELECT * FROM ""ReservationResources""
                WHERE ""ReservationId"" = @ReservationId AND ""IsDeleted"" = false",
                new { ReservationId = reservation.Id }
            );

            if (!resources.Any()) return;

            var request = new ResourceService.GetResourcesByOrganizationSlugRequest
            {
                Slug = organization.Slug,
                Date = reservation.StartDate.ToString("yyyy-MM-dd"),
            };

            await _resourceServiceClient.GetResourcesByOrganizationSlugAsync(request);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error broadcasting reservation change: {ex.Message}");
        }
    }

    private async Task<ReservationDTO> MapToDTO(
        Models.Reservation reservation,
        bool withResources = false,
        bool withOrganizations = false,
        OrganizationService.OrganizationDTO organization = null,
        Dictionary<Guid, List<ReservationResource>> reservationResourceMap = null,
        Dictionary<string, ResourceService.ResourceDTO> resourceDict = null
    )
    {
        var detail = await QueryFirstOrDefaultAsync<Models.Detail>(
            @"SELECT * FROM ""Details"" WHERE ""ReservationId"" = @ReservationId",
            new { ReservationId = reservation.Id }
        );

        var status = await QueryFirstOrDefaultAsync<Status>(
            @"SELECT * FROM ""Statuses"" WHERE ""Id"" = @StatusId AND ""IsDeleted"" = false AND ""IsActive"" = true",
            new { StatusId = reservation.StatusId }
        );

        List<ReservationResource> resources = null;
        if (reservationResourceMap != null && reservationResourceMap.TryGetValue(reservation.Id, out var rrList))
        {
            resources = rrList;
        }
        else
        {
            resources = (await QueryAsync<ReservationResource>(
                @"SELECT * FROM ""ReservationResources"" WHERE ""ReservationId"" = @ReservationId AND ""IsDeleted"" = false",
                new { ReservationId = reservation.Id }
            )).ToList();
        }

        Type type = typeof(Enums.ReservationStatusColor);
        var statusName = status?.Name;

        var color = statusName != null
            ? typeof(Enums.ReservationStatusColor)
                .GetFields(BindingFlags.Static | BindingFlags.Public)
                .FirstOrDefault(p => p.Name == statusName)
            : null;

        var reservationDto = new ReservationDTO
        {
            Id = reservation.Id.ToString(),
            UserId = reservation.UserId.ToString(),
            OrganizationId = reservation.OrganizationId.ToString(),
            StatusId = reservation.StatusId.ToString(),
            TotalAmount = reservation.TotalAmount,
            Code = reservation.Code,
            StartDate = reservation.StartDate.ToString(),
            EndDate = reservation.EndDate.ToString(),
            Source = reservation.Source,
            Detail = detail != null ? new DetailDTO
            {
                Name = detail.Name,
                Email = detail.Email,
                Phone = detail.Phone,
                NumberOfAdults = detail.NumberOfAdults,
                NumberOfChildren = detail.NumberOfChildren,
                NumberOfInfants = detail.NumberOfInfants,
                NumberOfPets = detail.NumberOfPets,
                ResourceQuantity = detail.ResourceQuantity,
                Note = detail.Note,
                OriginalPrice = detail.OriginalPrice,
                Discount = detail.Discount,
                Currency = detail.Currency,
            } : null,
            ResourceIds = { resources.Select(r => r.ResourceId.ToString()) },
            Color = color != null ? color.GetValue(null).ToString() : string.Empty,
        };

        if (withResources && resources.Any() && resourceDict != null)
        {
            foreach (var rr in resources)
            {
                if (resourceDict.TryGetValue(rr.ResourceId.ToString(), out var resource))
                {
                    var resourceItemDto = new ResourceItemDto
                    {
                        Id = resource.Id,
                        Price = resource.Price,
                        Slug = resource.Slug,
                        Number = resource.Number,
                    };
                    reservationDto.Resources.Add(resourceItemDto);
                }
            }
        }
        else if (withResources && resources.Any())
        {
            var resourceIds = resources.Select(r => r.ResourceId.ToString()).ToList();
            var dbResources = await _resourceServiceClient.GetResourcesAsync(
                new ResourceService.GetResourcesRequest
                {
                    OrganizationId = reservation.OrganizationId.ToString(),
                    ResourceIds = { resourceIds },
                }
            );

            foreach (var resource in dbResources.Resources)
            {
                var resourceItemDto = new ResourceItemDto
                {
                    Id = resource.Id,
                    Price = resource.Price,
                    Slug = resource.Slug,
                    Number = resource.Number,
                };
                reservationDto.Resources.Add(resourceItemDto);
            }
        }

        if (withOrganizations)
        {
            if (organization != null)
            {
                reservationDto.Organization = organization;
            }
            else
            {
                var organizationRequest = new OrganizationService.GetByIdRequest
                {
                    Id = reservation.OrganizationId.ToString(),
                };
                var org = await _organizationClient.GetOrganizationByIdAsync(organizationRequest);
                if (org != null)
                {
                    reservationDto.Organization = org;
                }
            }
        }

        return reservationDto;
    }
}