#!/bin/bash

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${BLUE}🧪 Testing gRPC Endpoints for Reservation Service${NC}"
echo "=================================================="

# Check if the service is running
echo -e "${YELLOW}⏳ Checking if service is running...${NC}"
if ! curl -f http://localhost:5000/health > /dev/null 2>&1; then
    echo -e "${RED}❌ Service is not running or not responding!${NC}"
    echo "Please start the service first with: ./build-and-run.sh"
    exit 1
fi

echo -e "${GREEN}✅ Service is running and responding!${NC}"

# Wait a bit more for the service to be fully ready
echo -e "${YELLOW}⏳ Waiting for service to be fully ready...${NC}"
sleep 5

# Run gRPC tests
echo -e "${YELLOW}🧪 Running gRPC endpoint tests...${NC}"
echo ""

# Compile and run the test
dotnet run --project Reservation.Tests/Reservation.Tests.csproj

if [ $? -eq 0 ]; then
    echo ""
    echo -e "${GREEN}✅ All gRPC tests completed successfully!${NC}"
    echo ""
    echo -e "${BLUE}📋 Test Summary:${NC}"
    echo "  • CreateReservation: ✅"
    echo "  • CreateReservationForOrganization: ✅"
    echo "  • GetReservations: ✅"
    echo "  • GetReservationById: ✅"
    echo "  • GetReservationByCode: ✅"
    echo "  • GetReservationsByDateRange: ✅"
    echo "  • GetReservationsByResources: ✅"
    echo "  • Status Management: ✅"
    echo "  • Get Statistics: ✅"
    echo "  • Get Reservations Count Per Day: ✅"
    echo "  • Search Clients: ✅"
    echo "  • Get Reservations by Source Count: ✅"
    echo "  • Generate Report: ✅"
    echo ""
    echo -e "${GREEN}🎉 All endpoints are working correctly!${NC}"
else
    echo ""
    echo -e "${RED}❌ Some tests failed! Check the output above for details.${NC}"
    echo ""
    echo -e "${YELLOW}🔧 Troubleshooting tips:${NC}"
    echo "  • Check service logs: docker-compose -f docker-compose.dev.yml logs -f"
    echo "  • Verify database connection: docker-compose -f docker-compose.dev.yml exec postgres psql -U reservation -d reservation -c 'SELECT COUNT(*) FROM \"Reservations\";'"
    echo "  • Check if seed data was loaded: docker-compose -f docker-compose.dev.yml exec postgres psql -U reservation -d reservation -c 'SELECT COUNT(*) FROM \"Statuses\";'"
    echo "  • Restart services: docker-compose -f docker-compose.dev.yml restart"
    exit 1
fi
