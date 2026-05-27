FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY OsrsTracker.Domain/OsrsTracker.Domain.csproj OsrsTracker.Domain/
COPY OsrsTracker.Api/OsrsTracker.Api.csproj OsrsTracker.Api/
RUN dotnet restore OsrsTracker.Api/OsrsTracker.Api.csproj
COPY . .
RUN dotnet publish OsrsTracker.Api/OsrsTracker.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "OsrsTracker.Api.dll"]
