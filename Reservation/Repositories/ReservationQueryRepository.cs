using System.Data;
using Core.Models;
using Core.Repositories;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Reservation.Data;
using Reservation.Interfaces;
using Reservation.Models;
using ReservationService;

namespace Reservation.Repositories;

public class ReservationQueryRepository : GenericRepository<Models.Reservation, ReservationContext>, IReservationQuery
{
    private readonly ReservationContext _context;
    private readonly AuthService.AuthService.AuthServiceClient _authServiceClient;
    private readonly ResourceService.ResourceService.ResourceServiceClient _resourceServiceClient;
    private readonly OrganizationService.OrganizationService.OrganizationServiceClient _organizationClient;

    public ReservationQueryRepository(
        ReservationContext context,
        Func<IDbConnection> dbConnectionFactory,
        AuthService.AuthService.AuthServiceClient authServiceClient,
        ResourceService.ResourceService.ResourceServiceClient resourceServiceClient,
        OrganizationService.OrganizationService.OrganizationServiceClient organizationServiceClient
    ) : base(context, dbConnectionFactory)
    {
        _context = context;
        _authServiceClient = authServiceClient;
        _resourceServiceClient = resourceServiceClient;
        _organizationClient = organizationServiceClient;
    }

    public async Task<ReservationDTO> GetReservationByResourceIdAsync(Guid resourceId)
    {
        var reservationResource = await QueryFirstOrDefaultAsync<ReservationResource>(
            @"SELECT * FROM ""ReservationResources"" WHERE ""ResourceId"" = @ResourceId AND ""IsActive"" = true AND ""IsDeleted"" = false",
            new { ResourceId = resourceId });

        if (reservationResource == null) return null;

        var today = DateTime.UtcNow.Date;
        var reservation = await _context
            .Reservations.Where(r => r.Id == reservationResource.ReservationId && r.IsActive && !r.IsDeleted 
                && r.StartDate.Date <= today.Date && r.EndDate.Date >= today.Date)
            .FirstOrDefaultAsync();

        if (reservation == null) return null;

        var reservationDTO = await MapToDTO(reservation);
        AuthService.IdOnly idOnly = new AuthService.IdOnly { Id = reservation.UserId.ToString() };
        reservationDTO.UserProfile = await _authServiceClient.GetUserProfileByIdAsync(idOnly);
        return reservationDTO;
    }

