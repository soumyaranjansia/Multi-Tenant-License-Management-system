# Multi-stage Dockerfile for Gov2Biz DocumentService
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["Gov2Biz.sln", "."]
COPY ["DocumentService/src/Gov2Biz.DocumentService.csproj", "DocumentService/src/"]
COPY ["Shared/src/Gov2Biz.Shared.csproj", "Shared/src/"]

RUN dotnet restore "DocumentService/src/Gov2Biz.DocumentService.csproj"

COPY . .

WORKDIR "/src/DocumentService/src"
RUN dotnet build "Gov2Biz.DocumentService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Gov2Biz.DocumentService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

RUN mkdir -p /Logs && chmod 777 /Logs && \
    mkdir -p /app/Documents && chmod 777 /app/Documents

COPY --from=publish /app/publish .

EXPOSE 80
EXPOSE 443

ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_ENVIRONMENT=Production

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost/healthz || exit 1

ENTRYPOINT ["dotnet", "Gov2Biz.DocumentService.dll"]
