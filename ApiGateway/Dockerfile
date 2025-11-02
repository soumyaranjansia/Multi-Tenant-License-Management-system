# Multi-stage Dockerfile for Gov2Biz API Gateway
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["Gov2Biz.sln", "."]
COPY ["ApiGateway/Gov2Biz.ApiGateway.csproj", "ApiGateway/"]

RUN dotnet restore "ApiGateway/Gov2Biz.ApiGateway.csproj"

COPY . .

WORKDIR "/src/ApiGateway"
RUN dotnet build "Gov2Biz.ApiGateway.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Gov2Biz.ApiGateway.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

RUN mkdir -p /Logs && chmod 777 /Logs

COPY --from=publish /app/publish .
COPY ApiGateway/ocelot.json .

EXPOSE 80
EXPOSE 443

ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_ENVIRONMENT=Production

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost/ || exit 1

ENTRYPOINT ["dotnet", "Gov2Biz.ApiGateway.dll"]
