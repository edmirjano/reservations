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

public class ReservationRepository(
    ReservationContext context,
    Func<IDbConnection> dbConnectionFactory,
    AuthService.AuthService.AuthServiceClient authServiceClient,
    PaymentService.PaymentService.PaymentServiceClient paymentServiceClient,
    ResourceService.ResourceService.ResourceServiceClient resourceServiceClient,
    OrganizationService.OrganizationService.OrganizationServiceClient organizationServiceClient
)
    : GenericRepository<Models.Reservation, ReservationContext>(context, dbConnectionFactory),
        IReservation
{
    private readonly ResourceService.ResourceService.ResourceServiceClient _resourceServiceClient =
        resourceServiceClient;
    private readonly OrganizationService.OrganizationService.OrganizationServiceClient _organizationClient =
        organizationServiceClient;

    public async Task<ReservationDTO> CreateReservationAsync(
        CreateReservationDto createReservationDto
    )
    {
        var createdStatus = await AddOrCreateInitialReservationStatus();
        var reservation = new Models.Reservation
        {
            UserId = Guid.Parse(createReservationDto.UserId),
            OrganizationId = Guid.Parse(createReservationDto.OrganizationId),
            //PaymentTypeId = Guid.Parse(createReservationDto.PaymentTypeId),
            StatusId = createdStatus,
            Source = createReservationDto.Source,
            StartDate = DateTime.Parse(createReservationDto.StartDate).Date.ToUniversalTime(),
            EndDate = DateTime.Parse(createReservationDto.EndDate).Date.ToUniversalTime(),
        };

        var dbResources = await _resourceServiceClient.GetResourcesAsync(
            new ResourceService.GetResourcesRequest
            {
                OrganizationId = createReservationDto.OrganizationId,
                // ResourceIds = { createReservationDto.Resources.Select(r => r.Id) },
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

        // Generate a unique reservation code
        reservation.Code = GenerateReservationCode();
        var resourcePrices = paymentServiceClient.GetFullPrices(getFullPricesRequest);

        //if (resourcePrices.Prices.Sum(p => p.FullPrice) != createReservationDto.TotalAmount)
        //{
        //    throw new Exception("Total amount does not match the sum of resource prices.");
        //}
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

        // Store resource items if present
        if (createReservationDto.Resources != null)
        {
            foreach (var resourceDto in createReservationDto.Resources)
            {
                await SaveReservationResource(createdReservation.Id, resourceDto);
            }
        }

        // Broadcast the change
        _ = BroadcastReservationChangeAsync(createdReservation);

        return await MapToDTO(createdReservation);
    }

    public async Task<ReservationDTO> CreateReservationForOrganizationAsync(
        CreateReservationForOrganizationDto createReservationDto
    )
    {
        if (!createReservationDto.Resources.Any())
            throw new Exception("At least 1 resource is required for making a reservation");

        //var createdStatus = await AddOrCreateInitialReservationStatus();

        // Generate a unique reservation code
        string reservationCode = GenerateReservationCode();

        var reservation = new Models.Reservation
        {
            UserId = Guid.Parse(createReservationDto.UserId),
            OrganizationId = Guid.Parse(createReservationDto.OrganizationId),
            //PaymentTypeId = Guid.Parse(createReservationDto.PaymentTypeId),
            StatusId = Guid.Parse(createReservationDto.StatusId),
            StartDate = DateHelper.ParseDate(createReservationDto.StartDate),
            EndDate = DateHelper.ParseDate(createReservationDto.EndDate),
            Code = reservationCode,
            Source = "Organization", // Since this reservation is created by the organization
            TotalAmount = createReservationDto.TotalAmount, // Always use calculated total
        };

        var createdReservation = await CreateAsync(reservation);

        // Create detail for the reservation, using provided values or defaults
        var detail = new Models.Detail
        {
            Id = Guid.NewGuid(),
            ReservationId = createdReservation.Id,
            Name = !string.IsNullOrWhiteSpace(createReservationDto.CustomerName)
                ? createReservationDto.CustomerName
                : "", // Use provided name or default
            Email = !string.IsNullOrWhiteSpace(createReservationDto.Email)
                ? createReservationDto.Email
                : "", // Use provided email or empty
            Phone = !string.IsNullOrWhiteSpace(createReservationDto.Phone)
                ? createReservationDto.Phone
                : "", // Use provided phone or empty
            NumberOfAdults = 0,
            NumberOfChildren = 0,
            NumberOfInfants = 0,
            NumberOfPets = 0,
            ResourceQuantity = createReservationDto.Resources?.Sum(r => r.Quantity) ?? 0,
            Note = createReservationDto.Notes,
            OriginalPrice = createdReservation.TotalAmount, // Use calculated total
            Discount = 0,
            Currency = !string.IsNullOrWhiteSpace(createReservationDto.Currency)
                ? createReservationDto.Currency
                : "", // Use provided currency or default to EUR
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

        // Store resource items if present
        if (createReservationDto.Resources != null)
        {
            foreach (var resourceDto in createReservationDto.Resources)
            {
                if (!String.IsNullOrEmpty(resourceDto.Slug) && String.IsNullOrEmpty(resourceDto.Id))
                {
                    var slugreq = new ResourceService.GetBySlugRequest { Slug = resourceDto.Slug };

                    var resourcebyslug = await _resourceServiceClient.GetResourceBySlugAsync(
                        slugreq
                    );
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

        // Broadcast the change
        _ = BroadcastReservationChangeAsync(createdReservation);

        return await MapToDTO(createdReservation);
    }

    public async Task<ReservationDTO> GetReservationByResourceIdAsync(Guid resourceId)
    {
        // Query the ReservationResources table to check for the resource
        var reservationResource = await QueryFirstOrDefaultAsync<ReservationResource>(
            @"
        SELECT *
        FROM ""ReservationResources""
        WHERE ""ResourceId"" = @ResourceId
        AND ""IsActive"" = true
        AND ""IsDeleted"" = false",
            new { ResourceId = resourceId }
        );

        // If no reservation resource is found, return null or throw an exception depending on your requirements
        if (reservationResource == null)
        {
            return null;
        }

        var today = DateTime.UtcNow.Date;

        var reservation = await context
            .Reservations.Where(r =>
                r.Id == reservationResource.ReservationId
                && r.IsActive
                && !r.IsDeleted
                && r.StartDate.Date <= today.Date
                && r.EndDate.Date >= today.Date
            )
            .FirstOrDefaultAsync();

        if (reservation == null)
        {
            return null;
        }

        // Map the reservation to DTO with resources
        var reservationDTO = await MapToDTO(reservation);

        // Fetch user profile for this reservation
        AuthService.IdOnly idOnly = new AuthService.IdOnly { Id = reservation.UserId.ToString() };
        reservationDTO.UserProfile = await authServiceClient.GetUserProfileByIdAsync(idOnly);

        return reservationDTO;
    }

    public async Task<ReservationDTO> UpdateReservationForOrganizationAsync(ReservationDTO dto)
    {
        // Fetch existing reservation
        var reservation = await QueryFirstOrDefaultAsync<Models.Reservation>(
            @"SELECT * FROM ""Reservations""
            WHERE ""Id"" = @Id AND ""IsDeleted"" = false",
            new { Id = Guid.Parse(dto.Id) }
        );

        if (reservation == null)
            throw new Exception("Reservation not found");

        // Update reservation fields
        reservation.StartDate = DateHelper.ParseDate(dto.StartDate);
        reservation.EndDate = DateHelper.ParseDate(dto.EndDate);
        reservation.TotalAmount = dto.TotalAmount;
        reservation.UpdatedAt = DateTime.UtcNow;
        
        // IMPORTANT: We should never change the IsActive property
        // reservation.IsActive remains unchanged from its original value
        
        var statusFinished = new StatusDTO();
        if (!String.IsNullOrEmpty(dto.StatusId))
        {
            reservation.StatusId = Guid.Parse(dto.StatusId);

            //Find finished status
            statusFinished = (await this.GetStatuses())
                .Where(p => p.Id.Equals(dto.StatusId))
                .FirstOrDefault();
                
            // We no longer modify IsActive based on status
        }

        // Create a parameter object to ensure all values are properly mapped
        var parameters = new
        {
            Id = reservation.Id,
            StartDate = reservation.StartDate,
            EndDate = reservation.EndDate,
            TotalAmount = reservation.TotalAmount,
            StatusId = reservation.StatusId,
            UpdatedAt = reservation.UpdatedAt
            // IsActive is deliberately excluded from the parameter object
        };

        // Log values for debugging
        Console.WriteLine($"Updating reservation {reservation.Id}:");
        Console.WriteLine($"  StartDate: {reservation.StartDate:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"  EndDate: {reservation.EndDate:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"  StatusId: {reservation.StatusId}");

        try
        {
            // Use Dapper for direct database update to ensure immediate persistence
            var rowsAffected = await ExecuteAsync(
                @"
            UPDATE ""Reservations""
            SET ""StartDate"" = @StartDate,
                ""EndDate"" = @EndDate,
                ""TotalAmount"" = @TotalAmount,
                ""StatusId"" = @StatusId,
                ""UpdatedAt"" = @UpdatedAt
            WHERE ""Id"" = @Id",
                parameters
            );
            
            Console.WriteLine($"Update affected {rowsAffected} rows");
            
            // Verify the update worked by retrieving the updated record using Dapper
            var updatedRecord = await QueryFirstOrDefaultAsync<Models.Reservation>(
                @"SELECT * FROM ""Reservations"" WHERE ""Id"" = @Id",
                new { Id = reservation.Id }
            );
            
            if (updatedRecord != null)
            {
                Console.WriteLine($"Record updated successfully. Values in database:");
                Console.WriteLine($"  StartDate: {updatedRecord.StartDate:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine($"  EndDate: {updatedRecord.EndDate:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine($"  StatusId: {updatedRecord.StatusId}");
                Console.WriteLine($"  IsActive: {updatedRecord.IsActive}");
            }
            else
            {
                Console.WriteLine("Warning: Could not verify update - record not found after update!");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating reservation: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            throw;
        }

        // Update or insert detail
        if (dto.Detail != null)
        {
            var existingDetail = await QueryFirstOrDefaultAsync<Models.Detail>(
                @"SELECT * FROM ""Details"" WHERE ""ReservationId"" = @ReservationId",
                new { ReservationId = reservation.Id }
            );

            if (existingDetail != null)
            {
                // Update detail
                existingDetail.Name = dto.Detail.Name;
                existingDetail.Email = dto.Detail.Email;
                existingDetail.Phone = dto.Detail.Phone;
                existingDetail.Note = dto.Detail.Note;
                existingDetail.Currency = dto.Detail.Currency;
                existingDetail.ResourceQuantity = dto.Detail.ResourceQuantity;
                existingDetail.Discount = dto.Detail.Discount;
                existingDetail.OriginalPrice = dto.Detail.OriginalPrice;
                existingDetail.UpdatedAt = DateTime.UtcNow;

                // Check if statusFinished is not null before using it
                if (statusFinished != null)
                {
                    // Only add notes based on status, but don't change IsActive
                    if (statusFinished.Name.Equals(ReservationStatusNames.Finished))
                    {
                        existingDetail.Note +=
                            $"{Environment.NewLine} Reservation completed on {DateTime.Now}";
                    }
                    else if (statusFinished.Name.Equals(ReservationStatusNames.CheckedIn))
                    {
                        existingDetail.Note +=
                            $"{Environment.NewLine} Client checked in on {DateTime.Now}";
                    }
                }

                // Use Dapper for direct database update
                await ExecuteAsync(
                    @"
                UPDATE ""Details""
                SET ""Name"" = @Name,
                    ""Email"" = @Email,
                    ""Phone"" = @Phone,
                    ""Note"" = @Note,
                    ""Currency"" = @Currency,
                    ""OriginalPrice"" = @OriginalPrice,
                    ""Discount"" = @Discount,
                    ""ResourceQuantity"" = @ResourceQuantity,
                    ""UpdatedAt"" = @UpdatedAt
                WHERE ""ReservationId"" = @ReservationId",
                    new
                    {
                        Name = existingDetail.Name,
                        Email = existingDetail.Email,
                        Phone = existingDetail.Phone,
                        Note = existingDetail.Note,
                        Currency = existingDetail.Currency,
                        OriginalPrice = existingDetail.OriginalPrice,
                        Discount = existingDetail.Discount,
                        ResourceQuantity = existingDetail.ResourceQuantity,
                        UpdatedAt = existingDetail.UpdatedAt,
                        ReservationId = reservation.Id
                    }
                );
            }
            else
            {
                // Insert if missing
                var newDetail = new Models.Detail
                {
                    Id = Guid.NewGuid(),
                    ReservationId = reservation.Id,
                    Name = dto.Detail.Name ?? "Organization Booking",
                    Email = dto.Detail.Email ?? "",
                    Phone = dto.Detail.Phone ?? "",
                    Note = dto.Detail.Note,
                    Currency = dto.Detail.Currency ?? "EUR",
                    ResourceQuantity = dto.Detail.ResourceQuantity,
                    OriginalPrice = dto.Detail.OriginalPrice,
                    Discount = dto.Detail.Discount,
                    NumberOfAdults = dto.Detail.NumberOfAdults,
                    NumberOfChildren = dto.Detail.NumberOfChildren,
                    NumberOfInfants = dto.Detail.NumberOfInfants,
                    NumberOfPets = dto.Detail.NumberOfPets,
                    IsActive = true,
                    IsDeleted = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                };
                await ExecuteAsync(
                    @"INSERT INTO ""Details"" (""Id"", ""ReservationId"", ""Name"", ""Email"", ""Phone"", ""NumberOfAdults"", ""NumberOfChildren"", ""NumberOfInfants"", ""NumberOfPets"", ""ResourceQuantity"", ""Note"", ""OriginalPrice"", ""Discount"", ""Currency"", ""IsActive"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"")
                               VALUES(@Id, @ReservationId, @Name, @Email, @Phone, @NumberOfAdults, @NumberOfChildren, @NumberOfInfants, @NumberOfPets, @ResourceQuantity, @Note, @OriginalPrice, @Discount, @Currency, @IsActive, @IsDeleted, @CreatedAt, @UpdatedAt)",
                    newDetail
                );
            }
        }

        if (dto.ResourceIds != null && dto.ResourceIds.Any())
        {
            await ExecuteAsync(
                @"UPDATE ""ReservationResources"" SET ""IsDeleted"" = true WHERE ""ReservationId"" = @ReservationId",
                new { ReservationId = reservation.Id }
            );

            foreach (var resourceIdStr in dto.ResourceIds)
            {
                var resourceId = Guid.Parse(resourceIdStr);
                var existing = await QueryFirstOrDefaultAsync<ReservationResource>(
                    @"SELECT * FROM ""ReservationResources"" WHERE ""ReservationId"" = @ReservationId AND ""ResourceId"" = @ResourceId",
                    new { ReservationId = reservation.Id, ResourceId = resourceId }
                );

                if (existing != null)
                {
                    existing.IsDeleted = false;
                    existing.UpdatedAt = DateTime.UtcNow;

                    await ExecuteAsync(
                        @"UPDATE ""ReservationResources"" SET ""IsDeleted"" = @IsDeleted, ""UpdatedAt"" = @UpdatedAt
                    WHERE ""Id"" = @Id",
                        existing
                    );
                }
                else
                {
                    await SaveReservationResource(
                        reservation.Id,
                        new ResourceItemDto { Id = resourceIdStr }
                    );
                }
            }
        }

        // Broadcast the change
        _ = BroadcastReservationChangeAsync(reservation);

        return await MapToDTO(reservation);
    }

    public async Task<ReservationDTO> UpdateReservationAsync(ReservationDTO reservationDto)
    {
        var reservation = GetById(Guid.Parse(reservationDto.Id));
        reservation.UserId = Guid.Parse(reservationDto.UserId);
        reservation.OrganizationId = Guid.Parse(reservationDto.OrganizationId);
        //reservation.PaymentTypeId = Guid.Parse(reservationDto.PaymentTypeId);
        reservation.StatusId = Guid.Parse(reservationDto.StatusId);
        reservation.TotalAmount = reservationDto.TotalAmount;
        reservation.Code = reservationDto.Code;
        reservation.StartDate = DateHelper.ParseDate(reservationDto.StartDate);
        reservation.EndDate = DateHelper.ParseDate(reservationDto.EndDate);
        reservation.Source = reservationDto.Source;

        var updatedReservation = await UpdateAsync(reservation);

        var detail = new Models.Detail
        {
            ReservationId = updatedReservation.Id,
            Name = reservationDto.Detail.Name,
            Email = reservationDto.Detail.Email,
            Phone = reservationDto.Detail.Phone,
            NumberOfAdults = reservationDto.Detail.NumberOfAdults,
            NumberOfChildren = reservationDto.Detail.NumberOfChildren,
            NumberOfInfants = reservationDto.Detail.NumberOfInfants,
            NumberOfPets = reservationDto.Detail.NumberOfPets,
            ResourceQuantity = reservationDto.Detail.ResourceQuantity,
            Note = reservationDto.Detail.Note,
            OriginalPrice = reservationDto.Detail.OriginalPrice,
            Discount = reservationDto.Detail.Discount,
            Currency = reservationDto.Detail.Currency,
        };

        await ExecuteAsync(
            @"
    UPDATE ""Details""
    SET ""Name"" = @Name,
        ""Email"" = @Email,
        ""Phone"" = @Phone,
        ""NumberOfAdults"" = @NumberOfAdults,
        ""NumberOfChildren"" = @NumberOfChildren,
        ""NumberOfInfants"" = @NumberOfInfants,
        ""NumberOfPets"" = @NumberOfPets,
        ""ResourceQuantity"" = @ResourceQuantity,
        ""Note"" = @Note,
        ""OriginalPrice"" = @OriginalPrice,
        ""Discount"" = @Discount,
        ""Currency"" = @Currency
    WHERE ""ReservationId"" = @ReservationId",
            detail
        );

        // Broadcast the change
        _ = BroadcastReservationChangeAsync(updatedReservation);

        return await MapToDTO(updatedReservation);
    }

    public async Task DeleteReservationAsync(Guid id)
    {
        var reservation = await DeleteAsync(id); // Only fetch and delete once
        if (reservation != null)
        {
            await ExecuteAsync(
                @"UPDATE ""Details"" SET ""IsDeleted"" = true, ""IsActive"" = false WHERE ""ReservationId"" = @ReservationId",
                new { ReservationId = id }
            );
            await ExecuteAsync(
                @"UPDATE ""ReservationResources"" SET ""IsDeleted"" = true, ""IsActive"" = false WHERE ""ReservationId"" = @ReservationId",
                new { ReservationId = id }
            );
            await BroadcastReservationChangeAsync(reservation);
        }
    }

    public async Task<ReservationDTO> GetReservationByIdAsync(Guid id)
    {
        var reservation = await context
            .Reservations.Where(r => r.Id == id && r.IsDeleted == false && r.IsActive == true)
            .Include(r => r.Status)
            .FirstOrDefaultAsync();

        if (reservation == null)
        {
            return null;
        }
        AuthService.IdOnly idOnly = new AuthService.IdOnly();
        idOnly.Id = reservation.UserId.ToString();
        var userProfile = await authServiceClient.GetUserProfileByIdAsync(idOnly);

        // Map the reservation to DTO and include resources
        var reservationDTO = await MapToDTO(reservation);
        reservationDTO.UserProfile = userProfile;

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

    public async Task<GetReservationsByResourcesResponse> GetReservationsByResourcesAsync(
        GetReservationsByResourcesRequest request
    )
    {
        // Parse start and end as full DateTime, preserving time
        var startDate = DateTime.Parse(request.StartDate).ToUniversalTime();
        var endDate = DateTime.Parse(request.EndDate).ToUniversalTime();

        var resourceGuids = request.ResourceIds.Select(Guid.Parse).ToList();

        // Use Dapper for better performance and no caching issues
        var sql = @"
            SELECT DISTINCT r.*
            FROM ""Reservations"" r
            INNER JOIN ""ReservationResources"" rr ON r.""Id"" = rr.""ReservationId""
            WHERE rr.""ResourceId"" = ANY(@ResourceIds)
                AND r.""IsDeleted"" = false
                AND r.""IsActive"" = true
                AND r.""StartDate"" <= @EndDate
                AND r.""EndDate"" >= @StartDate
            ORDER BY r.""StartDate""";

        var parameters = new
        {
            ResourceIds = resourceGuids.ToArray(),
            StartDate = startDate,
            EndDate = endDate
        };

        // Use Dapper to get reservations directly from database
        var reservations = await QueryAsync<Models.Reservation>(sql, parameters);

        var response = new GetReservationsByResourcesResponse();

        foreach (var reservation in reservations)
        {
            var dto = await MapToDTO(reservation, true);
            response.Reservations.Add(dto);
        }

        return response;
    }

    public async Task<ReservationDTO> ValidateTicketAsync(string code)
    {
        var reservationDto = new ReservationDTO();
        var reservation = _context.Reservations.FirstOrDefault(x => x.Code == code);
        if (reservation != null)
        {
            var statuses = await GetStatuses();
            if (statuses != null)
            {
                var actualStatus = statuses
                    .FirstOrDefault(x => x.Id == reservation.StatusId.ToString())
                    ?.Name;
                if (actualStatus != "CheckedIn")
                {
                    var checkedInStatusId = statuses.FirstOrDefault(x => x.Name == "CheckedIn")?.Id;
                    if (checkedInStatusId != null)
                    {
                        reservation.StatusId = Guid.Parse(checkedInStatusId);
                        await UpdateAsync(reservation);
                    }
                }
            }
            reservationDto = await GetReservationByIdAsync(reservation.Id);
        }
        return reservationDto;
    }

    public async Task<IEnumerable<DateReservationCountDTO>> GetReservationsCountPerDayAsync(
        string organizationId,
        DateTime startDate,
        DateTime endDate
    )
    {
        using (var connection = dbConnectionFactory())
        {
            var query =
                @"
            SELECT
                DATE_TRUNC('day', r.""StartDate"") AS ""Date"",
                COUNT(*) AS ""ReservationCount""
            FROM ""Reservations"" r
            WHERE r.""OrganizationId"" = @OrganizationId::uuid
              AND r.""StartDate"" >= @StartDate
              AND r.""EndDate"" <= @EndDate
              AND r.""IsActive"" = true
              AND r.""IsDeleted"" = false
            GROUP BY DATE_TRUNC('day', r.""StartDate"")
            ORDER BY ""Date"";";

            var result = await connection.QueryAsync<DateReservationCountDTO>(
                query,
                new
                {
                    OrganizationId = organizationId,
                    StartDate = startDate,
                    EndDate = endDate,
                }
            );

            return result;
        }
    }

    public async Task<SearchClientsResponse> SearchClientsByNameAsync(
        string nameQuery,
        string organizationId,
        int maxResults
    )
    {
        if (string.IsNullOrWhiteSpace(nameQuery))
            return new SearchClientsResponse();

        var query =
            @"
            WITH RankedResults AS (
                SELECT DISTINCT ON (d.""Name"", d.""Phone"")
                    d.""Name"",
                    d.""Phone"",
                    d.""Email"",
                    r.""CreatedAt"" as ""ReservationDate"",
                    ROW_NUMBER() OVER (ORDER BY r.""CreatedAt"" DESC) as RowNum
                FROM ""Details"" d
                INNER JOIN ""Reservations"" r ON d.""ReservationId"" = r.""Id""
                WHERE r.""OrganizationId"" = @OrganizationId::uuid
                    AND r.""IsDeleted"" = false
                    AND r.""IsActive"" = true
                    AND (
                        LOWER(d.""Name"") LIKE LOWER(@SearchPattern)
                        OR d.""Phone"" LIKE @SearchPattern
                        OR LOWER(d.""Email"") LIKE LOWER(@SearchPattern)
                    )
            )
            SELECT *
            FROM RankedResults
            WHERE RowNum <= @MaxResults
            ORDER BY ""ReservationDate"" DESC";

        var searchPattern = $"%{nameQuery}%";

        var results = await QueryAsync<(
            string Name,
            string Phone,
            string Email,
            DateTime ReservationDate
        )>(
            query,
            new
            {
                OrganizationId = organizationId,
                SearchPattern = searchPattern,
                MaxResults = maxResults,
            }
        );

        var response = new SearchClientsResponse();
        foreach (var result in results)
        {
            response.Clients.Add(
                new ClientSuggestion
                {
                    Name = result.Name ?? "",
                    Phone = result.Phone ?? "",
                    Email = result.Email ?? "",
                    MostRecentReservationDate = result.ReservationDate.ToString("MM/dd"),
                }
            );
        }

        return response;
    }

    public async Task<GetReservationsBySourceCountResponse> GetReservationsBySourceCountAsync(
        string organizationId,
        DateTime startDate,
        DateTime endDate
    )
    {
        using (var connection = dbConnectionFactory())
        {
            var query =
                @"
                SELECT
                    COUNT(CASE WHEN ""Source"" = 'Mobile' THEN 1 END) as ""TotalReservationsClient"",
                    COUNT(CASE WHEN ""Source"" = 'Organization' THEN 1 END) as ""TotalReservationsBusiness""
                FROM ""Reservations""
                WHERE ""OrganizationId"" = @OrganizationId::uuid
                AND ""StartDate"" >= @StartDate
                AND ""EndDate"" <= @EndDate
                AND ""IsActive"" = true
                AND ""IsDeleted"" = false";

            var result =
                await connection.QueryFirstOrDefaultAsync<GetReservationsBySourceCountResponse>(
                    query,
                    new
                    {
                        OrganizationId = organizationId,
                        StartDate = startDate,
                        EndDate = endDate,
                    }
                );

            return result ?? new GetReservationsBySourceCountResponse();
        }
    }

    // Helper method to generate a unique reservation code
    private string GenerateReservationCode()
    {
        // Generate a 8-character unique code with a mix of letters and numbers
        // Format: ORG-XXXXX (where X is alphanumeric)
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();

        string randomPart = new string(
            Enumerable.Repeat(chars, 5).Select(s => s[random.Next(s.Length)]).ToArray()
        );

        return $"ORG-{randomPart}";
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

    private async Task<Guid> AddOrCreateInitialReservationStatus()
    {
        var createdStatus = QueryAsync<Status>(
            @"SELECT * FROM ""Statuses"" WHERE ""Name"" = 'Created' AND ""IsDeleted"" = false AND ""IsActive"" = true"
        )
            .Result.FirstOrDefault();

        if (createdStatus != null)
            return createdStatus.Id;

        var status = new Status
        {
            Id = Guid.NewGuid(),
            Name = "Created",
            Description = "Reservation has been created by user, but it is not payed yet.",
            IsActive = true,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        await ExecuteAsync(
            @"INSERT INTO ""Statuses"" (""Id"", ""Name"", ""Description"", ""IsActive"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"")
                       VALUES(@Id, @Name, @Description, @IsActive, @IsDeleted, @CreatedAt, @UpdatedAt)",
            status
        );

        return status.Id;
    }

    private async Task<IEnumerable<StatusDTO>> GetStatuses()
    {
        using (var connection = dbConnectionFactory())
        {
            var query =
                @"
                SELECT 
                    ""Id""::text AS ""Id"",
                    ""Name"",
                    ""Description""
                FROM ""Statuses""
                WHERE ""IsDeleted"" = false AND ""IsActive"" = true";

            var result = await connection.QueryAsync<StatusDTO>(query);

            return result;
        }
    }

    private async Task BroadcastReservationChangeAsync(Models.Reservation reservation)
    {
        try
        {
            // Get organization slug from organization ID
            var organizationRequest = new OrganizationService.GetByIdRequest
            {
                Id = reservation.OrganizationId.ToString(),
            };
            var organization = await _organizationClient.GetOrganizationByIdAsync(
                organizationRequest
            );

            if (organization == null)
            {
                return;
            }

            // Get resources for this reservation
            var resources = await QueryAsync<ReservationResource>(
                @"SELECT * FROM ""ReservationResources""
                WHERE ""ReservationId"" = @ReservationId AND ""IsDeleted"" = false",
                new { ReservationId = reservation.Id }
            );

            if (!resources.Any())
            {
                return;
            }

            // Get resources from resource service
            var request = new ResourceService.GetResourcesByOrganizationSlugRequest
            {
                Slug = organization.Slug,
                Date = reservation.StartDate.ToString("yyyy-MM-dd"),
            };

            // Call the resource service to trigger a websocket update
            await _resourceServiceClient.GetResourcesByOrganizationSlugAsync(request);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error broadcasting reservation change: {ex.Message}");
        }
    }

    public async Task<byte[]> GenerateReservationReportAsync(
        string organizationId,
        string startDate,
        string endDate
    )
    {
        var enrichedReservations = await GetEnrichedReservationDataAsync(organizationId, startDate, endDate);
        var analytics = await CalculateReservationAnalyticsAsync(enrichedReservations, startDate, endDate);
        return await GenerateEnrichedReservationExcelWithAnalyticsAsync(enrichedReservations, analytics, organizationId);
    }

    private async Task<byte[]> GenerateEnrichedReservationExcelWithAnalyticsAsync(
        List<EnrichedReservationData> reservations,
        ReservationAnalytics analytics,
        string organizationId)
    {
        using (var workbook = new ClosedXML.Excel.XLWorkbook())
        {
                        // Create Summary Sheet with Analytics
            ExcelReportHelper.CreateSummarySheet(workbook, analytics);

            // Create Charts Sheet
            ExcelReportHelper.CreateChartsSheet(workbook, analytics);

                        // Create Resource Data Sheet
            ExcelReportHelper.CreateResourceDataSheet(workbook, reservations, analytics);

            // Create SunEasy Data Sheet
            ExcelReportHelper.CreateSunEasyDataSheet(workbook, reservations, analytics);

            // Create Users Sheet
            await CreateUsersSheet(workbook, reservations, analytics, organizationId);

            // Create Data Sheet
            ExcelReportHelper.CreateDataSheet(workbook, reservations);

            using (var stream = new System.IO.MemoryStream())
            {
                workbook.SaveAs(stream);
                return stream.ToArray();
            }
        }
    }

    private async Task<List<EnrichedReservationData>> GetEnrichedReservationDataAsync(
        string organizationId,
        string startDate,
        string endDate
    )
    {
        // First, get basic reservation data with joins
        var reservationData = await QueryAsync<ReservationQueryResult>(
            @"SELECT
                r.""Id"" as ReservationId,
                r.""Code"" as ReservationCode,
                r.""OrganizationId"",
                r.""UserId"",
                r.""StatusId"",
                r.""Source"",
                r.""TotalAmount"",
                r.""TotalAmount"" as NetAmount,
                r.""StartDate"",
                r.""EndDate"",
                s.""Name"" as StatusName,
                d.""Name"" as CustomerName,
                d.""Phone"" as CustomerPhone,
                d.""Email"" as CustomerEmail,
                d.""Note"" as Notes,
                d.""Currency""
            FROM ""Reservations"" r
            LEFT JOIN ""Statuses"" s ON r.""StatusId"" = s.""Id""
            LEFT JOIN ""Details"" d ON r.""Id"" = d.""ReservationId""
            WHERE r.""OrganizationId"" = @OrgId
                AND r.""StartDate"" >= @Start
                AND r.""EndDate"" <= @End
                AND r.""IsDeleted"" = false
                AND r.""IsActive"" = true",
            new
            {
                OrgId = Guid.Parse(organizationId),
                Start = DateTime.Parse(startDate),
                End = DateTime.Parse(endDate),
            }
        );

        var enrichedList = new List<EnrichedReservationData>();

        // Get organization info once
        var organizationRequest = new OrganizationService.GetByIdRequest { Id = organizationId };
        var organization = await _organizationClient.GetOrganizationByIdAsync(organizationRequest);
        string organizationName = organization?.Name ?? "Unknown Organization";

        foreach (var reservation in reservationData)
        {
            var enrichedReservation = new EnrichedReservationData
            {
                ReservationCode = reservation.ReservationCode ?? "",
                OrganizationName = organizationName,
                StatusName = reservation.StatusName ?? "",
                Source = reservation.Source ?? "",
                TotalAmount = (double)reservation.TotalAmount,
                NetAmount = (double)reservation.NetAmount,
                StartDate = reservation.StartDate,
                EndDate = reservation.EndDate,
                CustomerName = reservation.CustomerName ?? "",
                CustomerPhone = reservation.CustomerPhone ?? "",
                CustomerEmail = reservation.CustomerEmail ?? "",
                Notes = reservation.Notes ?? "",
                Currency = reservation.Currency ?? ""
            };

            // Get user details
            try
            {
                var userIdOnly = new AuthService.IdOnly { Id = reservation.UserId.ToString() };
                var userProfile = await authServiceClient.GetUserProfileByIdAsync(userIdOnly);
                enrichedReservation.Username = userProfile?.Username ?? "Unknown User";
                enrichedReservation.UserEmail = userProfile?.Email ?? "";
            }
            catch
            {
                enrichedReservation.Username = "Unknown User";
                enrichedReservation.UserEmail = "";
            }

            // Calculate net amount based on source
            if (reservation.Source == "Organization")
            {
                enrichedReservation.NetAmount = (double)reservation.TotalAmount;
            }
            else
            {
                // For SunEasy reservations, we need to calculate the organization's net value
                // This would be the total amount minus platform fees
                // For now, we'll use a simple calculation - you can adjust this based on your business logic
                enrichedReservation.NetAmount = (double)reservation.TotalAmount * 0.85; // Assuming 15% platform fee
            }

            // Get resource numbers
            try
            {
                var resourceNumbers = await GetResourceNumbersForReservation(reservation.ReservationId);
                enrichedReservation.ResourceNumbers = string.Join(", ", resourceNumbers);
            }
            catch
            {
                enrichedReservation.ResourceNumbers = "";
            }

            enrichedList.Add(enrichedReservation);
        }

        return enrichedList;
    }

    private async Task<List<string>> GetResourceNumbersForReservation(Guid reservationId)
    {
        var resourceIds = await QueryAsync<Guid>(
            @"SELECT ""ResourceId"" FROM ""ReservationResources""
              WHERE ""ReservationId"" = @ReservationId
                AND ""IsDeleted"" = false",
            new { ReservationId = reservationId }
        );

        var resourceNumbers = new List<string>();

        foreach (var resourceId in resourceIds)
        {
            try
            {
                                var resourceRequest = new ResourceService.GetByIdRequest
                {
                    Id = resourceId.ToString()
                };
                var resource = await _resourceServiceClient.GetResourceByIdAsync(resourceRequest);
                if (resource != null && !string.IsNullOrEmpty(resource.Number))
                {
                    resourceNumbers.Add(resource.Number);
                }
            }
            catch
            {
                // If we can't get resource details, skip it
                continue;
            }
        }

        return resourceNumbers;
    }

    private async Task<ReservationAnalytics> CalculateReservationAnalyticsAsync(
        List<EnrichedReservationData> reservations,
        string startDate,
        string endDate
    )
    {
        var analytics = new ReservationAnalytics
        {
            StartDate = DateTime.Parse(startDate),
            EndDate = DateTime.Parse(endDate)
        };

        analytics.TotalDays = (analytics.EndDate - analytics.StartDate).Days + 1;

        if (!reservations.Any())
        {
            return analytics;
        }

        // Basic Statistics
        analytics.TotalReservations = reservations.Count;
        analytics.TotalRevenue = reservations.Sum(r => r.TotalAmount);
        analytics.NetRevenue = reservations.Sum(r => r.NetAmount);
        analytics.AverageReservationValue = analytics.TotalRevenue / analytics.TotalReservations;
        analytics.OrganizationReservations = reservations.Count(r => r.Source == "Organization");
        analytics.SunEasyReservations = reservations.Count(r => r.Source != "Organization");
        analytics.DailyAverageRevenue = analytics.TotalRevenue / analytics.TotalDays;

        // Determine primary currency (most common one)
        analytics.Currency = reservations
            .Where(r => !string.IsNullOrEmpty(r.Currency))
            .GroupBy(r => r.Currency)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault()?.Key ?? "EUR";

        // Status Analytics
        var statusGroups = reservations.GroupBy(r => r.StatusName).ToList();
        analytics.StatusBreakdown = statusGroups.Select(g => new StatusStat
        {
            StatusName = g.Key,
            Count = g.Count(),
            Percentage = (double)g.Count() / analytics.TotalReservations * 100,
            Revenue = g.Sum(r => r.TotalAmount)
        }).ToList();

        // Resource Analytics
        await CalculateResourceAnalyticsAsync(reservations, analytics);

        // Daily Sales Data
        analytics.DailySalesData = reservations
            .GroupBy(r => r.StartDate.Date)
            .Select(g => new DailySales
            {
                Date = g.Key,
                Revenue = g.Sum(r => r.TotalAmount),
                NetRevenue = g.Sum(r => r.NetAmount),
                ReservationCount = g.Count()
            })
            .OrderBy(d => d.Date)
            .ToList();

        // Peak Analysis
        if (analytics.DailySalesData.Any())
        {
            var busiestDay = analytics.DailySalesData.OrderByDescending(d => d.ReservationCount).First();
            var bestRevenueDay = analytics.DailySalesData.OrderByDescending(d => d.Revenue).First();

            analytics.BusiestDay = $"{busiestDay.Date:yyyy-MM-dd} ({busiestDay.ReservationCount} reservations)";
            analytics.BestRevenueDay = $"{bestRevenueDay.Date:yyyy-MM-dd} ({analytics.Currency}{bestRevenueDay.Revenue:F2})";
        }

        return analytics;
    }

    private async Task CreateUsersSheet(ClosedXML.Excel.XLWorkbook workbook, List<EnrichedReservationData> reservations, ReservationAnalytics analytics, string organizationId)
    {
        var worksheet = workbook.Worksheets.Add("Users");
        
        // Title
        worksheet.Cell(1, 1).Value = "Organization Users Performance";
        worksheet.Cell(1, 1).Style.Font.FontSize = 18;
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Range(1, 1, 1, 6).Merge();

        // Period info
        worksheet.Cell(2, 1).Value = $"Period: {analytics.StartDate:yyyy-MM-dd} to {analytics.EndDate:yyyy-MM-dd}";
        worksheet.Cell(2, 1).Style.Font.Italic = true;
        worksheet.Range(2, 1, 2, 6).Merge();

        var row = 4;

        // Get organization users from Organization service
        var organizationUserIds = new HashSet<string>(); // User IDs of organization users
        var organizationUserDetails = new Dictionary<string, (string Username, string Email)>();
        
        try
        {
            var orgUsersRequest = new OrganizationService.GetUserOfOrganizationRequest
            {
                OrganizationId = organizationId
            };
            var orgUsersResponse = await _organizationClient.GetUserOfOrganizationsAsync(orgUsersRequest);
            
            if (orgUsersResponse?.OrganizationUsers != null)
            {
                foreach (var orgUser in orgUsersResponse.OrganizationUsers)
                {
                    organizationUserIds.Add(orgUser.UserId);
                    if (orgUser.User != null)
                    {
                        organizationUserDetails[orgUser.UserId] = (
                            orgUser.User.Username ?? "Unknown User",
                            orgUser.User.Email ?? ""
                        );
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Log error but continue with empty list (will show no users)
            Console.WriteLine($"Error getting organization users: {ex.Message}");
        }

        // Get reservations with UserIds to match against organization users
        var reservationUserIds = await QueryAsync<dynamic>(
            @"SELECT ""Id"", ""UserId"", ""Code"" FROM ""Reservations"" 
              WHERE ""OrganizationId"" = @OrgId AND ""IsDeleted"" = false",
            new { OrgId = Guid.Parse(organizationId) }
        );

        // Create lookup from reservation code to userId
        var reservationUserLookup = reservationUserIds.ToDictionary(
            r => r.Code?.ToString() ?? "",
            r => r.UserId?.ToString() ?? ""
        );

        // Filter reservations to only include those made by organization users
        var orgStaffReservations = reservations
            .Where(r => 
                reservationUserLookup.ContainsKey(r.ReservationCode) &&
                organizationUserIds.Contains(reservationUserLookup[r.ReservationCode])
            )
            .ToList();

        // Calculate user performance from organization staff reservations only
        var userPerformance = new List<dynamic>();

        if (orgStaffReservations.Any())
        {
            // Group by userId (from lookup) and calculate performance
            var userGroups = orgStaffReservations
                .GroupBy(r => reservationUserLookup[r.ReservationCode])
                .ToList();

            userPerformance = userGroups.Select(g => {
                var userId = g.Key;
                var userDetails = organizationUserDetails.ContainsKey(userId) 
                    ? organizationUserDetails[userId] 
                    : ("Unknown User", "");

                return new {
                    Username = userDetails.Item1,
                    Email = userDetails.Item2,
                    ReservationCount = g.Count(),
                    TotalRevenue = g.Sum(r => r.TotalAmount),
                    NetRevenue = g.Sum(r => r.NetAmount),
                    AverageRevenue = g.Average(r => r.TotalAmount),
                    AverageNetRevenue = g.Average(r => r.NetAmount),
                    Currency = g.FirstOrDefault()?.Currency ?? analytics.Currency
                };
            })
            .OrderByDescending(u => u.TotalRevenue)
            .ToList<dynamic>();
        }

        if (userPerformance.Any())
        {
            // Add info about organization staff
            worksheet.Cell(row, 1).Value = $"📊 ORGANIZATION STAFF PERFORMANCE ({userPerformance.Count} staff members)";
            worksheet.Cell(row, 1).Style.Font.Bold = true;
            worksheet.Cell(row, 1).Style.Font.FontSize = 12;
            worksheet.Cell(row, 1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGreen;
            worksheet.Range(row, 1, row, 6).Merge();
            row += 2;

            // Headers
            worksheet.Cell(row, 1).Value = "Username";
            worksheet.Cell(row, 2).Value = "Email";
            worksheet.Cell(row, 3).Value = "Reservations";
            worksheet.Cell(row, 4).Value = "Total Revenue";
            worksheet.Cell(row, 5).Value = "Net Revenue";
            worksheet.Cell(row, 6).Value = "Avg Revenue/Reservation";

            // Style headers
            var headerRange = worksheet.Range(row, 1, row, 6);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightBlue;
            headerRange.Style.Border.OutsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
            headerRange.Style.Border.InsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
            row++;

            // Data rows
            foreach (var user in userPerformance)
            {
                worksheet.Cell(row, 1).Value = user.Username;
                worksheet.Cell(row, 2).Value = user.Email;
                worksheet.Cell(row, 3).Value = user.ReservationCount;
                worksheet.Cell(row, 4).Value = $"{user.Currency}{user.TotalRevenue:F2}";
                worksheet.Cell(row, 5).Value = $"{user.Currency}{user.NetRevenue:F2}";
                worksheet.Cell(row, 6).Value = $"{user.Currency}{user.AverageRevenue:F2}";
                
                // Alternate row colors for better readability
                if ((row - 5) % 2 == 0)
                {
                    worksheet.Range(row, 1, row, 6).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGray;
                }
                
                row++;
            }

            // Summary section
            row += 2;
            worksheet.Cell(row, 1).Value = "📊 USERS SUMMARY";
            worksheet.Cell(row, 1).Style.Font.Bold = true;
            worksheet.Cell(row, 1).Style.Font.FontSize = 14;
            worksheet.Cell(row, 1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.Orange;
            worksheet.Range(row, 1, row, 6).Merge();
            row += 2;

            var totalUsers = userPerformance.Count;
            var totalUserRevenue = userPerformance.Sum(u => (double)u.TotalRevenue);
            var totalUserNetRevenue = userPerformance.Sum(u => (double)u.NetRevenue);
            var totalUserReservations = userPerformance.Sum(u => (int)u.ReservationCount);
            var avgRevenuePerUser = totalUserRevenue / totalUsers;
            var avgNetRevenuePerUser = totalUserNetRevenue / totalUsers;

            worksheet.Cell(row, 1).Value = "Active Users:";
            worksheet.Cell(row, 2).Value = totalUsers;
            worksheet.Cell(row, 3).Value = "Total User Revenue:";
            worksheet.Cell(row, 4).Value = $"{analytics.Currency}{totalUserRevenue:F2}";
            row++;

            worksheet.Cell(row, 1).Value = "Total User Net Revenue:";
            worksheet.Cell(row, 2).Value = $"{analytics.Currency}{totalUserNetRevenue:F2}";
            worksheet.Cell(row, 3).Value = "Total User Reservations:";
            worksheet.Cell(row, 4).Value = totalUserReservations;
            row++;

            worksheet.Cell(row, 1).Value = "Avg Revenue/User:";
            worksheet.Cell(row, 2).Value = $"{analytics.Currency}{avgRevenuePerUser:F2}";
            worksheet.Cell(row, 3).Value = "Avg Net Revenue/User:";
            worksheet.Cell(row, 4).Value = $"{analytics.Currency}{avgNetRevenuePerUser:F2}";
            row += 2;

            // Top performers
            if (userPerformance.Any())
            {
                worksheet.Cell(row, 1).Value = "🏆 TOP PERFORMERS";
                worksheet.Cell(row, 1).Style.Font.Bold = true;
                worksheet.Cell(row, 1).Style.Font.FontSize = 12;
                worksheet.Cell(row, 1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.Gold;
                worksheet.Range(row, 1, row, 6).Merge();
                row += 2;

                var topRevenue = userPerformance.First();
                var topReservations = userPerformance.OrderByDescending(u => u.ReservationCount).First();
                var topAverage = userPerformance.OrderByDescending(u => u.AverageRevenue).First();
                var topNet = userPerformance.OrderByDescending(u => u.NetRevenue).First();

                worksheet.Cell(row, 1).Value = "Highest Revenue:";
                worksheet.Cell(row, 2).Value = $"{topRevenue.Username} ({topRevenue.Currency}{topRevenue.TotalRevenue:F2})";
                worksheet.Range(row, 2, row, 6).Merge();
                row++;

                worksheet.Cell(row, 1).Value = "Highest Net Revenue:";
                worksheet.Cell(row, 2).Value = $"{topNet.Username} ({topNet.Currency}{topNet.NetRevenue:F2})";
                worksheet.Range(row, 2, row, 6).Merge();
                row++;

                worksheet.Cell(row, 1).Value = "Most Reservations:";
                worksheet.Cell(row, 2).Value = $"{topReservations.Username} ({topReservations.ReservationCount} reservations)";
                worksheet.Range(row, 2, row, 6).Merge();
                row++;

                worksheet.Cell(row, 1).Value = "Highest Average:";
                worksheet.Cell(row, 2).Value = $"{topAverage.Username} ({topAverage.Currency}{topAverage.AverageRevenue:F2}/reservation)";
                worksheet.Range(row, 2, row, 6).Merge();
            }
        }
        else
        {
            worksheet.Cell(row, 1).Value = "No organization staff reservations found for this period.";
            worksheet.Cell(row, 1).Style.Font.Italic = true;
            worksheet.Cell(row, 1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightYellow;
            worksheet.Range(row, 1, row, 6).Merge();
            row += 2;
            
            worksheet.Cell(row, 1).Value = "ℹ️ Note: This sheet only shows reservations made by organization staff members, not customer reservations.";
            worksheet.Cell(row, 1).Style.Font.Italic = true;
            worksheet.Cell(row, 1).Style.Font.FontSize = 10;
            worksheet.Range(row, 1, row, 6).Merge();
        }

        // Auto-fit columns
        worksheet.Columns().AdjustToContents();
    }

    private async Task CalculateResourceAnalyticsAsync(
        List<EnrichedReservationData> reservations,
        ReservationAnalytics analytics
    )
    {
        // Group by resource numbers
        var resourceGroups = reservations
            .Where(r => !string.IsNullOrEmpty(r.ResourceNumbers))
            .SelectMany(r => r.ResourceNumbers.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(resourceNum => new { ResourceNumber = resourceNum.Trim(), Reservation = r }))
            .GroupBy(x => x.ResourceNumber)
            .ToList();

        if (resourceGroups.Any())
        {
            // Most Reserved Resource
            var mostReserved = resourceGroups
                .OrderByDescending(g => g.Count())
                .First();

            analytics.MostReservedResource = new ResourceStat
            {
                ResourceNumber = mostReserved.Key,
                ReservationCount = mostReserved.Count(),
                TotalRevenue = mostReserved.Sum(x => x.Reservation.TotalAmount),
                AverageValue = mostReserved.Average(x => x.Reservation.TotalAmount)
            };

            // Most Profitable Resource
            var mostProfitable = resourceGroups
                .OrderByDescending(g => g.Sum(x => x.Reservation.TotalAmount))
                .First();

            analytics.MostProfitableResource = new ResourceStat
            {
                ResourceNumber = mostProfitable.Key,
                ReservationCount = mostProfitable.Count(),
                TotalRevenue = mostProfitable.Sum(x => x.Reservation.TotalAmount),
                AverageValue = mostProfitable.Average(x => x.Reservation.TotalAmount)
            };
        }
    }
}

public class ReservationQueryResult
{
    public Guid ReservationId { get; set; }
    public string ReservationCode { get; set; } = string.Empty;
    public Guid OrganizationId { get; set; }
    public Guid UserId { get; set; }
    public Guid StatusId { get; set; }
    public string Source { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal NetAmount { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
}

public static class ExcelReportHelper
{
    public static byte[] GenerateReservationExcel(List<Models.Reservation> reservations)
    {
        using (var workbook = new ClosedXML.Excel.XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("Reservations");
            worksheet.Cell(1, 1).Value = "ReservationId";
            worksheet.Cell(1, 2).Value = "OrganizationId";
            worksheet.Cell(1, 3).Value = "UserId";
            worksheet.Cell(1, 4).Value = "StatusId";
            worksheet.Cell(1, 5).Value = "TotalAmount";
            worksheet.Cell(1, 6).Value = "StartDate";
            worksheet.Cell(1, 7).Value = "EndDate";
            worksheet.Cell(1, 8).Value = "Code";
            int row = 2;
            foreach (var reservation in reservations)
            {
                worksheet.Cell(row, 1).Value = reservation.Id.ToString();
                worksheet.Cell(row, 2).Value = reservation.OrganizationId.ToString();
                worksheet.Cell(row, 3).Value = reservation.UserId.ToString();
                worksheet.Cell(row, 4).Value = reservation.StatusId.ToString();
                worksheet.Cell(row, 5).Value = reservation.TotalAmount;
                worksheet.Cell(row, 6).Value = reservation.StartDate.ToString("u");
                worksheet.Cell(row, 7).Value = reservation.EndDate.ToString("u");
                worksheet.Cell(row, 8).Value = reservation.Code;
                row++;
            }
            using (var stream = new System.IO.MemoryStream())
            {
                workbook.SaveAs(stream);
                return stream.ToArray();
            }
        }
    }

    public static byte[] GenerateEnrichedReservationExcel(List<EnrichedReservationData> reservations)
    {
        using (var workbook = new ClosedXML.Excel.XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("Reservations");

            // Set headers with improved column names
            worksheet.Cell(1, 1).Value = "Reservation Code";
            worksheet.Cell(1, 2).Value = "Organization Name";
            worksheet.Cell(1, 3).Value = "Username";
            worksheet.Cell(1, 4).Value = "User Email";
            worksheet.Cell(1, 5).Value = "Status";
            worksheet.Cell(1, 6).Value = "Source";
            worksheet.Cell(1, 7).Value = "Total Amount";
            worksheet.Cell(1, 8).Value = "Net Amount";
            worksheet.Cell(1, 9).Value = "Currency";
            worksheet.Cell(1, 10).Value = "Resource Numbers";
            worksheet.Cell(1, 11).Value = "Start Date";
            worksheet.Cell(1, 12).Value = "End Date";
            worksheet.Cell(1, 13).Value = "Customer Name";
            worksheet.Cell(1, 14).Value = "Customer Phone";
            worksheet.Cell(1, 15).Value = "Customer Email";
            worksheet.Cell(1, 16).Value = "Notes";

            // Style the headers
            var headerRange = worksheet.Range(1, 1, 1, 16);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGray;
            headerRange.Style.Border.OutsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
            headerRange.Style.Border.InsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;

            int row = 2;
            foreach (var reservation in reservations)
            {
                worksheet.Cell(row, 1).Value = reservation.ReservationCode;
                worksheet.Cell(row, 2).Value = reservation.OrganizationName;
                worksheet.Cell(row, 3).Value = reservation.Username;
                worksheet.Cell(row, 4).Value = reservation.UserEmail;
                worksheet.Cell(row, 5).Value = reservation.StatusName;
                worksheet.Cell(row, 6).Value = reservation.Source;
                worksheet.Cell(row, 7).Value = reservation.TotalAmount;
                worksheet.Cell(row, 8).Value = reservation.NetAmount;
                worksheet.Cell(row, 9).Value = reservation.Currency;
                worksheet.Cell(row, 10).Value = reservation.ResourceNumbers;
                worksheet.Cell(row, 11).Value = reservation.StartDate.ToString("yyyy-MM-dd HH:mm");
                worksheet.Cell(row, 12).Value = reservation.EndDate.ToString("yyyy-MM-dd HH:mm");
                worksheet.Cell(row, 13).Value = reservation.CustomerName;
                worksheet.Cell(row, 14).Value = reservation.CustomerPhone;
                worksheet.Cell(row, 15).Value = reservation.CustomerEmail;
                worksheet.Cell(row, 16).Value = reservation.Notes;
                row++;
            }

            // Auto-fit columns
            worksheet.Columns().AdjustToContents();

            using (var stream = new System.IO.MemoryStream())
            {
                workbook.SaveAs(stream);
                return stream.ToArray();
            }
        }
    }



    public static void CreateSummarySheet(ClosedXML.Excel.XLWorkbook workbook, ReservationAnalytics analytics)
    {
        var worksheet = workbook.Worksheets.Add("Summary & Analytics");

        // Title
        worksheet.Cell(1, 1).Value = "Reservation Report Summary";
        worksheet.Cell(1, 1).Style.Font.FontSize = 18;
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Range(1, 1, 1, 4).Merge();

        // Date Range
        worksheet.Cell(2, 1).Value = $"Report Period: {analytics.StartDate:yyyy-MM-dd} to {analytics.EndDate:yyyy-MM-dd}";
        worksheet.Cell(2, 1).Style.Font.Italic = true;
        worksheet.Range(2, 1, 2, 4).Merge();

        var row = 4;

        // Key Metrics Section
        worksheet.Cell(row, 1).Value = "KEY METRICS";
        worksheet.Cell(row, 1).Style.Font.Bold = true;
        worksheet.Cell(row, 1).Style.Font.FontSize = 14;
        worksheet.Cell(row, 1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightBlue;
        worksheet.Range(row, 1, row, 4).Merge();
        row += 2;

        worksheet.Cell(row, 1).Value = "Total Reservations:";
        worksheet.Cell(row, 2).Value = analytics.TotalReservations;
        worksheet.Cell(row, 3).Value = "Total Revenue:";
        worksheet.Cell(row, 4).Value = $"{analytics.Currency}{analytics.TotalRevenue:F2}";
        row++;

        worksheet.Cell(row, 1).Value = "Net Revenue:";
        worksheet.Cell(row, 2).Value = $"{analytics.Currency}{analytics.NetRevenue:F2}";
        worksheet.Cell(row, 3).Value = "Average Value:";
        worksheet.Cell(row, 4).Value = $"{analytics.Currency}{analytics.AverageReservationValue:F2}";
        row++;

        worksheet.Cell(row, 1).Value = "Daily Average:";
        worksheet.Cell(row, 2).Value = $"{analytics.Currency}{analytics.DailyAverageRevenue:F2}";
        worksheet.Cell(row, 3).Value = "Total Days:";
        worksheet.Cell(row, 4).Value = analytics.TotalDays;
        row += 2;

        // Source Breakdown
        worksheet.Cell(row, 1).Value = "RESERVATION SOURCES";
        worksheet.Cell(row, 1).Style.Font.Bold = true;
        worksheet.Cell(row, 1).Style.Font.FontSize = 14;
        worksheet.Cell(row, 1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGreen;
        worksheet.Range(row, 1, row, 4).Merge();
        row += 2;

        worksheet.Cell(row, 1).Value = "Organization Bookings:";
        worksheet.Cell(row, 2).Value = analytics.OrganizationReservations;
        worksheet.Cell(row, 3).Value = "SunEasy Bookings:";
        worksheet.Cell(row, 4).Value = analytics.SunEasyReservations;
        row += 2;

        // Resource Analytics
        worksheet.Cell(row, 1).Value = "TOP PERFORMING RESOURCES";
        worksheet.Cell(row, 1).Style.Font.Bold = true;
        worksheet.Cell(row, 1).Style.Font.FontSize = 14;
        worksheet.Cell(row, 1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightYellow;
        worksheet.Range(row, 1, row, 4).Merge();
        row += 2;

        if (!string.IsNullOrEmpty(analytics.MostReservedResource.ResourceNumber))
        {
            worksheet.Cell(row, 1).Value = "Most Reserved:";
            worksheet.Cell(row, 2).Value = $"{analytics.MostReservedResource.ResourceNumber} ({analytics.MostReservedResource.ReservationCount} bookings)";
            worksheet.Range(row, 2, row, 4).Merge();
            row++;

            worksheet.Cell(row, 1).Value = "Most Profitable:";
            worksheet.Cell(row, 2).Value = $"{analytics.MostProfitableResource.ResourceNumber} ({analytics.Currency}{analytics.MostProfitableResource.TotalRevenue:F2})";
            worksheet.Range(row, 2, row, 4).Merge();
            row += 2;
        }

        // Peak Performance
        worksheet.Cell(row, 1).Value = "PEAK PERFORMANCE";
        worksheet.Cell(row, 1).Style.Font.Bold = true;
        worksheet.Cell(row, 1).Style.Font.FontSize = 14;
        worksheet.Cell(row, 1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightCoral;
        worksheet.Range(row, 1, row, 4).Merge();
        row += 2;

        worksheet.Cell(row, 1).Value = "Busiest Day:";
        worksheet.Cell(row, 2).Value = analytics.BusiestDay;
        worksheet.Range(row, 2, row, 4).Merge();
        row++;

        worksheet.Cell(row, 1).Value = "Best Revenue Day:";
        worksheet.Cell(row, 2).Value = analytics.BestRevenueDay;
        worksheet.Range(row, 2, row, 4).Merge();
        row += 2;

        // Status Breakdown
        if (analytics.StatusBreakdown.Any())
        {
            worksheet.Cell(row, 1).Value = "STATUS BREAKDOWN";
            worksheet.Cell(row, 1).Style.Font.Bold = true;
            worksheet.Cell(row, 1).Style.Font.FontSize = 14;
            worksheet.Cell(row, 1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGray;
            worksheet.Range(row, 1, row, 4).Merge();
            row += 2;

            worksheet.Cell(row, 1).Value = "Status";
            worksheet.Cell(row, 2).Value = "Count";
            worksheet.Cell(row, 3).Value = "Percentage";
            worksheet.Cell(row, 4).Value = "Revenue";

            var headerRange = worksheet.Range(row, 1, row, 4);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGray;
            row++;

            foreach (var status in analytics.StatusBreakdown.OrderByDescending(s => s.Count))
            {
                worksheet.Cell(row, 1).Value = status.StatusName;
                worksheet.Cell(row, 2).Value = status.Count;
                worksheet.Cell(row, 3).Value = $"{status.Percentage:F1}%";
                worksheet.Cell(row, 4).Value = $"{analytics.Currency}{status.Revenue:F2}";
                row++;
            }
        }

        // Auto-fit columns
        worksheet.Columns().AdjustToContents();
    }

    public static void CreateChartsSheet(ClosedXML.Excel.XLWorkbook workbook, ReservationAnalytics analytics)
    {
        var worksheet = workbook.Worksheets.Add("Sales Chart");

        // Title
        worksheet.Cell(1, 1).Value = "Daily Sales Performance";
        worksheet.Cell(1, 1).Style.Font.FontSize = 16;
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Range(1, 1, 1, 4).Merge();

        // Chart data headers
        var row = 3;
        worksheet.Cell(row, 1).Value = "Date";
        worksheet.Cell(row, 2).Value = "Revenue";
        worksheet.Cell(row, 3).Value = "Net Revenue";
        worksheet.Cell(row, 4).Value = "Reservations";

        var headerRange = worksheet.Range(row, 1, row, 4);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightBlue;
        headerRange.Style.Border.OutsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
        row++;

        // Chart data
        foreach (var dailySale in analytics.DailySalesData)
        {
            worksheet.Cell(row, 1).Value = dailySale.Date.ToString("yyyy-MM-dd");
            worksheet.Cell(row, 2).Value = dailySale.Revenue;
            worksheet.Cell(row, 3).Value = dailySale.NetRevenue;
            worksheet.Cell(row, 4).Value = dailySale.ReservationCount;
            row++;
        }

        // Add a simple chart (ClosedXML has limited charting, but we can create data for external charting)
        var chartStartRow = row + 2;
        worksheet.Cell(chartStartRow, 1).Value = "📊 SALES TRENDS";
        worksheet.Cell(chartStartRow, 1).Style.Font.Bold = true;
        worksheet.Cell(chartStartRow, 1).Style.Font.FontSize = 14;

        chartStartRow += 2;
        if (analytics.DailySalesData.Any())
        {
            var maxRevenue = analytics.DailySalesData.Max(d => d.Revenue);
            var minRevenue = analytics.DailySalesData.Min(d => d.Revenue);
            var avgRevenue = analytics.DailySalesData.Average(d => d.Revenue);

            worksheet.Cell(chartStartRow, 1).Value = "Highest Daily Revenue:";
            worksheet.Cell(chartStartRow, 2).Value = $"{analytics.Currency}{maxRevenue:F2}";
            chartStartRow++;

            worksheet.Cell(chartStartRow, 1).Value = "Lowest Daily Revenue:";
            worksheet.Cell(chartStartRow, 2).Value = $"{analytics.Currency}{minRevenue:F2}";
            chartStartRow++;

            worksheet.Cell(chartStartRow, 1).Value = "Average Daily Revenue:";
            worksheet.Cell(chartStartRow, 2).Value = $"{analytics.Currency}{avgRevenue:F2}";
            chartStartRow++;

            // Simple trend indicator
            var firstHalf = analytics.DailySalesData.Take(analytics.DailySalesData.Count / 2).Average(d => d.Revenue);
            var secondHalf = analytics.DailySalesData.Skip(analytics.DailySalesData.Count / 2).Average(d => d.Revenue);
            var trend = secondHalf > firstHalf ? "📈 Increasing" : secondHalf < firstHalf ? "📉 Decreasing" : "➡️ Stable";

            worksheet.Cell(chartStartRow, 1).Value = "Trend:";
            worksheet.Cell(chartStartRow, 2).Value = trend;
        }

        // Auto-fit columns
        worksheet.Columns().AdjustToContents();
    }



    public static void CreateDataSheet(ClosedXML.Excel.XLWorkbook workbook, List<EnrichedReservationData> reservations)
    {
        var worksheet = workbook.Worksheets.Add("Reservations Data");

        // Set headers with improved column names
        worksheet.Cell(1, 1).Value = "Reservation Code";
        worksheet.Cell(1, 2).Value = "Organization Name";
        worksheet.Cell(1, 3).Value = "Username";
        worksheet.Cell(1, 4).Value = "User Email";
        worksheet.Cell(1, 5).Value = "Status";
        worksheet.Cell(1, 6).Value = "Source";
        worksheet.Cell(1, 7).Value = "Total Amount";
        worksheet.Cell(1, 8).Value = "Net Amount";
        worksheet.Cell(1, 9).Value = "Currency";
        worksheet.Cell(1, 10).Value = "Resource Numbers";
        worksheet.Cell(1, 11).Value = "Start Date";
        worksheet.Cell(1, 12).Value = "End Date";
        worksheet.Cell(1, 13).Value = "Customer Name";
        worksheet.Cell(1, 14).Value = "Customer Phone";
        worksheet.Cell(1, 15).Value = "Customer Email";
        worksheet.Cell(1, 16).Value = "Notes";

        // Style the headers
        var headerRange = worksheet.Range(1, 1, 1, 16);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGray;
        headerRange.Style.Border.OutsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
        headerRange.Style.Border.InsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;

        int row = 2;
        foreach (var reservation in reservations)
        {
            worksheet.Cell(row, 1).Value = reservation.ReservationCode;
            worksheet.Cell(row, 2).Value = reservation.OrganizationName;
            worksheet.Cell(row, 3).Value = reservation.Username;
            worksheet.Cell(row, 4).Value = reservation.UserEmail;
            worksheet.Cell(row, 5).Value = reservation.StatusName;
            worksheet.Cell(row, 6).Value = reservation.Source;
            worksheet.Cell(row, 7).Value = reservation.TotalAmount;
            worksheet.Cell(row, 8).Value = reservation.NetAmount;
            worksheet.Cell(row, 9).Value = reservation.Currency;
            worksheet.Cell(row, 10).Value = reservation.ResourceNumbers;
            worksheet.Cell(row, 11).Value = reservation.StartDate.ToString("yyyy-MM-dd HH:mm");
            worksheet.Cell(row, 12).Value = reservation.EndDate.ToString("yyyy-MM-dd HH:mm");
            worksheet.Cell(row, 13).Value = reservation.CustomerName;
            worksheet.Cell(row, 14).Value = reservation.CustomerPhone;
            worksheet.Cell(row, 15).Value = reservation.CustomerEmail;
            worksheet.Cell(row, 16).Value = reservation.Notes;
            row++;
        }

        // Auto-fit columns
        worksheet.Columns().AdjustToContents();
    }

    public static void CreateResourceDataSheet(ClosedXML.Excel.XLWorkbook workbook, List<EnrichedReservationData> reservations, ReservationAnalytics analytics)
    {
        var worksheet = workbook.Worksheets.Add("Resource Data");

        // Title
        worksheet.Cell(1, 1).Value = "Resource Performance Analysis";
        worksheet.Cell(1, 1).Style.Font.FontSize = 18;
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Range(1, 1, 1, 6).Merge();

        // Period info
        worksheet.Cell(2, 1).Value = $"Period: {analytics.StartDate:yyyy-MM-dd} to {analytics.EndDate:yyyy-MM-dd}";
        worksheet.Cell(2, 1).Style.Font.Italic = true;
        worksheet.Range(2, 1, 2, 6).Merge();

        var row = 4;

        // Calculate resource performance data
        var resourcePerformance = reservations
            .Where(r => !string.IsNullOrEmpty(r.ResourceNumbers))
            .SelectMany(r => r.ResourceNumbers.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(resourceNum => new {
                    ResourceNumber = resourceNum.Trim(),
                    TotalAmount = r.TotalAmount,
                    NetAmount = r.NetAmount,
                    Currency = r.Currency
                }))
            .GroupBy(x => x.ResourceNumber)
            .Select(g => new {
                ResourceNumber = g.Key,
                SalesCount = g.Count(),
                TotalRevenue = g.Sum(x => x.TotalAmount),
                NetRevenue = g.Sum(x => x.NetAmount),
                AverageRevenue = g.Average(x => x.TotalAmount),
                AverageNetRevenue = g.Average(x => x.NetAmount)
            })
            .OrderByDescending(x => x.TotalRevenue)
            .ToList();

        // Headers
        worksheet.Cell(row, 1).Value = "Resource Number";
        worksheet.Cell(row, 2).Value = "Sales Count";
        worksheet.Cell(row, 3).Value = "Total Revenue";
        worksheet.Cell(row, 4).Value = "Net Revenue";
        worksheet.Cell(row, 5).Value = "Avg Revenue/Sale";
        worksheet.Cell(row, 6).Value = "Avg Net/Sale";

        // Style headers
        var headerRange = worksheet.Range(row, 1, row, 6);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightBlue;
        headerRange.Style.Border.OutsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
        headerRange.Style.Border.InsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
        row++;

        // Data rows
        foreach (var resource in resourcePerformance)
        {
            worksheet.Cell(row, 1).Value = resource.ResourceNumber;
            worksheet.Cell(row, 2).Value = resource.SalesCount;
            worksheet.Cell(row, 3).Value = $"{analytics.Currency}{resource.TotalRevenue:F2}";
            worksheet.Cell(row, 4).Value = $"{analytics.Currency}{resource.NetRevenue:F2}";
            worksheet.Cell(row, 5).Value = $"{analytics.Currency}{resource.AverageRevenue:F2}";
            worksheet.Cell(row, 6).Value = $"{analytics.Currency}{resource.AverageNetRevenue:F2}";

            // Alternate row colors for better readability
            if ((row - 5) % 2 == 0)
            {
                worksheet.Range(row, 1, row, 6).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGray;
            }

            row++;
        }

        // Summary section
        row += 2;
        worksheet.Cell(row, 1).Value = "RESOURCE SUMMARY";
        worksheet.Cell(row, 1).Style.Font.Bold = true;
        worksheet.Cell(row, 1).Style.Font.FontSize = 14;
        worksheet.Cell(row, 1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.Orange;
        worksheet.Range(row, 1, row, 6).Merge();
        row += 2;

        if (resourcePerformance.Any())
        {
            var totalResources = resourcePerformance.Count;
            var totalSales = resourcePerformance.Sum(r => (int)r.SalesCount);
            var totalRevenue = resourcePerformance.Sum(r => (double)r.TotalRevenue);
            var avgSalesPerResource = totalSales / (double)totalResources;
            var avgRevenuePerResource = totalRevenue / totalResources;

            worksheet.Cell(row, 1).Value = "Total Resources:";
            worksheet.Cell(row, 2).Value = totalResources;
            worksheet.Cell(row, 3).Value = "Total Sales:";
            worksheet.Cell(row, 4).Value = totalSales;
            row++;

            worksheet.Cell(row, 1).Value = "Total Revenue:";
            worksheet.Cell(row, 2).Value = $"{analytics.Currency}{totalRevenue:F2}";
            worksheet.Cell(row, 3).Value = "Avg Sales/Resource:";
            worksheet.Cell(row, 4).Value = $"{avgSalesPerResource:F1}";
            row++;

            worksheet.Cell(row, 1).Value = "Avg Revenue/Resource:";
            worksheet.Cell(row, 2).Value = $"{analytics.Currency}{avgRevenuePerResource:F2}";
            row += 2;

            // Top performers
            worksheet.Cell(row, 1).Value = "🏆 TOP PERFORMERS";
            worksheet.Cell(row, 1).Style.Font.Bold = true;
            worksheet.Cell(row, 1).Style.Font.FontSize = 12;
            worksheet.Cell(row, 1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.Gold;
            worksheet.Range(row, 1, row, 6).Merge();
            row += 2;

            worksheet.Cell(row, 1).Value = "Highest Revenue:";
            worksheet.Cell(row, 2).Value = $"{resourcePerformance.First().ResourceNumber} ({analytics.Currency}{resourcePerformance.First().TotalRevenue:F2})";
            worksheet.Range(row, 2, row, 6).Merge();
            row++;

            var mostSales = resourcePerformance.OrderByDescending(r => r.SalesCount).First();
            worksheet.Cell(row, 1).Value = "Most Sales:";
            worksheet.Cell(row, 2).Value = $"{mostSales.ResourceNumber} ({mostSales.SalesCount} sales)";
            worksheet.Range(row, 2, row, 6).Merge();
            row++;

            var highestAvg = resourcePerformance.OrderByDescending(r => r.AverageRevenue).First();
            worksheet.Cell(row, 1).Value = "Highest Avg/Sale:";
            worksheet.Cell(row, 2).Value = $"{highestAvg.ResourceNumber} ({analytics.Currency}{highestAvg.AverageRevenue:F2})";
            worksheet.Range(row, 2, row, 6).Merge();
        }

        // Auto-fit columns
        worksheet.Columns().AdjustToContents();
    }

    public static void CreateSunEasyDataSheet(ClosedXML.Excel.XLWorkbook workbook, List<EnrichedReservationData> reservations, ReservationAnalytics analytics)
    {
        var worksheet = workbook.Worksheets.Add("From SunEasy");

        // Filter only SunEasy reservations (source != "Organization")
        var sunEasyReservations = reservations.Where(r => r.Source != "Organization").ToList();

        // Title
        worksheet.Cell(1, 1).Value = "SunEasy Platform Reservations & Resources";
        worksheet.Cell(1, 1).Style.Font.FontSize = 18;
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Range(1, 1, 1, 8).Merge();

        // Period and summary info
        worksheet.Cell(2, 1).Value = $"Period: {analytics.StartDate:yyyy-MM-dd} to {analytics.EndDate:yyyy-MM-dd} | Total SunEasy Reservations: {sunEasyReservations.Count}";
        worksheet.Cell(2, 1).Style.Font.Italic = true;
        worksheet.Range(2, 1, 2, 8).Merge();

        var row = 4;

        // SECTION 1: SunEasy Reservations Table
        worksheet.Cell(row, 1).Value = "📱 SUNEASY PLATFORM RESERVATIONS";
        worksheet.Cell(row, 1).Style.Font.Bold = true;
        worksheet.Cell(row, 1).Style.Font.FontSize = 14;
        worksheet.Cell(row, 1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightBlue;
        worksheet.Range(row, 1, row, 8).Merge();
        row += 2;

        if (sunEasyReservations.Any())
        {
            // Reservations table headers
            worksheet.Cell(row, 1).Value = "Reservation Code";
            worksheet.Cell(row, 2).Value = "Username";
            worksheet.Cell(row, 3).Value = "User Email";
            worksheet.Cell(row, 4).Value = "Status";
            worksheet.Cell(row, 5).Value = "Total Amount";
            worksheet.Cell(row, 6).Value = "Net Amount";
            worksheet.Cell(row, 7).Value = "Resource Numbers";
            worksheet.Cell(row, 8).Value = "Start Date";

            // Style reservation headers
            var reservationHeaderRange = worksheet.Range(row, 1, row, 8);
            reservationHeaderRange.Style.Font.Bold = true;
            reservationHeaderRange.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGray;
            reservationHeaderRange.Style.Border.OutsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
            reservationHeaderRange.Style.Border.InsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
            row++;

            // Reservations data
            foreach (var reservation in sunEasyReservations.OrderByDescending(r => r.TotalAmount))
            {
                worksheet.Cell(row, 1).Value = reservation.ReservationCode;
                worksheet.Cell(row, 2).Value = reservation.Username;
                worksheet.Cell(row, 3).Value = reservation.UserEmail;
                worksheet.Cell(row, 4).Value = reservation.StatusName;
                worksheet.Cell(row, 5).Value = $"{analytics.Currency}{reservation.TotalAmount:F2}";
                worksheet.Cell(row, 6).Value = $"{analytics.Currency}{reservation.NetAmount:F2}";
                worksheet.Cell(row, 7).Value = reservation.ResourceNumbers;
                worksheet.Cell(row, 8).Value = reservation.StartDate.ToString("yyyy-MM-dd");

                // Alternate row colors
                if ((row - (sunEasyReservations.Count > 0 ? row - sunEasyReservations.Count + 1 : row)) % 2 == 0)
                {
                    worksheet.Range(row, 1, row, 8).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGray;
                }

                row++;
            }
        }
        else
        {
            worksheet.Cell(row, 1).Value = "No SunEasy reservations found in this period.";
            worksheet.Cell(row, 1).Style.Font.Italic = true;
            worksheet.Range(row, 1, row, 8).Merge();
            row++;
        }

        row += 3;

        // SECTION 2: SunEasy Resource Performance
        worksheet.Cell(row, 1).Value = "🏖️ SUNEASY RESOURCE PERFORMANCE";
        worksheet.Cell(row, 1).Style.Font.Bold = true;
        worksheet.Cell(row, 1).Style.Font.FontSize = 14;
        worksheet.Cell(row, 1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGreen;
        worksheet.Range(row, 1, row, 6).Merge();
        row += 2;

        if (sunEasyReservations.Any())
        {
            // Calculate SunEasy resource performance
            var sunEasyResourcePerformance = sunEasyReservations
                .Where(r => !string.IsNullOrEmpty(r.ResourceNumbers))
                .SelectMany(r => r.ResourceNumbers.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(resourceNum => new {
                        ResourceNumber = resourceNum.Trim(),
                        TotalAmount = r.TotalAmount,
                        NetAmount = r.NetAmount
                    }))
                .GroupBy(x => x.ResourceNumber)
                .Select(g => new {
                    ResourceNumber = g.Key,
                    SalesCount = g.Count(),
                    TotalRevenue = g.Sum(x => x.TotalAmount),
                    NetRevenue = g.Sum(x => x.NetAmount),
                    AverageRevenue = g.Average(x => x.TotalAmount),
                    AverageNetRevenue = g.Average(x => x.NetAmount)
                })
                .OrderByDescending(x => x.TotalRevenue)
                .ToList();

            // Resource performance table headers
            worksheet.Cell(row, 1).Value = "Resource Number";
            worksheet.Cell(row, 2).Value = "SunEasy Sales";
            worksheet.Cell(row, 3).Value = "Total Revenue";
            worksheet.Cell(row, 4).Value = "Net Revenue";
            worksheet.Cell(row, 5).Value = "Avg Revenue/Sale";
            worksheet.Cell(row, 6).Value = "Avg Net/Sale";

            // Style resource headers
            var resourceHeaderRange = worksheet.Range(row, 1, row, 6);
            resourceHeaderRange.Style.Font.Bold = true;
            resourceHeaderRange.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGreen;
            resourceHeaderRange.Style.Border.OutsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
            resourceHeaderRange.Style.Border.InsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
            row++;

            // Resource performance data
            foreach (var resource in sunEasyResourcePerformance)
            {
                worksheet.Cell(row, 1).Value = resource.ResourceNumber;
                worksheet.Cell(row, 2).Value = resource.SalesCount;
                worksheet.Cell(row, 3).Value = $"{analytics.Currency}{resource.TotalRevenue:F2}";
                worksheet.Cell(row, 4).Value = $"{analytics.Currency}{resource.NetRevenue:F2}";
                worksheet.Cell(row, 5).Value = $"{analytics.Currency}{resource.AverageRevenue:F2}";
                worksheet.Cell(row, 6).Value = $"{analytics.Currency}{resource.AverageNetRevenue:F2}";

                // Alternate row colors
                if ((row - (sunEasyResourcePerformance.Count > 0 ? row - sunEasyResourcePerformance.Count + 1 : row)) % 2 == 0)
                {
                    worksheet.Range(row, 1, row, 6).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGray;
                }

                row++;
            }

            // SunEasy Summary Statistics
            row += 2;
            worksheet.Cell(row, 1).Value = "📊 SUNEASY SUMMARY";
            worksheet.Cell(row, 1).Style.Font.Bold = true;
            worksheet.Cell(row, 1).Style.Font.FontSize = 12;
            worksheet.Cell(row, 1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.Orange;
            worksheet.Range(row, 1, row, 6).Merge();
            row += 2;

            var totalSunEasyRevenue = sunEasyReservations.Sum(r => r.TotalAmount);
            var totalSunEasyNetRevenue = sunEasyReservations.Sum(r => r.NetAmount);
            var avgSunEasyReservationValue = sunEasyReservations.Average(r => r.TotalAmount);
            var totalSunEasyResources = sunEasyResourcePerformance.Count;
            var totalSunEasySales = sunEasyResourcePerformance.Sum(r => (int)r.SalesCount);

            worksheet.Cell(row, 1).Value = "Total SunEasy Revenue:";
            worksheet.Cell(row, 2).Value = $"{analytics.Currency}{totalSunEasyRevenue:F2}";
            worksheet.Cell(row, 3).Value = "Total Net Revenue:";
            worksheet.Cell(row, 4).Value = $"{analytics.Currency}{totalSunEasyNetRevenue:F2}";
            row++;

            worksheet.Cell(row, 1).Value = "Avg Reservation Value:";
            worksheet.Cell(row, 2).Value = $"{analytics.Currency}{avgSunEasyReservationValue:F2}";
            worksheet.Cell(row, 3).Value = "Platform Fee Revenue:";
            worksheet.Cell(row, 4).Value = $"{analytics.Currency}{(totalSunEasyRevenue - totalSunEasyNetRevenue):F2}";
            row++;

            worksheet.Cell(row, 1).Value = "Resources Used:";
            worksheet.Cell(row, 2).Value = totalSunEasyResources;
            worksheet.Cell(row, 3).Value = "Total Bookings:";
            worksheet.Cell(row, 4).Value = totalSunEasySales;
            row += 2;

            // Top SunEasy Performers
            if (sunEasyResourcePerformance.Any())
            {
                worksheet.Cell(row, 1).Value = "🏆 TOP SUNEASY PERFORMERS";
                worksheet.Cell(row, 1).Style.Font.Bold = true;
                worksheet.Cell(row, 1).Style.Font.FontSize = 12;
                worksheet.Cell(row, 1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.Gold;
                worksheet.Range(row, 1, row, 6).Merge();
                row += 2;

                var topRevenue = sunEasyResourcePerformance.First();
                var topSales = sunEasyResourcePerformance.OrderByDescending(r => r.SalesCount).First();
                var topAverage = sunEasyResourcePerformance.OrderByDescending(r => r.AverageRevenue).First();

                worksheet.Cell(row, 1).Value = "Top Revenue:";
                worksheet.Cell(row, 2).Value = $"{topRevenue.ResourceNumber} ({analytics.Currency}{topRevenue.TotalRevenue:F2})";
                worksheet.Range(row, 2, row, 6).Merge();
                row++;

                worksheet.Cell(row, 1).Value = "Most Bookings:";
                worksheet.Cell(row, 2).Value = $"{topSales.ResourceNumber} ({topSales.SalesCount} sales)";
                worksheet.Range(row, 2, row, 6).Merge();
                row++;

                worksheet.Cell(row, 1).Value = "Best Average:";
                worksheet.Cell(row, 2).Value = $"{topAverage.ResourceNumber} ({analytics.Currency}{topAverage.AverageRevenue:F2}/sale)";
                worksheet.Range(row, 2, row, 6).Merge();
            }
        }
        else
        {
            worksheet.Cell(row, 1).Value = "No SunEasy resource data available.";
            worksheet.Cell(row, 1).Style.Font.Italic = true;
            worksheet.Range(row, 1, row, 6).Merge();
        }

        // Auto-fit columns
        worksheet.Columns().AdjustToContents();
    }
}