    public async Task<IEnumerable<ReservationDTO>> GetReservationsAsync(
        GetReservationsRequest request
    )
    {
        // Handle pagination
        var pagination = PaginationHandler.GetDefaultPagination();
        var page = request.Page > 0 ? request.Page : pagination.Page;
        var pageSize = request.PerPage > 0 ? request.PerPage : pagination.PageSize;

        var query = context
            .Reservations.Include(r => r.Status) // Include Status entity
            .Where(r => r.IsDeleted == false && r.IsActive == true); // Apply the default filters

        // Apply keyword filter if provided
        if (!string.IsNullOrEmpty(request.Keyword))
        {
            query = query.Where(r =>
                r.Code.Contains(request.Keyword) || r.Status.Name.Contains(request.Keyword)
            );
        }

        // Apply filters for UserId and OrganizationId if provided
        if (!string.IsNullOrEmpty(request.UserId))
        {
            query = query.Where(r => r.UserId.ToString() == request.UserId);
        }

        if (!string.IsNullOrEmpty(request.OrganizationId))
        {
            query = query.Where(r => r.OrganizationId.ToString() == request.OrganizationId);
        }

        if (!string.IsNullOrEmpty(request.StartDate) && !string.IsNullOrEmpty(request.EndDate))
        {
            var startDate = DateTime.Parse(request.StartDate).ToUniversalTime();
            var endDate = DateTime.Parse(request.EndDate).ToUniversalTime();

            startDate = startDate.Date;
            endDate = endDate.Date.AddDays(1).AddTicks(-1);

            query = query.Where(r => r.StartDate >= startDate && r.EndDate <= endDate);
        }

        // Apply ordering based on the provided OrderBy field
        if (!string.IsNullOrEmpty(request.OrderBy))
        {
            switch (request.OrderBy)
            {
                case "Code":
                    query = query.OrderBy(r => r.Code);
                    break;

                default:
                    query = query.OrderBy(r => r.StartDate);
                    break;
            }
        }
        else
        {
            query = query.OrderBy(r => r.StartDate);
        }

        // Apply pagination
        query = query.Skip((page - 1) * pageSize).Take(pageSize);

        // Fetch reservations along with status
        var reservations = await query.ToListAsync();

        // --- Batch fetch ReservationResources and Resources ---
        var reservationIds = reservations.Select(r => r.Id).ToList();
        // Get all ReservationResource entries for these reservations
        var reservationResources = await QueryAsync<ReservationResource>(
            @"SELECT * FROM ""ReservationResources"" WHERE ""ReservationId"" = ANY(@ReservationIds) AND ""IsDeleted"" = false",
            new { ReservationIds = reservationIds }
        );
        // Map reservationId -> list of ReservationResource
        var reservationResourceMap = reservationResources
            .GroupBy(rr => rr.ReservationId)
            .ToDictionary(g => g.Key, g => g.ToList());
        // Collect all unique resource IDs
        var allResourceIds = reservationResources.Select(rr => rr.ResourceId.ToString()).Distinct().ToList();
        // Batch fetch all resources in one call
        var allResourcesResponse = await _resourceServiceClient.GetResourcesAsync(
            new ResourceService.GetResourcesRequest { ResourceIds = { allResourceIds } }
        );
        var resourceDict = allResourcesResponse.Resources.ToDictionary(r => r.Id);

        // Map to DTOs
        var reservationDTOs = new List<ReservationDTO>();
        if (reservations.Count > 0)
        {
            // Add these dictionaries before the loop
            var userProfileCache = new Dictionary<string, AuthService.UserProfileDto>();
            var organizationCache = new Dictionary<string, OrganizationService.OrganizationDTO>();

            foreach (var reservation in reservations)
            {
                // 1. User Profile Caching
                AuthService.UserProfileDto userProfile;
                var userId = reservation.UserId.ToString();
                if (!userProfileCache.TryGetValue(userId, out userProfile))
                {
                    var idOnly = new AuthService.IdOnly { Id = userId };
                    userProfile = await authServiceClient.GetUserProfileByIdAsync(idOnly);
                    if (!string.IsNullOrEmpty(userProfile?.Id))
                    {
                        userProfileCache[userId] = userProfile;
                    }
                }

                // 2. Organization Caching (if needed)
                OrganizationService.OrganizationDTO organization = null;
                if (request.WithOrganizations)
                {
                    var orgId = reservation.OrganizationId.ToString();
                    if (!organizationCache.TryGetValue(orgId, out organization))
                    {
                        var orgRequest = new OrganizationService.GetByIdRequest { Id = orgId };
                        organization = await _organizationClient.GetOrganizationByIdAsync(orgRequest);
                        if (organization != null)
                        {
                            organizationCache[orgId] = organization;
                        }
                    }
                }

                // 3. MapToDTO (pass in organization and resource dictionary)
                var reservationDTO = await MapToDTO(
                    reservation,
                    true,
                    request.WithOrganizations,
                    organization,
                    reservationResourceMap,
                    resourceDict
                );

                // Attach cached user profile
                if (!string.IsNullOrEmpty(userProfile?.Id))
                {
                    reservationDTO.UserProfile = userProfile;
                }

                reservationDTOs.Add(reservationDTO);
            }
        }

        return reservationDTOs;
    }
    
    
    
