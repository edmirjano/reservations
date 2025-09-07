# Use the official ASP.NET Core runtime image
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
