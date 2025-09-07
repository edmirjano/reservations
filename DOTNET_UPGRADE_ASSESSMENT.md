# .NET 9.0 Upgrade & Refactoring Assessment

## 🎯 **Assessment Overview**

This assessment evaluates a developer's ability to:
1. Upgrade .NET 8.0 to .NET 9.0 with proper package management
2. Refactor a monolithic repository into clean architecture
3. Implement proper error handling and validation
4. Create database seeding and testing infrastructure
5. Set up gRPC endpoint testing

## 📋 **Current State Analysis**

### Issues Identified:
- ❌ Target framework still on `net8.0` (should be `net9.0`)
- ❌ Dockerfile uses .NET 8.0 runtime
- ❌ Massive `ReservationRepository` (2500+ lines) violating SRP
- ❌ No proper error handling or custom exceptions
- ❌ Missing database seeding
- ❌ No gRPC testing examples
- ❌ Nullable reference warnings throughout codebase

## 🚀 **Phase 1: .NET 9.0 Upgrade**

### 1.1 Update Project Files

**File: `Reservation/Reservation.csproj`**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <DockerDefaultTargetOS>Linux</DockerDefaultTargetOS>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="ClosedXML" Version="0.105.0" />
    <PackageReference Include="Dapper" Version="2.1.66" />
    <PackageReference Include="Grpc.Core" Version="2.46.6" />
    <PackageReference Include="Grpc.Net.ClientFactory" Version="2.70.0" />
    <PackageReference Include="Grpc.Tools" Version="2.71.0">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="Google.Protobuf" Version="3.30.2" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="9.0.4" />
    <PackageReference Include="Microsoft.EntityFrameworkCore" Version="9.0.4" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="9.0.4">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="9.0.4">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.Extensions.Configuration" Version="9.0.4" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="9.0.4" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="9.0.4" />
    <PackageReference Include="FluentValidation" Version="11.9.0" />
    <PackageReference Include="FluentValidation.DependencyInjectionExtensions" Version="11.9.0" />
  </ItemGroup>
  <!-- ... rest of project file ... -->
</Project>
```

### 1.2 Update Dockerfile

**File: `Reservation/Dockerfile`**
```dockerfile
# Use the official ASP.NET Core runtime image for .NET 9.0
FROM mcr.microsoft.com/dotnet/aspnet:9.0

# Set environment variables with defaults
ENV DB_HOST="" \
    DB_PORT="" \
    DB_NAME="" \
    DB_USER="" \
    DB_PASSWORD="" \
    ASPNETCORE_URLS="http://+:80"

# Set the working directory
WORKDIR /app

# Copy the published files from the local directory (built by the script)
COPY ./publish /app

# Expose port 80 for HTTP
EXPOSE 80

# Health check (optional for Kubernetes or Docker Swarm)
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl --fail http://localhost:80/health || exit 1

# Run the application
ENTRYPOINT ["dotnet", "reservation.dll"]
```

## 🏗️ **Phase 2: Repository Refactoring**

### 2.1 Create Custom Exceptions

**File: `Reservation/Exceptions/ReservationExceptions.cs`**
```csharp
namespace Reservation.Exceptions;

public class ReservationNotFoundException : Exception
{
    public ReservationNotFoundException(Guid id) : base($"Reservation with ID {id} not found") { }
}

public class StatusNotFoundException : Exception
{
    public StatusNotFoundException(Guid id) : base($"Status with ID {id} not found") { }
}

public class InvalidReservationDataException : Exception
{
    public InvalidReservationDataException(string message) : base(message) { }
}

public class ReservationConflictException : Exception
{
    public ReservationConflictException(string message) : base(message) { }
}

public class ResourceUnavailableException : Exception
{
    public ResourceUnavailableException(Guid resourceId, DateTime startDate, DateTime endDate) 
        : base($"Resource {resourceId} is not available from {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}") { }
}
```

### 2.2 Split Repository into Focused Repositories

**File: `Reservation/Repositories/IReservationRepository.cs`**
```csharp
namespace Reservation.Repositories;

