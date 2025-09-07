using System.Data;
using System.Reflection;
using Core.Constant.Enums;
using Core.Models;
using Core.Repositories;
using Reservation.Data;
using Reservation.Interfaces;
using Reservation.Models;
using ReservationService;

namespace Reservation.Repositories;

public class StatusRepository(
    ReservationContext reservationContext,
    Func<IDbConnection> dbConnectionFactory
) : GenericRepository<Status, ReservationContext>(reservationContext, dbConnectionFactory), IStatus
{
    public async Task<StatusDTO> CreateStatusAsync(StatusDTO statusDto)
    {
        var status = new Status { Name = statusDto.Name, Description = statusDto.Description };

        var createdStatus = await CreateAsync(status);
        return MapToDTO(createdStatus);
    }

    public async Task<StatusDTO> UpdateStatusAsync(StatusDTO statusDto)
    {
        var status = await GetByIdAsync(Guid.Parse(statusDto.Id));
        status.Name = statusDto.Name;
        status.Description = statusDto.Description;
        var updatedStatus = await UpdateAsync(status);
        return MapToDTO(updatedStatus);
    }

    public async Task DeleteStatusAsync(Guid id)
    {
        await DeleteAsync(id);
    }

    public async Task<StatusDTO> GetStatusByIdAsync(Guid id)
    {
        var status = await GetByIdAsync(id);
        return MapToDTO(status);
    }

    public async Task<IEnumerable<StatusDTO>> GetStatusesAsync(GetStatusesRequest request)
    {
        var pagination = PaginationHandler.GetDefaultPagination();

        var page = request.Page > 0 ? request.Page : pagination.Page;
        var pageSize = request.PerPage > 0 ? request.PerPage : pagination.PageSize;

        var query =
            @"
                SELECT *
                FROM ""Statuses""
                WHERE ""IsDeleted"" = false AND ""IsActive"" = true";

        if (!string.IsNullOrEmpty(request.Keyword))
        {
            query += @" AND (""Name"" LIKE @Keyword OR Description LIKE @Keyword)";
        }

        if (!string.IsNullOrEmpty(request.OrderBy))
        {
            switch (request.OrderBy)
            {
                case "Name":
                    query += @" ORDER BY ""Name"" ";
                    break;

                default:
                    query += @" ORDER BY ""Id"" ";
                    break;
            }
        }
        else
        {
            query += @" ORDER BY ""CreatedAt"" ";
        }

        query += @" OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

        var statuses = await QueryAsync<Status>(
            query,
            new
            {
                Keyword = $"%{request.Keyword}%",
                Offset = (page - 1) * pageSize,
                PageSize = pageSize,
            }
        );
        return statuses.Select(MapToDTO);
    }

    private StatusDTO MapToDTO(Status status)
    {
        Type type = typeof(Enums.ReservationStatusColor);
        var color = type.GetFields(BindingFlags.Static | BindingFlags.Public)
            .FirstOrDefault(p => p.Name == status.Name);

        return new StatusDTO
        {
            Id = status.Id.ToString(),
            Name = status.Name,
            Description = status.Description,
            StatusColor = color != null ? color.GetValue(null).ToString() : string.Empty,
        };
    }
}