    public async Task<GetReservationsByResourcesResponse> GetReservationsByResourcesAsync(GetReservationsByResourcesRequest request)
    {
        var startDate = DateTime.Parse(request.StartDate).ToUniversalTime();
        var endDate = DateTime.Parse(request.EndDate).ToUniversalTime();
        var resourceGuids = request.ResourceIds.Select(Guid.Parse).ToList();

        var sql = @"SELECT DISTINCT r.* FROM ""Reservations"" r
            INNER JOIN ""ReservationResources"" rr ON r.""Id"" = rr.""ReservationId""
            WHERE rr.""ResourceId"" = ANY(@ResourceIds) AND r.""IsDeleted"" = false AND r.""IsActive"" = true
                AND r.""StartDate"" <= @EndDate AND r.""EndDate"" >= @StartDate
            ORDER BY r.""StartDate""";

        var reservations = await QueryAsync<Models.Reservation>(sql, new { ResourceIds = resourceGuids.ToArray(), StartDate = startDate, EndDate = endDate });
        var response = new GetReservationsByResourcesResponse();

        foreach (var reservation in reservations)
        {
            var dto = await MapToDTO(reservation, true);
            response.Reservations.Add(dto);
        }
        return response;
    }

    public async Task<SearchClientsResponse> SearchClientsByNameAsync(string nameQuery, string organizationId, int maxResults)
    {
        if (string.IsNullOrWhiteSpace(nameQuery)) return new SearchClientsResponse();

        var query = @"WITH RankedResults AS (
                SELECT DISTINCT ON (d.""Name"", d.""Phone"") d.""Name"", d.""Phone"", d.""Email"", r.""CreatedAt"" as ""ReservationDate"",
                    ROW_NUMBER() OVER (ORDER BY r.""CreatedAt"" DESC) as RowNum
                FROM ""Details"" d INNER JOIN ""Reservations"" r ON d.""ReservationId"" = r.""Id""
                WHERE r.""OrganizationId"" = @OrganizationId::uuid AND r.""IsDeleted"" = false AND r.""IsActive"" = true
                    AND (LOWER(d.""Name"") LIKE LOWER(@SearchPattern) OR d.""Phone"" LIKE @SearchPattern OR LOWER(d.""Email"") LIKE LOWER(@SearchPattern))
            ) SELECT * FROM RankedResults WHERE RowNum <= @MaxResults ORDER BY ""ReservationDate"" DESC";

        var searchPattern = $"%{nameQuery}%";
        var results = await QueryAsync<(string Name, string Phone, string Email, DateTime ReservationDate)>(
            query, new { OrganizationId = organizationId, SearchPattern = searchPattern, MaxResults = maxResults });

        var response = new SearchClientsResponse();
        foreach (var result in results)
        {
            response.Clients.Add(new ClientSuggestion
            {
                Name = result.Name ?? "",
                Phone = result.Phone ?? "",
                Email = result.Email ?? "",
                MostRecentReservationDate = result.ReservationDate.ToString("MM/dd")
            });
        }
        return response;
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
        // Fetch the Detail asynchronously, separate from the main query
        var detail = await QueryFirstOrDefaultAsync<Models.Detail>(
            @"
        SELECT *
        FROM ""Details""
        WHERE ""ReservationId"" = @ReservationId",
            new { ReservationId = reservation.Id }
        );

        // Fetch the Status asynchronously, similar to how Detail is fetched
        var status = await QueryFirstOrDefaultAsync<Status>(
            @"
        SELECT *
        FROM ""Statuses""
        WHERE ""Id"" = @StatusId AND ""IsDeleted"" = false AND ""IsActive"" = true",
            new { StatusId = reservation.StatusId }
        );

        // Use pre-fetched ReservationResources if available
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

        var color =
            statusName != null
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
            Detail =
                detail != null
                    ? new DetailDTO
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
                    }
                    : null,
            ResourceIds = { resources.Select(r => r.ResourceId.ToString()) },
            Color = color != null ? color.GetValue(null).ToString() : string.Empty,
        };

        // Only fetch resources if withResources is true
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
            // fallback: fetch resources as before (should not happen in main flow)
            var resourceIds = resources.Select(r => r.ResourceId.ToString()).ToList();
            var dbResources = await _resourceServiceClient.GetResourcesAsync(
                new ResourceService.GetResourcesRequest
                {
                    OrganizationId = reservation.OrganizationId.ToString(),
                    ResourceIds = { resourceIds },
                }
            );

            // Map each resource to ResourceItemDto instead of directly adding the resources
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

        // Fetch organization information if requested
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
                var org = await _organizationClient.GetOrganizationByIdAsync(
                    organizationRequest
                );
                if (org != null)
                {
                    reservationDto.Organization = org;
                }
            }
        }

        return reservationDto;
    }
}