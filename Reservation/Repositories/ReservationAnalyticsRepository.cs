using System.Data;
using Core.Repositories;
using Reservation.Data;
using Reservation.Interfaces;
using ReservationService;

namespace Reservation.Repositories;

public class ReservationAnalyticsRepository : GenericRepository<Models.Reservation, ReservationContext>, IReservationAnalytics
{
    public ReservationAnalyticsRepository(ReservationContext context, Func<IDbConnection> dbConnectionFactory)
        : base(context, dbConnectionFactory)
    {
    }

    public async Task<IEnumerable<DateReservationCountDTO>> GetReservationsCountPerDayAsync(string organizationId, DateTime startDate, DateTime endDate)
    {
        var query = @"SELECT DATE_TRUNC('day', r.""StartDate"") AS ""Date"", COUNT(*) AS ""ReservationCount""
            FROM ""Reservations"" r
            WHERE r.""OrganizationId"" = @OrganizationId::uuid AND r.""StartDate"" >= @StartDate AND r.""EndDate"" <= @EndDate
                AND r.""IsActive"" = true AND r.""IsDeleted"" = false
            GROUP BY DATE_TRUNC('day', r.""StartDate"") ORDER BY ""Date""";

        return await QueryAsync<DateReservationCountDTO>(query, new { OrganizationId = organizationId, StartDate = startDate, EndDate = endDate });
    }

    public async Task<GetReservationsBySourceCountResponse> GetReservationsBySourceCountAsync(string organizationId, DateTime startDate, DateTime endDate)
    {
        var query = @"SELECT
                COUNT(CASE WHEN ""Source"" = 'Mobile' THEN 1 END) as ""TotalReservationsClient"",
                COUNT(CASE WHEN ""Source"" = 'Organization' THEN 1 END) as ""TotalReservationsBusiness""
            FROM ""Reservations""
            WHERE ""OrganizationId"" = @OrganizationId::uuid AND ""StartDate"" >= @StartDate AND ""EndDate"" <= @EndDate
                AND ""IsActive"" = true AND ""IsDeleted"" = false";

        return await QueryFirstOrDefaultAsync<GetReservationsBySourceCountResponse>(query, 
            new { OrganizationId = organizationId, StartDate = startDate, EndDate = endDate }) 
            ?? new GetReservationsBySourceCountResponse();
    }
}