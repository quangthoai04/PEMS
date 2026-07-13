# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Build context is the backend/ folder; copy all projects
COPY . .

RUN dotnet restore PEMS.Api/PEMS.Api.csproj
RUN dotnet publish PEMS.Api/PEMS.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Railway injects PORT at runtime; fall back to 8080 for local runs
ENTRYPOINT ["/bin/sh", "-c", "exec dotnet PEMS.Api.dll --urls http://0.0.0.0:${PORT:-8080}"]
