# Multi-stage Dockerfile for Gov2Biz MVCFrontend
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["Gov2Biz.sln", "."]
COPY ["MVCFrontend/src/Gov2Biz.MVCFrontend.csproj", "MVCFrontend/src/"]
COPY ["Shared/src/Gov2Biz.Shared.csproj", "Shared/src/"]

RUN dotnet restore "MVCFrontend/src/Gov2Biz.MVCFrontend.csproj"

COPY . .

WORKDIR "/src/MVCFrontend/src"
RUN dotnet build "Gov2Biz.MVCFrontend.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Gov2Biz.MVCFrontend.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

COPY --from=publish /app/publish .

EXPOSE 80
EXPOSE 443

ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "Gov2Biz.MVCFrontend.dll"]
