# Reservation Service - .NET 9.0 Upgrade Assessment

## 🎯 Overview

This project is a comprehensive assessment for upgrading a .NET 8.0 Reservation Service to .NET 9.0 with proper refactoring, testing, and documentation. The service provides gRPC endpoints for managing hotel/resort reservations.

## 🏗️ Architecture

- **Framework**: .NET 9.0
- **Database**: PostgreSQL 15
- **Cache**: Redis 7
- **Communication**: gRPC
- **Containerization**: Docker & Docker Compose

## 📋 Prerequisites

- .NET 9.0 SDK
- Docker & Docker Compose
- PostgreSQL client (optional, for direct database access)
- Git

## 🚀 Quick Start

### 1. Clone and Setup
```bash
git clone <repository-url>
cd reservations
chmod +x build-and-run.sh test-grpc.sh
```

### 2. Build and Run
```bash
./build-and-run.sh
```

This script will:
- ✅ Verify .NET 9.0 SDK installation
- 🧹 Clean and restore dependencies
- 🔨 Build the solution
- 📦 Publish the application
- 🐳 Build Docker image
- 🚀 Start all services with Docker Compose

### 3. Test gRPC Endpoints
```bash
./test-grpc.sh
```

## 🧪 Testing gRPC Endpoints

The service provides comprehensive gRPC endpoints for reservation management:

### Core Endpoints
- `CreateReservation` - Create new reservations
- `CreateReservationForOrganization` - Create reservations for organizations
- `GetReservationById` - Retrieve reservation by ID
- `GetReservations` - List reservations with pagination
- `UpdateReservation` - Update existing reservations
- `DeleteReservation` - Soft delete reservations

### Advanced Endpoints
- `GetReservationsByResources` - Get reservations by resource IDs
- `GetReservationsByDateRange` - Filter by date range
- `ValidateTicket` - Validate reservation by code
- `GetReservationsCountPerDay` - Daily statistics
- `SearchClients` - Client autocomplete
- `GenerateReservationReport` - Export reports

### Status Management
- `CreateStatus` - Create reservation statuses
- `GetStatuses` - List all statuses
- `UpdateStatus` - Update status information
- `DeleteStatus` - Remove statuses

### Analytics
- `GetStats` - Reservation statistics
- `GetReservationsBySourceCount` - Source-based analytics

## 🗄️ Database Schema

### Tables
- **Reservations** - Main reservation data
- **Details** - Customer information
- **ReservationResources** - Resource assignments
- **Statuses** - Reservation status definitions

### Seed Data
The database is automatically seeded with:
- 5 default statuses (Pending, Confirmed, Cancelled, Completed, No-Show)
- 100 realistic reservations with customer details
- Resource assignments for testing

## 🔧 Development

### Project Structure
```
Reservation/
├── Data/                 # Database context and seeding
├── Models/              # Entity models
├── Repositories/        # Data access layer
├── Services/           # Business logic
├── Tests/              # gRPC test client
├── Program.cs          # Application entry point
└── Dockerfile          # Container configuration
```

### Key Files
- `DOTNET_UPGRADE_ASSESSMENT.md` - Complete assessment requirements
- `Data/SeedData.cs` - Database seeding with realistic data
- `Tests/GrpcTestClient.cs` - Comprehensive gRPC testing client
- `Tests/TestGrpcEndpoints.cs` - Test execution script

## 🐳 Docker Services

### Core Services
- **reservation-service** (Port 5000) - Main gRPC service
- **postgres** (Port 5432) - PostgreSQL database
- **redis** (Port 6379) - Redis cache

### Mock Services (for testing)
- **auth-mock** (Port 5001) - Authentication service mock
- **payment-mock** (Port 5010) - Payment service mock
- **resource-mock** (Port 5002) - Resource service mock
- **organization-mock** (Port 5004) - Organization service mock
- **email-mock** (Port 5006) - Email service mock

## 📊 Monitoring & Health Checks

### Health Endpoints
- `http://localhost:5000/health` - Service health check

### Logs
```bash
# View all logs
docker-compose -f docker-compose.dev.yml logs -f

# View specific service logs
docker-compose -f docker-compose.dev.yml logs -f reservation
docker-compose -f docker-compose.dev.yml logs -f postgres
```

### Database Access
```bash
# Connect to PostgreSQL
docker-compose -f docker-compose.dev.yml exec postgres psql -U reservation -d reservation

# Check reservation count
docker-compose -f docker-compose.dev.yml exec postgres psql -U reservation -d reservation -c "SELECT COUNT(*) FROM \"Reservations\";"

# Check statuses
docker-compose -f docker-compose.dev.yml exec postgres psql -U reservation -d reservation -c "SELECT * FROM \"Statuses\";"
```

