FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# copy files and do restore
COPY src/MatchmakingService.Api/MatchmakingService.Api.csproj src/MatchmakingService.Api/
COPY src/MatchmakingService.Application/MatchmakingService.Application.csproj src/MatchmakingService.Application/
RUN dotnet restore src/MatchmakingService.Api/MatchmakingService.Api.csproj

# Copy all source code and build
COPY src/ src/
RUN dotnet publish src/MatchmakingService.Api/MatchmakingService.Api.csproj -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MatchmakingService.Api.dll"]