#!/bin/bash

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${BLUE}🏗️ Building .NET 9.0 Reservation Service...${NC}"
echo "=================================================="

# Check if .NET 9.0 is installed
if ! dotnet --version | grep -q "9\."; then
    echo -e "${RED}❌ .NET 9.0 is not installed. Please install .NET 9.0 SDK first.${NC}"
    echo "Download from: https://dotnet.microsoft.com/download/dotnet/9.0"
    exit 1
fi

echo -e "${GREEN}✅ .NET 9.0 SDK found: $(dotnet --version)${NC}"

# Clean previous builds
echo -e "${YELLOW}🧹 Cleaning previous builds...${NC}"
dotnet clean
dotnet restore

# Build the solution
echo -e "${YELLOW}🔨 Building solution...${NC}"
dotnet build --configuration Release

if [ $? -ne 0 ]; then
    echo -e "${RED}❌ Build failed! Please fix the errors above.${NC}"
    exit 1
fi

echo -e "${GREEN}✅ Build completed successfully!${NC}"

# Publish the application
echo -e "${YELLOW}📦 Publishing application...${NC}"
dotnet publish Reservation/Reservation.csproj --configuration Release --output Reservation/publish

if [ $? -ne 0 ]; then
    echo -e "${RED}❌ Publish failed! Please fix the errors above.${NC}"
    exit 1
fi

echo -e "${GREEN}✅ Application published successfully!${NC}"

# Build Docker image
echo -e "${YELLOW}🐳 Building Docker image...${NC}"
docker build -t reservation-service:latest ./Reservation

if [ $? -ne 0 ]; then
    echo -e "${RED}❌ Docker build failed! Please fix the errors above.${NC}"
    exit 1
fi

echo -e "${GREEN}✅ Docker image built successfully!${NC}"

# Start services with Docker Compose
echo -e "${YELLOW}🚀 Starting services with Docker Compose...${NC}"
docker-compose -f docker-compose.dev.yml up --build -d

if [ $? -ne 0 ]; then
    echo -e "${RED}❌ Failed to start services! Please check Docker Compose configuration.${NC}"
    exit 1
fi

echo -e "${GREEN}✅ Services started successfully!${NC}"
echo ""
echo -e "${BLUE}🎉 Application is running!${NC}"
echo "=================================================="
echo -e "${GREEN}📡 gRPC endpoint: http://localhost:5000${NC}"
echo -e "${GREEN}🗄️ PostgreSQL: localhost:5432${NC}"
echo -e "${GREEN}🔴 Redis: localhost:6379${NC}"
echo ""
echo -e "${YELLOW}📋 Available commands:${NC}"
echo "  • View logs: docker-compose -f docker-compose.dev.yml logs -f"
echo "  • Stop services: docker-compose -f docker-compose.dev.yml down"
echo "  • Test gRPC: ./test-grpc.sh"
echo "  • Database access: psql -h localhost -p 5432 -U reservation -d reservation"
echo ""
echo -e "${BLUE}⏳ Waiting for services to be ready...${NC}"
sleep 10

# Test if the service is responding
echo -e "${YELLOW}🧪 Testing service health...${NC}"
if curl -f http://localhost:5000/health > /dev/null 2>&1; then
    echo -e "${GREEN}✅ Service is healthy and responding!${NC}"
else
    echo -e "${YELLOW}⚠️ Service might still be starting up. Check logs with: docker-compose -f docker-compose.dev.yml logs -f${NC}"
fi

echo ""
echo -e "${GREEN}🎯 Ready to test gRPC endpoints!${NC}"
echo "Run: ./test-grpc.sh"
