# Build stage: the SDK image has the compiler and is discarded afterwards.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Copy project files first and restore on their own layer. Docker caches this,
# so editing source code does not re-download every NuGet package.
COPY src/MerchantOnboarding.Api/MerchantOnboarding.Api.csproj src/MerchantOnboarding.Api/
RUN dotnet restore src/MerchantOnboarding.Api/MerchantOnboarding.Api.csproj

COPY src/ src/
RUN dotnet publish src/MerchantOnboarding.Api/MerchantOnboarding.Api.csproj \
    -c Release -o /app --no-restore

# Runtime stage: no SDK, no compiler, no source - a much smaller image
# with less to attack.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# The aspnet image ships a non-root "app" user (uid 1654); use it rather
# than running as root.
USER app

COPY --from=build --chown=app:app /app .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "MerchantOnboarding.Api.dll"]
