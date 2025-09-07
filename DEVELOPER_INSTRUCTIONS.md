# Developer Instructions - .NET 9.0 Upgrade Assessment

## 🎯 **Your Task**

You are tasked with upgrading this .NET 8.0 Reservation Service to .NET 9.0 and implementing proper refactoring. This is a comprehensive assessment that evaluates your skills in:

1. **Framework Upgrades** - .NET 8.0 → .NET 9.0
2. **Architecture Refactoring** - Clean code principles
3. **Error Handling** - Custom exceptions and validation
4. **Database Management** - Seeding and optimization
5. **gRPC Testing** - Comprehensive endpoint testing
6. **Documentation** - Clear setup and usage instructions

## 📋 **What's Already Provided**

### ✅ **Complete Infrastructure**
- **Assessment Document**: `DOTNET_UPGRADE_ASSESSMENT.md` - Your complete task list
- **Database Seeding**: `Reservation/Data/SeedData.cs` - Realistic dummy data
- **gRPC Test Client**: `Reservation.Tests/GrpcTestClient.cs` - Comprehensive testing
- **Test Scripts**: `Reservation.Tests/TestGrpcEndpoints.cs` - All endpoint tests
- **Docker Setup**: `docker-compose.dev.yml` - Complete development environment
- **Build Scripts**: `build-and-run.sh` & `test-grpc.sh` - Automated setup
- **Documentation**: `README.md` - Complete setup guide

### 🗄️ **Database Schema**
- **100 Realistic Reservations** with customer details
- **5 Default Statuses** (Pending, Confirmed, Cancelled, Completed, No-Show)
- **Resource Assignments** for testing
- **Proper Relationships** and foreign keys

### 🧪 **Testing Infrastructure**
- **13 gRPC Endpoint Tests** covering all functionality
- **Mock Services** for external dependencies
- **Health Checks** and monitoring
- **Performance Testing** examples

## 🚀 **Quick Start (5 Minutes)**

```bash
# 1. Make scripts executable
chmod +x build-and-run.sh test-grpc.sh

# 2. Build and run everything
./build-and-run.sh

# 3. Test gRPC endpoints
./test-grpc.sh
```

## 🎯 **Your Deliverables**

### **Phase 1: .NET 9.0 Upgrade** ⚡
- [ ] Update `Reservation.csproj` target framework to `net9.0`
- [ ] Update `Dockerfile` to use .NET 9.0 runtime
- [ ] Update all NuGet packages to .NET 9.0 compatible versions
- [ ] Ensure zero build warnings

### **Phase 2: Repository Refactoring** 🏗️
- [ ] Split `ReservationRepository` (2500+ lines) into focused repositories
- [ ] Create custom exception classes
- [ ] Implement proper error handling
- [ ] Add input validation
- [ ] Follow SOLID principles

### **Phase 3: Database & Testing** 🗄️
- [ ] Implement database seeding (already provided)
- [ ] Create comprehensive gRPC tests (already provided)
- [ ] Ensure all endpoints are testable
- [ ] Add unit test examples

### **Phase 4: Documentation** 📚
- [ ] Update README with setup instructions (already provided)
- [ ] Document API endpoints
- [ ] Provide troubleshooting guide
- [ ] Add performance testing examples

## 🔧 **Current Issues to Fix**

### ❌ **Critical Issues**
1. **Target Framework**: Still on `net8.0` (should be `net9.0`)
2. **Dockerfile**: Uses .NET 8.0 runtime
3. **Repository Size**: `ReservationRepository` is 2500+ lines
4. **Error Handling**: No custom exceptions
5. **Validation**: Missing input validation

### ⚠️ **Warning Issues**
1. **Nullable References**: 62+ warnings throughout codebase
2. **Async Patterns**: Missing await operators
3. **Parameter Capture**: Constructor parameter issues

## 📊 **Assessment Criteria**

### ✅ **Must Complete (Pass/Fail)**
- [ ] **Framework Upgrade**: Successfully upgrade to .NET 9.0
- [ ] **Docker Update**: Update Dockerfile to use .NET 9.0 runtime
- [ ] **Repository Split**: Break down 2500+ line repository into focused repositories (max 200 lines each)
- [ ] **Custom Exceptions**: Implement proper exception handling
- [ ] **Database Seeding**: Create and implement seed data
- [ ] **gRPC Testing**: Provide working gRPC test examples
- [ ] **Clean Build**: Zero warnings, successful build

### 🎯 **Quality Indicators (Bonus Points)**
- [ ] **Clean Architecture**: Proper separation of concerns
- [ ] **Error Handling**: Comprehensive exception handling
- [ ] **Validation**: Input validation implementation
- [ ] **Testing**: Unit test examples
- [ ] **Documentation**: Clear README with setup instructions
- [ ] **Performance**: Efficient database queries
- [ ] **Code Quality**: Clean, readable, maintainable code

## 🧪 **Testing Your Work**

### **1. Build Test**
```bash
dotnet build --configuration Release
# Should complete with ZERO warnings
```

### **2. Docker Test**
```bash
docker build -t reservation-service:latest ./Reservation
# Should build successfully
```

### **3. gRPC Test**
```bash
./test-grpc.sh
# Should test all 13 endpoints successfully
```

### **4. Integration Test**
```bash
./build-and-run.sh
# Should start all services and be ready for testing
```

## 📁 **File Structure Overview**

```
reservations/
├── DOTNET_UPGRADE_ASSESSMENT.md    # Your complete task list
├── README.md                       # Setup and usage guide
├── DEVELOPER_INSTRUCTIONS.md       # This file
├── build-and-run.sh               # Build and start script
├── test-grpc.sh                   # Test script
├── docker-compose.dev.yml         # Development environment
├── init-db.sql                    # Database initialization
├── Reservation/                   # Main service
│   ├── Data/
│   │   └── SeedData.cs           # Database seeding (provided)
│   ├── Models/                   # Entity models
│   ├── Repositories/             # Data access (needs refactoring)
│   ├── Services/                 # Business logic
│   ├── Program.cs                # Application entry point
│   └── Dockerfile                # Container config (needs update)
└── Reservation.Tests/            # Test project
    ├── GrpcTestClient.cs         # gRPC test client (provided)
    ├── TestGrpcEndpoints.cs      # Test execution (provided)
    └── Program.cs                # Test entry point
```

## 🎯 **Success Criteria**

Your assessment is successful when:
- ✅ .NET 9.0 upgrade completed successfully
- ✅ All repositories refactored (max 200 lines each)
- ✅ Custom exceptions implemented
- ✅ Database seeding working
- ✅ gRPC endpoints testable
- ✅ Zero build warnings
- ✅ Docker environment working
- ✅ Clear documentation provided

## 🚨 **Important Notes**

1. **Focus on Quality**: Clean, maintainable code is more important than speed
2. **Test Everything**: Ensure all gRPC endpoints work correctly
3. **Document Changes**: Explain what you changed and why
4. **Follow Patterns**: Use the existing code patterns and conventions
5. **Ask Questions**: If something is unclear, ask for clarification

## 🎉 **Good Luck!**

This assessment is designed to evaluate your ability to work with modern .NET applications, clean architecture, and comprehensive testing. Take your time, follow best practices, and create something you're proud of!

---

**Need Help?** Check the `DOTNET_UPGRADE_ASSESSMENT.md` file for detailed examples and requirements.
