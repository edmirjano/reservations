using Core.Interfaces;
using Reservation.Models;
using ReservationService;

namespace Reservation.Interfaces;

public interface IStatus : IGeneric<Status>
{
    /// <summary>
    /// Creates a new status.
    /// </summary>
    /// <param name="status">The status DTO to create.</param>
    /// <returns>The created status DTO.</returns>
    Task<StatusDTO> CreateStatusAsync(StatusDTO status);

    /// <summary>
    /// Updates an existing status.
    /// </summary>
    /// <param name="status">The status DTO to update.</param>
    /// <returns>The updated status DTO.</returns>
    Task<StatusDTO> UpdateStatusAsync(StatusDTO status);

    /// <summary>
    /// Deletes a status by its ID.
    /// </summary>
    /// <param name="id">The ID of the status to delete.</param>
    Task DeleteStatusAsync(Guid id);

    /// <summary>
    /// Gets a status by its ID.
    /// </summary>
    /// <param name="id">The ID of the status to retrieve.</param>
    /// <returns>The status DTO.</returns>
    Task<StatusDTO> GetStatusByIdAsync(Guid id);

    /// <summary>
    /// Gets a list of statuses based on the specified request.
    /// </summary>
    /// <param name="request">The request parameters for retrieving statuses.</param>
    /// <returns>A list of status DTOs.</returns>
    Task<IEnumerable<StatusDTO>> GetStatusesAsync(GetStatusesRequest request);
}
