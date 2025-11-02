# Multi-stage Dockerfile for Gov2Biz LicenseService
# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["Gov2Biz.sln", "."]
COPY ["LicenseService/src/Gov2Biz.LicenseService.csproj", "LicenseService/src/"]
COPY ["Shared/src/Gov2Biz.Shared.csproj", "Shared/src/"]

# Restore dependencies
RUN dotnet restore "LicenseService/src/Gov2Biz.LicenseService.csproj"

# Copy all source files
COPY . .

# Build the project
WORKDIR "/src/LicenseService/src"
RUN dotnet build "Gov2Biz.LicenseService.csproj" -c Release -o /app/build

# Publish Stage
FROM build AS publish
RUN dotnet publish "Gov2Biz.LicenseService.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Create logs directory
RUN mkdir -p /Logs && chmod 777 /Logs

# Copy published files
COPY --from=publish /app/publish .

# Expose ports
EXPOSE 80
EXPOSE 443

# Set environment variables
ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_ENVIRONMENT=Production

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost/healthz || exit 1

ENTRYPOINT ["dotnet", "Gov2Biz.LicenseService.dll"]
