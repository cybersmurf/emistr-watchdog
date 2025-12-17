# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files
COPY Emistr.sln ./
COPY src/Emistr.LicenseService/*.csproj ./src/Emistr.LicenseService/
COPY src/Emistr.LicenseService.Tests/*.csproj ./src/Emistr.LicenseService.Tests/
COPY src/Emistr.LicenseClient/*.csproj ./src/Emistr.LicenseClient/
COPY src/Emistr.Watchdog/*.csproj ./src/Emistr.Watchdog/
COPY src/Emistr.Watchdog.Tests/*.csproj ./src/Emistr.Watchdog.Tests/

# Restore only for this project
RUN dotnet restore src/Emistr.Watchdog/Emistr.Watchdog.csproj

# Copy source code
COPY src/Emistr.Watchdog/ ./src/Emistr.Watchdog/

# Build and publish
RUN dotnet publish src/Emistr.Watchdog/Emistr.Watchdog.csproj -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

# Create non-root user
RUN groupadd -r appgroup && useradd -r -g appgroup appuser

# Copy published files
COPY --from=build /app/publish .

# Create logs directory
RUN mkdir -p /app/logs && chown -R appuser:appgroup /app

# Switch to non-root user
USER appuser

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:5050/api/health || exit 1

# Expose dashboard port
EXPOSE 5050

# Environment
ENV ASPNETCORE_URLS=http://+:5050
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "Emistr.Watchdog.dll"]
