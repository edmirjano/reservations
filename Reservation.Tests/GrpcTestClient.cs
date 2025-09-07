using Grpc.Net.Client;
using ReservationService;
using System.Text.Json;

namespace Reservation.Tests;

public class GrpcTestClient
{
    private readonly ReservationService.ReservationService.ReservationServiceClient _client;
    private readonly GrpcChannel _channel;

    public GrpcTestClient(string serverUrl = "http://localhost:5000")
    {
        _channel = GrpcChannel.ForAddress(serverUrl);
        _client = new ReservationService.ReservationService.ReservationServiceClient(_channel);
    }

    public async Task<ReservationDTO> CreateTestReservationAsync()
    {
        var request = new CreateReservationDto
        {
            UserId = Guid.NewGuid().ToString(),
            OrganizationId = Guid.NewGuid().ToString(),
            StartDate = DateTime.Now.AddDays(1).ToString("yyyy-MM-dd"),
            EndDate = DateTime.Now.AddDays(2).ToString("yyyy-MM-dd"),
            Source = "Test",
            Resources = 
            {
                new ResourceItemDto
                {
                    Id = Guid.NewGuid().ToString(),
                    Date = DateTime.Now.AddDays(1).ToString("yyyy-MM-dd"),
                    Price = 100,
                    Quantity = 1
                }
            },
            Detail = new DetailDTO
            {
                Name = "Test User",
                Email = "test@example.com",
                Phone = "+1234567890",
                NumberOfAdults = 2,
                NumberOfChildren = 0,
                NumberOfInfants = 0,
                NumberOfPets = 0,
                ResourceQuantity = 1,
                Note = "Test reservation",
                OriginalPrice = 100,
                Discount = 0,
                Currency = "EUR"
            }
        };

        return await _client.CreateReservationAsync(request);
    }

    public async Task<ReservationDTO> CreateReservationForOrganizationAsync()
    {
        var request = new CreateReservationForOrganizationDto
        {
            UserId = Guid.NewGuid().ToString(),
            OrganizationId = Guid.NewGuid().ToString(),
            StartDate = DateTime.Now.AddDays(1).ToString("yyyy-MM-dd"),
            EndDate = DateTime.Now.AddDays(3).ToString("yyyy-MM-dd"),
            CustomerName = "Organization Customer",
            Email = "org@example.com",
            Phone = "+1234567890",
            Currency = "USD",
            TotalAmount = 250,
            Resources = 
            {
                new ResourceItemDto
                {
                    Id = Guid.NewGuid().ToString(),
                    Date = DateTime.Now.AddDays(1).ToString("yyyy-MM-dd"),
                    Price = 125,
                    Quantity = 2
                }
            }
        };

        return await _client.CreateReservationForOrganizationAsync(request);
    }

    public async Task<ReservationDTO> GetReservationByIdAsync(string id)
    {
        var request = new GetByIdRequest { Id = id };
        return await _client.GetReservationByIdAsync(request);
    }

    public async Task<ReservationDTO> GetReservationByCodeAsync(string code)
    {
        var request = new GetByCodeRequest { Code = code };
        return await _client.ValidateTicketAsync(request);
    }

    public async Task<ReservationDTOList> GetReservationsAsync(int page = 1, int perPage = 10)
    {
        var request = new GetReservationsRequest
        {
            Page = page,
            PerPage = perPage,
            WithOrganizations = false
        };
        return await _client.GetReservationsAsync(request);
    }

    public async Task<ReservationDTOList> GetReservationsByOrganizationAsync(string organizationId, int page = 1, int perPage = 10)
    {
        var request = new GetReservationsRequest
        {
            OrganizationId = organizationId,
            Page = page,
            PerPage = perPage,
            WithOrganizations = true
        };
        return await _client.GetReservationsAsync(request);
    }

    public async Task<ReservationDTOList> GetReservationsByDateRangeAsync(string startDate, string endDate)
    {
        var request = new GetReservationsRequest
        {
            StartDate = startDate,
            EndDate = endDate,
            Page = 1,
            PerPage = 50,
            WithOrganizations = false
        };
        return await _client.GetReservationsAsync(request);
    }

    public async Task<GetReservationsByResourcesResponse> GetReservationsByResourcesAsync(string[] resourceIds, string startDate, string endDate)
    {
        var request = new GetReservationsByResourcesRequest
        {
            StartDate = startDate,
            EndDate = endDate
        };
        request.ResourceIds.AddRange(resourceIds);
        
        return await _client.GetReservationsByResourcesAsync(request);
    }

    public async Task<StatusDTO> CreateStatusAsync(string name, string description)
    {
        var request = new StatusDTO
        {
            Name = name,
            Description = description
        };
        return await _client.CreateStatusAsync(request);
    }

    public async Task<StatusDTOList> GetStatusesAsync()
    {
        var request = new GetStatusesRequest
        {
            Page = 1,
            PerPage = 20
        };
        return await _client.GetStatusesAsync(request);
    }

    public async Task<GetStatsResponse> GetStatsAsync(string organizationId, string startDate, string endDate)
    {
        var request = new GetStatsRequest
        {
            OrganizationId = organizationId,
            StartDate = startDate,
            EndDate = endDate
        };
        return await _client.GetStatsAsync(request);
    }

    public async Task<DateReservationCountList> GetReservationsCountPerDayAsync(string organizationId, string startDate, string endDate)
    {
        var request = new DateRangeRequest
        {
            OrganizationId = organizationId,
            StartDate = startDate,
            EndDate = endDate
        };
        return await _client.GetReservationsCountPerDayAsync(request);
    }

    public async Task<SearchClientsResponse> SearchClientsAsync(string nameQuery, string organizationId, int maxResults = 10)
    {
        var request = new SearchClientsRequest
        {
            NameQuery = nameQuery,
            OrganizationId = organizationId,
            MaxResults = maxResults
        };
        return await _client.SearchClientsByNameAsync(request);
    }

    public async Task<GetReservationsBySourceCountResponse> GetReservationsBySourceCountAsync(string organizationId, string startDate, string endDate)
    {
        var request = new GetReservationsBySourceCountRequest
        {
            OrganizationId = organizationId,
            StartDate = startDate,
            EndDate = endDate
        };
        return await _client.GetReservationsBySourceCountAsync(request);
    }

    public async Task<GenerateReservationReportResponse> GenerateReservationReportAsync(string organizationId, string startDate, string endDate)
    {
        var request = new GenerateReservationReportRequest
        {
            OrganizationId = organizationId,
            StartDate = startDate,
            EndDate = endDate
        };
        return await _client.GenerateReservationReportAsync(request);
    }

    public void Dispose()
    {
        _channel?.Dispose();
    }
}
