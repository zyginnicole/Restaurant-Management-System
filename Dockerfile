# Render has no native .NET runtime, so the app is deployed as a Docker image.

# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY RestaurantManagementSystem.csproj ./
RUN dotnet restore RestaurantManagementSystem.csproj

COPY . .
RUN dotnet publish RestaurantManagementSystem.csproj -c Release -o /app/publish --no-restore

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

# Fonts for QuestPDF (PDF receipts/reports) and ClosedXML (Excel export) on Linux
RUN apt-get update \
    && apt-get install -y --no-install-recommends libfontconfig1 fonts-dejavu-core \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production \
    # Render terminates HTTPS in front of the app; trust its X-Forwarded-* headers
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true \
    TZ=Asia/Kuala_Lumpur

# Render tells the container which port to listen on through $PORT
EXPOSE 8080
ENTRYPOINT ["sh", "-c", "ASPNETCORE_HTTP_PORTS=${PORT:-8080} exec dotnet RestaurantManagementSystem.dll"]
