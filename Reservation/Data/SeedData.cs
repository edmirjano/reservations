using Microsoft.EntityFrameworkCore;
using Reservation.Models;

namespace Reservation.Data;

public static class SeedData
{
    public static async Task SeedAsync(ReservationContext context)
    {
        // Ensure database is created
        await context.Database.EnsureCreatedAsync();

        // Seed Statuses
        await SeedStatusesAsync(context);

        // Seed Reservations with Details
        await SeedReservationsAsync(context);

        // Seed Reservation Resources
        await SeedReservationResourcesAsync(context);
    }

    private static async Task SeedStatusesAsync(ReservationContext context)
    {
        if (await context.Statuses.AnyAsync()) return;

        var statuses = new List<Status>
        {
            new()
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "Pending",
                Description = "Reservation is pending confirmation",
                IsActive = true,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Name = "Confirmed",
                Description = "Reservation is confirmed and active",
                IsActive = true,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                Name = "Cancelled",
                Description = "Reservation has been cancelled",
                IsActive = true,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                Name = "Completed",
                Description = "Reservation has been completed",
                IsActive = true,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
                Name = "No-Show",
                Description = "Customer did not show up",
                IsActive = true,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        context.Statuses.AddRange(statuses);
        await context.SaveChangesAsync();
    }

    private static async Task SeedReservationsAsync(ReservationContext context)
    {
        if (await context.Reservations.AnyAsync()) return;

        var statuses = await context.Statuses.ToListAsync();
        var confirmedStatus = statuses.First(s => s.Name == "Confirmed");
        var pendingStatus = statuses.First(s => s.Name == "Pending");
        var completedStatus = statuses.First(s => s.Name == "Completed");
        var cancelledStatus = statuses.First(s => s.Name == "Cancelled");

        var reservations = new List<Models.Reservation>();
        var details = new List<Detail>();

        // Sample organizations and users
        var organizations = new[]
        {
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), // Hotel Paradise
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), // Resort Sunset
            Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"), // Villa Ocean
            Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"), // Apartment City
            Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee")  // Cabin Mountain
        };

