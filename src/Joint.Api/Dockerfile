# Build from the repository root:  docker build -f src/Joint.Api/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /repo

COPY global.json Directory.Build.props ./
COPY src/Joint.Contracts/Joint.Contracts.csproj src/Joint.Contracts/
COPY src/Joint.Api/Joint.Api.csproj src/Joint.Api/
RUN dotnet restore src/Joint.Api/Joint.Api.csproj

COPY src/Joint.Contracts src/Joint.Contracts
COPY src/Joint.Api src/Joint.Api
RUN dotnet publish src/Joint.Api/Joint.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:11.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Joint.Api.dll"]