public interface IReservationRepository : IGenericRepository<Models.Reservation>
{
    Task<Models.Reservation?> GetByIdWithDetailsAsync(Guid id);
    Task<IEnumerable<Models.Reservation>> GetByDateRangeAsync(DateTime start, DateTime end);
    Task<IEnumerable<Models.Reservation>> GetByResourceIdsAsync(IEnumerable<Guid> resourceIds);
    Task<IEnumerable<Models.Reservation>> GetByOrganizationIdAsync(Guid organizationId, int page, int pageSize);
    Task<Models.Reservation?> GetByCodeAsync(string code);
    Task<bool> ExistsAsync(Guid id);
    Task<int> GetCountByOrganizationAsync(Guid organizationId);
}
```

**File: `Reservation/Repositories/ReservationRepository.cs`** (Refactored - max 200 lines)
```csharp
namespace Reservation.Repositories;

public class ReservationRepository : GenericRepository<Models.Reservation, ReservationContext>, IReservationRepository
{
    private readonly ReservationContext _context;

    public ReservationRepository(ReservationContext context, Func<IDbConnection> dbConnectionFactory) 
        : base(context, dbConnectionFactory)
    {
        _context = context;
    }

    public async Task<Models.Reservation?> GetByIdWithDetailsAsync(Guid id)
    {
        return await _context.Reservations
            .Include(r => r.Status)
            .Include(r => r.ReservationResources)
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
    }

    public async Task<IEnumerable<Models.Reservation>> GetByDateRangeAsync(DateTime start, DateTime end)
    {
        return await _context.Reservations
            .Where(r => r.StartDate >= start && r.EndDate <= end && !r.IsDeleted)
            .Include(r => r.Status)
            .ToListAsync();
    }

    public async Task<Models.Reservation?> GetByCodeAsync(string code)
    {
        return await _context.Reservations
            .Include(r => r.Status)
            .FirstOrDefaultAsync(r => r.Code == code && !r.IsDeleted);
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _context.Reservations
            .AnyAsync(r => r.Id == id && !r.IsDeleted);
    }

    // ... other focused methods (max 200 lines total)
}
```

### 2.3 Create Domain Services

**File: `Reservation/Services/Domain/IReservationDomainService.cs`**
```csharp
namespace Reservation.Services.Domain;

public interface IReservationDomainService
{
    Task<ReservationDTO> CreateReservationAsync(CreateReservationDto dto);
    Task<ReservationDTO> UpdateReservationAsync(ReservationDTO dto);
    Task<ReservationDTO> ValidateReservationBusinessRulesAsync(CreateReservationDto dto);
    Task<ReservationDTO> ProcessReservationPaymentAsync(ReservationDTO reservation);
}
```

**File: `Reservation/Services/Domain/ReservationDomainService.cs`**
```csharp
namespace Reservation.Services.Domain;

public class ReservationDomainService : IReservationDomainService
{
    private readonly IReservationRepository _reservationRepository;
    private readonly IReservationValidationService _validationService;
    private readonly IPaymentService _paymentService;
    private readonly ILogger<ReservationDomainService> _logger;

    public ReservationDomainService(
        IReservationRepository reservationRepository,
        IReservationValidationService validationService,
        IPaymentService paymentService,
        ILogger<ReservationDomainService> logger)
    {
        _reservationRepository = reservationRepository;
        _validationService = validationService;
        _paymentService = paymentService;
        _logger = logger;
    }

    public async Task<ReservationDTO> CreateReservationAsync(CreateReservationDto dto)
    {
        _logger.LogInformation("Creating reservation for user {UserId} in organization {OrganizationId}", 
            dto.UserId, dto.OrganizationId);

        // Validate business rules
        await _validationService.ValidateReservationAsync(dto);

        // Process payment
        var paymentResult = await _paymentService.ProcessPaymentAsync(dto);

        // Create reservation
        var reservation = await _reservationRepository.CreateAsync(MapToEntity(dto, paymentResult));

        _logger.LogInformation("Successfully created reservation {ReservationId}", reservation.Id);
        return MapToDTO(reservation);
    }

    // ... other methods
}
```

## 🗄️ **Phase 3: Database Seeding**

### 3.1 Create Seed Data

**File: `Reservation/Data/SeedData.cs`**
```csharp
namespace Reservation.Data;

