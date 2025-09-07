using Microsoft.EntityFrameworkCore;
using Reservation.Models;

namespace Reservation.Data;

public class ReservationContext(DbContextOptions<ReservationContext> options) : DbContext(options)
{
    public DbSet<Models.Reservation> Reservations { get; init; }
    public DbSet<ReservationResource> ReservationResources { get; init; }
    public DbSet<Status> Statuses { get; init; }
    public DbSet<Detail> Details { get; init; }
}