## 🧪 Manual Testing

### Using gRPC Client Tools

#### 1. Install grpcurl
```bash
# macOS
brew install grpcurl

# Linux
curl -L https://github.com/fullstorydev/grpcurl/releases/download/v1.8.7/grpcurl_1.8.7_linux_x86_64.tar.gz | tar -xz
sudo mv grpcurl /usr/local/bin/
```

#### 2. Test Endpoints
```bash
# List available services
grpcurl -plaintext localhost:5000 list

# List service methods
grpcurl -plaintext localhost:5000 list reservation.ReservationService

# Create a reservation
grpcurl -plaintext -d '{
  "userId": "123e4567-e89b-12d3-a456-426614174000",
  "organizationId": "123e4567-e89b-12d3-a456-426614174001",
  "startDate": "2024-12-25",
  "endDate": "2024-12-27",
  "source": "Web",
  "resources": [{
    "id": "123e4567-e89b-12d3-a456-426614174002",
    "date": "2024-12-25",
    "price": 150,
    "quantity": 1
  }],
  "detail": {
    "name": "John Doe",
    "email": "john@example.com",
    "phone": "+1234567890",
    "numberOfAdults": 2,
    "numberOfChildren": 0,
    "numberOfInfants": 0,
    "numberOfPets": 0,
    "resourceQuantity": 1,
    "note": "Test reservation",
    "originalPrice": 150,
    "discount": 0,
    "currency": "EUR"
  }
}' localhost:5000 reservation.ReservationService/CreateReservation

# Get reservations
grpcurl -plaintext -d '{
  "page": 1,
  "perPage": 10,
  "withOrganizations": false
}' localhost:5000 reservation.ReservationService/GetReservations
```

## 🔧 Troubleshooting

### Common Issues

#### 1. Service Not Starting
```bash
# Check service status
docker-compose -f docker-compose.dev.yml ps

# View logs
docker-compose -f docker-compose.dev.yml logs reservation

# Restart services
docker-compose -f docker-compose.dev.yml restart
```

#### 2. Database Connection Issues
```bash
# Check PostgreSQL health
docker-compose -f docker-compose.dev.yml exec postgres pg_isready -U reservation

# Reset database
docker-compose -f docker-compose.dev.yml down -v
docker-compose -f docker-compose.dev.yml up --build
```

#### 3. gRPC Connection Issues
```bash
# Test gRPC connectivity
grpcurl -plaintext localhost:5000 list

# Check if service is listening
netstat -tlnp | grep :5000
```

#### 4. Build Issues
```bash
# Clean and rebuild
dotnet clean
dotnet restore
dotnet build --configuration Release

# Check .NET version
dotnet --version
```

## 📈 Performance Testing

### Load Testing with Artillery
```bash
# Install Artillery
npm install -g artillery

# Run load test
artillery quick --count 100 --num 10 http://localhost:5000/health
```

### Database Performance
```bash
# Check query performance
docker-compose -f docker-compose.dev.yml exec postgres psql -U reservation -d reservation -c "EXPLAIN ANALYZE SELECT * FROM \"Reservations\" WHERE \"OrganizationId\" = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';"
```

## 🎯 Assessment Criteria

### ✅ Must Complete
1. **Framework Upgrade**: Successfully upgrade to .NET 9.0
2. **Docker Update**: Update Dockerfile to use .NET 9.0 runtime
3. **Repository Split**: Break down 2500+ line repository into focused repositories
4. **Custom Exceptions**: Implement proper exception handling
5. **Database Seeding**: Create and implement seed data
6. **gRPC Testing**: Provide working gRPC test examples
7. **Clean Build**: Zero warnings, successful build

### 🎯 Quality Indicators
1. **Clean Architecture**: Proper separation of concerns
2. **Error Handling**: Comprehensive exception handling
3. **Validation**: Input validation implementation
4. **Testing**: Unit test examples
5. **Documentation**: Clear setup instructions
6. **Performance**: Efficient database queries
7. **Code Quality**: Clean, readable, maintainable code

## 📚 Additional Resources

- [.NET 9.0 Documentation](https://docs.microsoft.com/en-us/dotnet/core/whats-new/dotnet-9)
- [gRPC C# Documentation](https://grpc.io/docs/languages/csharp/)
- [Entity Framework Core 9.0](https://docs.microsoft.com/en-us/ef/core/)
- [Docker Compose Documentation](https://docs.docker.com/compose/)

## 🤝 Contributing

This is an assessment project. Please follow the requirements in `DOTNET_UPGRADE_ASSESSMENT.md` for the complete task list.

## 📄 License

This project is for assessment purposes only.