public static class SeedData
{
    public static async Task SeedAsync(ReservationContext context)
    {
        if (!context.Statuses.Any())
        {
            var statuses = new List<Status>
            {
                new() { Id = Guid.NewGuid(), Name = "Pending", Description = "Reservation is pending confirmation", IsActive = true, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new() { Id = Guid.NewGuid(), Name = "Confirmed", Description = "Reservation is confirmed", IsActive = true, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new() { Id = Guid.NewGuid(), Name = "Cancelled", Description = "Reservation is cancelled", IsActive = true, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new() { Id = Guid.NewGuid(), Name = "Completed", Description = "Reservation is completed", IsActive = true, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
            };
            
            context.Statuses.AddRange(statuses);
            await context.SaveChangesAsync();
        }

        if (!context.Reservations.Any())
        {
            var status = await context.Statuses.FirstAsync();
            var reservations = new List<Models.Reservation>();
            
            for (int i = 1; i <= 50; i++)
            {
                reservations.Add(new Models.Reservation
                {
                    Id = Guid.NewGuid(),
                    UserId = Guid.NewGuid(),
                    OrganizationId = Guid.NewGuid(),
                    PaymentTypeId = Guid.NewGuid(),
                    StatusId = status.Id,
                    TotalAmount = 100 + (i * 10),
                    Code = $"RES-{i:D6}",
                    StartDate = DateTime.UtcNow.AddDays(i),
                    EndDate = DateTime.UtcNow.AddDays(i + 1),
                    Source = i % 2 == 0 ? "Web" : "Mobile",
                    IsActive = true,
                    IsDeleted = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            
            context.Reservations.AddRange(reservations);
            await context.SaveChangesAsync();
        }
    }
}
```

### 3.2 Update Program.cs for Seeding

**File: `Reservation/Program.cs`** (Add seeding)
```csharp
// Add this after services configuration
var app = host.Services.CreateScope().ServiceProvider;
using var scope = app.CreateScope();
var context = scope.ServiceProvider.GetRequiredService<ReservationContext>();
await context.Database.EnsureCreatedAsync();
await SeedData.SeedAsync(context);
```

## 🧪 **Phase 4: gRPC Testing Examples**

### 4.1 Create gRPC Test Client

**File: `Reservation/Tests/GrpcTestClient.cs`**
```csharp
using Grpc.Net.Client;
using ReservationService;

namespace Reservation.Tests;

public class GrpcTestClient
{
    private readonly ReservationService.ReservationService.ReservationServiceClient _client;

    public GrpcTestClient(string serverUrl = "http://localhost:5000")
    {
        var channel = GrpcChannel.ForAddress(serverUrl);
        _client = new ReservationService.ReservationService.ReservationServiceClient(channel);
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

    public async Task<ReservationDTO> GetReservationByIdAsync(string id)
    {
        var request = new GetByIdRequest { Id = id };
        return await _client.GetReservationByIdAsync(request);
    }

    public async Task<ReservationDTOList> GetReservationsAsync()
    {
        var request = new GetReservationsRequest
        {
            Page = 1,
            PerPage = 10,
            WithOrganizations = false
        };
        return await _client.GetReservationsAsync(request);
    }
}
```

### 4.2 Create Test Script

**File: `Reservation/Tests/TestGrpcEndpoints.cs`**
```csharp
namespace Reservation.Tests;

public class TestGrpcEndpoints
{
    public static async Task Main(string[] args)
    {
        var client = new GrpcTestClient();
        
        Console.WriteLine("🧪 Testing gRPC Endpoints...\n");

        try
        {
            // Test 1: Create Reservation
            Console.WriteLine("1. Testing CreateReservation...");
            var createdReservation = await client.CreateTestReservationAsync();
            Console.WriteLine($"✅ Created reservation: {createdReservation.Code}");
            Console.WriteLine($"   ID: {createdReservation.Id}");
            Console.WriteLine($"   Total Amount: {createdReservation.TotalAmount}\n");

            // Test 2: Get Reservation by ID
            Console.WriteLine("2. Testing GetReservationById...");
            var retrievedReservation = await client.GetReservationByIdAsync(createdReservation.Id);
            Console.WriteLine($"✅ Retrieved reservation: {retrievedReservation.Code}");
            Console.WriteLine($"   Status: {retrievedReservation.StatusId}\n");

            // Test 3: Get All Reservations
            Console.WriteLine("3. Testing GetReservations...");
            var reservations = await client.GetReservationsAsync();
            Console.WriteLine($"✅ Retrieved {reservations.Reservations.Count} reservations\n");

            Console.WriteLine("🎉 All tests passed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Test failed: {ex.Message}");
        }
    }
}
```

## 🚀 **Phase 5: Application Setup & Running**

### 5.1 Docker Compose for Development

**File: `docker-compose.dev.yml`**
```yaml
version: '3.8'

services:
  postgres:
    image: postgres:15
    environment:
      POSTGRES_DB: reservation
      POSTGRES_USER: reservation
      POSTGRES_PASSWORD: reservation
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data

  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"

  reservation:
    build:
      context: ./Reservation
      dockerfile: Dockerfile
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__DB_HOST=postgres
      - ConnectionStrings__DB_PORT=5432
      - ConnectionStrings__DB_NAME=reservation
      - ConnectionStrings__DB_USER=reservation
      - ConnectionStrings__DB_PASSWORD=reservation
      - ConnectionStrings__Redis=redis:6379
      - Services__Auth=http://auth:5001
      - Services__Payment=http://payment:5010
      - Services__Resource=http://resource:5002
      - Services__Organization=http://organization:5004
    depends_on:
      - postgres
      - redis
    ports:
      - "5000:80"

volumes:
  postgres_data:
```

### 5.2 Build and Run Scripts

**File: `build-and-run.sh`**
```bash
#!/bin/bash

echo "🏗️ Building .NET 9.0 Reservation Service..."

# Clean and restore
dotnet clean
dotnet restore

# Build
dotnet build --configuration Release

# Publish
dotnet publish Reservation/Reservation.csproj --configuration Release --output Reservation/publish

# Build Docker image
docker build -t reservation-service:latest ./Reservation

echo "✅ Build completed successfully!"
echo "🚀 Starting services with Docker Compose..."

# Start services
docker-compose -f docker-compose.dev.yml up --build

echo "🎉 Application is running!"
echo "📡 gRPC endpoint: http://localhost:5000"
echo "🗄️ PostgreSQL: localhost:5432"
echo "🔴 Redis: localhost:6379"
```

**File: `test-grpc.sh`**
```bash
#!/bin/bash

echo "🧪 Testing gRPC Endpoints..."

# Wait for service to be ready
echo "⏳ Waiting for service to be ready..."
sleep 10

# Run gRPC tests
dotnet run --project Reservation/Tests/TestGrpcEndpoints.cs

echo "✅ gRPC tests completed!"
```

## 📊 **Assessment Criteria**

### ✅ **Must Complete (Pass/Fail)**
1. **Framework Upgrade**: Successfully upgrade to .NET 9.0
2. **Docker Update**: Update Dockerfile to use .NET 9.0 runtime
3. **Repository Split**: Break down 2500+ line repository into focused repositories (max 200 lines each)
4. **Custom Exceptions**: Implement proper exception handling
5. **Database Seeding**: Create and implement seed data
6. **gRPC Testing**: Provide working gRPC test examples
7. **Clean Build**: Zero warnings, successful build

### 🎯 **Quality Indicators (Bonus Points)**
1. **Clean Architecture**: Proper separation of concerns
2. **Error Handling**: Comprehensive exception handling
3. **Validation**: Input validation implementation
4. **Testing**: Unit test examples
5. **Documentation**: Clear README with setup instructions
6. **Performance**: Efficient database queries
7. **Code Quality**: Clean, readable, maintainable code

## 🎯 **Expected Deliverables**

1. **Updated Project Files**: .NET 9.0 with latest packages
2. **Refactored Repositories**: Split into focused, single-responsibility classes
3. **Custom Exceptions**: Proper error handling throughout
4. **Database Seeding**: Comprehensive seed data
5. **gRPC Test Suite**: Working examples for testing endpoints
6. **Docker Setup**: Complete development environment
7. **Documentation**: Setup and testing instructions

## 🚨 **Critical Requirements**

- **Zero Warnings**: Build must complete without any warnings
- **Working gRPC**: All endpoints must be testable
- **Database Seeding**: Must populate database with realistic data
- **Clean Code**: Follow SOLID principles and clean architecture
- **Error Handling**: Proper exception handling throughout
- **Documentation**: Clear instructions for setup and testing

## 🎉 **Success Criteria**

The assessment is considered successful when:
- ✅ .NET 9.0 upgrade completed successfully
- ✅ All repositories refactored (max 200 lines each)
- ✅ Custom exceptions implemented
- ✅ Database seeding working
- ✅ gRPC endpoints testable
- ✅ Zero build warnings
- ✅ Docker environment working
- ✅ Clear documentation provided

---

**Note**: This assessment evaluates both technical skills and architectural understanding. Focus on clean, maintainable code that follows .NET best practices.
