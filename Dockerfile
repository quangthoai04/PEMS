# syntax=docker/dockerfile:1

# ── Stage 1: build & publish ──────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy only the backend .csproj files first so `dotnet restore` is cached
# independently of source changes.
COPY backend/PEMS.Domain/PEMS.Domain.csproj backend/PEMS.Domain/
COPY backend/PEMS.Application/PEMS.Application.csproj backend/PEMS.Application/
COPY backend/PEMS.Infrastructure/PEMS.Infrastructure.csproj backend/PEMS.Infrastructure/
COPY backend/PEMS.Api/PEMS.Api.csproj backend/PEMS.Api/
RUN dotnet restore backend/PEMS.Api/PEMS.Api.csproj

# Now copy the rest of the backend source and publish.
COPY backend/PEMS.Domain/ backend/PEMS.Domain/
COPY backend/PEMS.Application/ backend/PEMS.Application/
COPY backend/PEMS.Infrastructure/ backend/PEMS.Infrastructure/
COPY backend/PEMS.Api/ backend/PEMS.Api/

# Drop dev/testing/example config before publish — the SDK would otherwise
# copy every appsettings.*.json into the publish output, and none of these
# are needed (or wanted) in the runtime image. Real config comes from
# Railway environment variables.
RUN rm -f \
    backend/PEMS.Api/appsettings.Development.json \
    backend/PEMS.Api/appsettings.Development.example.json \
    backend/PEMS.Api/appsettings.Testing.example.json

RUN dotnet publish backend/PEMS.Api/PEMS.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ── Stage 2: runtime ───────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Run as a non-root user; own /app explicitly so appuser can read+execute
# everything in it regardless of the base image's default umask.
RUN useradd --uid 10001 --create-home --shell /usr/sbin/nologin appuser \
    && chown appuser:appuser /app
USER appuser

COPY --from=build --chown=appuser:appuser /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_EnableDiagnostics=0 \
    PORT=8080

# Railway overrides PORT at runtime; Program.cs reads it and binds
# 0.0.0.0:$PORT. The ENV default above covers plain `docker run` with no
# -e PORT (e.g. local testing).
EXPOSE 8080

ENTRYPOINT ["dotnet", "PEMS.Api.dll"]
