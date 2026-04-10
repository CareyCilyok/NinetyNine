# ─── Stage 1: build ───────────────────────────────────────────────────────────
# Use the full SDK image (not alpine) for the build stage. Alpine-based SDK images
# can fail with certain NuGet packages that rely on native code (e.g. MongoDB
# driver native libraries, ImageSharp). The build stage is discarded after
# publish, so its size does not affect the final image.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# ── Restore layer (cache-optimised) ───────────────────────────────────────────
# Copy only the .csproj files for the production dependency chain first:
#   Model <- Repository <- Services <- Web
#
# Docker re-uses this layer on subsequent builds as long as no .csproj has
# changed, even when source code has changed — giving high cache hit rates for
# the expensive `dotnet restore` step.
#
# We restore against the Web project only (not the whole solution) because the
# test projects are excluded from the build context via .dockerignore and are not
# needed to publish the web app.
COPY NinetyNine.sln .

COPY src/NinetyNine.Model/NinetyNine.Model.csproj           src/NinetyNine.Model/
COPY src/NinetyNine.Repository/NinetyNine.Repository.csproj src/NinetyNine.Repository/
COPY src/NinetyNine.Services/NinetyNine.Services.csproj     src/NinetyNine.Services/
COPY src/NinetyNine.Web/NinetyNine.Web.csproj               src/NinetyNine.Web/

RUN dotnet restore src/NinetyNine.Web/NinetyNine.Web.csproj

# ── Full source copy + publish ────────────────────────────────────────────────
# Copy source *after* restore so that source-only changes do not bust the
# restore cache layer above.
COPY src/ src/

RUN dotnet publish src/NinetyNine.Web/NinetyNine.Web.csproj \
        -c Release \
        -o /app/publish \
        --no-restore

# ─── Stage 2: runtime ─────────────────────────────────────────────────────────
# aspnet:8.0-alpine produces an image ~100-130 MB vs ~220 MB for the Debian
# variant. The official Microsoft aspnet images ship with a pre-created non-root
# user whose UID is exposed via the $APP_UID build argument (default 1654).
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime

WORKDIR /app

# Copy only the published output — no SDK, no source code.
COPY --from=build /app/publish .

# ── Runtime configuration ─────────────────────────────────────────────────────
# Bind to non-privileged port 8080 so the container runs without NET_BIND_SERVICE
# capability. The reverse-proxy / compose port mapping handles 80 -> 8080.
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Disable ASP.NET Core's file-watcher to save CPU in production.
ENV DOTNET_USE_POLLING_FILE_WATCHER=false

EXPOSE 8080

# ── Non-root execution ────────────────────────────────────────────────────────
# The official Microsoft runtime images ship the 'app' user with UID $APP_UID.
# Switching to it here satisfies CIS Docker Benchmark 4.1 (non-root container).
USER $APP_UID

# ── Health check ──────────────────────────────────────────────────────────────
# The /healthz endpoint is wired in Program.cs via MapHealthChecks.
# Uses wget (busybox) which is present on alpine without installing extra packages.
# --spider makes wget exit non-zero on HTTP error responses (4xx/5xx).
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD wget --quiet --tries=1 --spider http://localhost:8080/healthz || exit 1

ENTRYPOINT ["dotnet", "NinetyNine.Web.dll"]