        var users = new[]
        {
            Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),
            Guid.Parse("gggggggg-gggg-gggg-gggg-gggggggggggg"),
            Guid.Parse("hhhhhhhh-hhhh-hhhh-hhhh-hhhhhhhhhhhh"),
            Guid.Parse("iiiiiiii-iiii-iiii-iiii-iiiiiiiiiiii"),
            Guid.Parse("jjjjjjjj-jjjj-jjjj-jjjj-jjjjjjjjjjjj")
        };

        var paymentTypes = new[]
        {
            Guid.Parse("kkkkkkkk-kkkk-kkkk-kkkk-kkkkkkkkkkkk"),
            Guid.Parse("llllllll-llll-llll-llll-llllllllllll"),
            Guid.Parse("mmmmmmmm-mmmm-mmmm-mmmm-mmmmmmmmmmmm")
        };

        var sources = new[] { "Web", "Mobile", "Phone", "Walk-in", "Booking.com", "Airbnb", "Expedia" };
        var currencies = new[] { "EUR", "USD", "GBP", "CHF" };

        // Generate 100 realistic reservations
        for (int i = 1; i <= 100; i++)
        {
            var startDate = DateTime.UtcNow.AddDays(Random.Shared.Next(-30, 60));
            var endDate = startDate.AddDays(Random.Shared.Next(1, 14));
            var organizationId = organizations[Random.Shared.Next(organizations.Length)];
            var userId = users[Random.Shared.Next(users.Length)];
            var paymentTypeId = paymentTypes[Random.Shared.Next(paymentTypes.Length)];
            var source = sources[Random.Shared.Next(sources.Length)];
            var currency = currencies[Random.Shared.Next(currencies.Length)];

            // Determine status based on dates
            Status status;
            if (endDate < DateTime.UtcNow)
                status = Random.Shared.NextDouble() < 0.8 ? completedStatus : cancelledStatus;
            else if (startDate <= DateTime.UtcNow && endDate >= DateTime.UtcNow)
                status = confirmedStatus;
            else
                status = Random.Shared.NextDouble() < 0.7 ? confirmedStatus : pendingStatus;

            var totalAmount = Random.Shared.Next(50, 500) + Random.Shared.NextDouble() * 100;
            var discount = Random.Shared.NextDouble() < 0.3 ? Random.Shared.Next(5, 25) : 0;
            var finalAmount = totalAmount - (totalAmount * discount / 100);

            var reservation = new Models.Reservation
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                OrganizationId = organizationId,
                PaymentTypeId = paymentTypeId,
                StatusId = status.Id,
                TotalAmount = Math.Round(finalAmount, 2),
                Code = $"RES-{DateTime.Now.Year}-{i:D6}",
                StartDate = startDate,
                EndDate = endDate,
                Source = source,
                IsActive = status.Name != "Cancelled",
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow.AddDays(-Random.Shared.Next(0, 30)),
                UpdatedAt = DateTime.UtcNow.AddDays(-Random.Shared.Next(0, 5))
            };

            reservations.Add(reservation);

            // Create corresponding detail
            var detail = new Detail
            {
                Id = Guid.NewGuid(),
                ReservationId = reservation.Id,
                Name = GenerateRandomName(),
                Email = GenerateRandomEmail(),
                Phone = GenerateRandomPhone(),
                NumberOfAdults = Random.Shared.Next(1, 6),
                NumberOfChildren = Random.Shared.Next(0, 4),
                NumberOfInfants = Random.Shared.Next(0, 2),
                NumberOfPets = Random.Shared.Next(0, 3),
                ResourceQuantity = Random.Shared.Next(1, 5),
                Note = GenerateRandomNote(),
                OriginalPrice = Math.Round(totalAmount, 2),
                Discount = discount,
                Currency = currency,
                IsActive = true,
                IsDeleted = false,
                CreatedAt = reservation.CreatedAt,
                UpdatedAt = reservation.UpdatedAt
            };

            details.Add(detail);
        }

        context.Reservations.AddRange(reservations);
        context.Details.AddRange(details);
        await context.SaveChangesAsync();
    }

    private static async Task SeedReservationResourcesAsync(ReservationContext context)
    {
        if (await context.ReservationResources.AnyAsync()) return;

        var reservations = await context.Reservations.Take(50).ToListAsync();
        var reservationResources = new List<ReservationResource>();

        // Sample resource IDs
        var resourceIds = new[]
        {
            Guid.Parse("rrrrrrrr-rrrr-rrrr-rrrr-rrrrrrrrrrrr"), // Standard Room
            Guid.Parse("ssssssss-ssss-ssss-ssss-ssssssssssss"), // Deluxe Room
            Guid.Parse("tttttttt-tttt-tttt-tttt-tttttttttttt"), // Suite
            Guid.Parse("uuuuuuuu-uuuu-uuuu-uuuu-uuuuuuuuuuuu"), // Villa
            Guid.Parse("vvvvvvvv-vvvv-vvvv-vvvv-vvvvvvvvvvvv"), // Apartment
            Guid.Parse("wwwwwwww-wwww-wwww-wwww-wwwwwwwwwwww"), // Cabin
            Guid.Parse("xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"), // Studio
            Guid.Parse("yyyyyyyy-yyyy-yyyy-yyyy-yyyyyyyyyyyy")  // Penthouse
        };

        foreach (var reservation in reservations)
        {
            // Each reservation can have 1-3 resources
            var resourceCount = Random.Shared.Next(1, 4);
            var selectedResources = resourceIds.OrderBy(x => Random.Shared.Next()).Take(resourceCount);

            foreach (var resourceId in selectedResources)
            {
                var reservationResource = new ReservationResource
                {
                    Id = Guid.NewGuid(),
                    ReservationId = reservation.Id,
                    ResourceId = resourceId,
                    IsActive = true,
                    IsDeleted = false,
                    CreatedAt = reservation.CreatedAt,
                    UpdatedAt = reservation.UpdatedAt
                };

                reservationResources.Add(reservationResource);
            }
        }

        context.ReservationResources.AddRange(reservationResources);
        await context.SaveChangesAsync();
    }

    private static string GenerateRandomName()
    {
        var firstNames = new[]
        {
            "John", "Jane", "Michael", "Sarah", "David", "Emily", "Robert", "Jessica", "William", "Ashley",
            "James", "Amanda", "Christopher", "Jennifer", "Daniel", "Lisa", "Matthew", "Nancy", "Anthony", "Karen",
            "Mark", "Betty", "Donald", "Helen", "Steven", "Sandra", "Paul", "Donna", "Andrew", "Carol",
            "Joshua", "Ruth", "Kenneth", "Sharon", "Kevin", "Michelle", "Brian", "Laura", "George", "Sarah",
            "Timothy", "Kimberly", "Ronald", "Deborah", "Jason", "Dorothy", "Edward", "Lisa", "Jeffrey", "Nancy"
        };

        var lastNames = new[]
        {
            "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis", "Rodriguez", "Martinez",
            "Hernandez", "Lopez", "Gonzalez", "Wilson", "Anderson", "Thomas", "Taylor", "Moore", "Jackson", "Martin",
            "Lee", "Perez", "Thompson", "White", "Harris", "Sanchez", "Clark", "Ramirez", "Lewis", "Robinson",
            "Walker", "Young", "Allen", "King", "Wright", "Scott", "Torres", "Nguyen", "Hill", "Flores",
            "Green", "Adams", "Nelson", "Baker", "Hall", "Rivera", "Campbell", "Mitchell", "Carter", "Roberts"
        };

        return $"{firstNames[Random.Shared.Next(firstNames.Length)]} {lastNames[Random.Shared.Next(lastNames.Length)]}";
    }

    private static string GenerateRandomEmail()
    {
        var domains = new[] { "gmail.com", "yahoo.com", "hotmail.com", "outlook.com", "icloud.com", "company.com" };
        var name = GenerateRandomName().ToLower().Replace(" ", ".");
        return $"{name}@{domains[Random.Shared.Next(domains.Length)]}";
    }

    private static string GenerateRandomPhone()
    {
        var countryCodes = new[] { "+1", "+44", "+49", "+33", "+39", "+34", "+41", "+31" };
        var countryCode = countryCodes[Random.Shared.Next(countryCodes.Length)];
        var number = Random.Shared.Next(100000000, 999999999);
        return $"{countryCode}{number}";
    }

    private static string GenerateRandomNote()
    {
        var notes = new[]
        {
            "Early check-in requested",
            "Late check-out requested",
            "High floor preferred",
            "Quiet room requested",
            "Room with view requested",
            "Accessible room required",
            "Extra towels needed",
            "Celebrating anniversary",
            "Business trip",
            "Family vacation",
            "Honeymoon",
            "Birthday celebration",
            "No special requests",
            "Pet-friendly room needed",
            "Smoking room requested",
            "Non-smoking room required",
            "Ground floor preferred",
            "Pool view requested",
            "Garden view preferred",
            "City view requested"
        };

        return Random.Shared.NextDouble() < 0.7 ? notes[Random.Shared.Next(notes.Length)] : "No special requests";
    }
}
