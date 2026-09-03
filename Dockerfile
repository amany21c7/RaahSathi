# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /source

# Copy all project files and restore dependencies
COPY . .
RUN dotnet restore RaahSathi.csproj

# Publish the application in Release mode
RUN dotnet publish RaahSathi.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Install Node.js & npm in runtime container for WhatsApp Gateway microservice
RUN apt-get update && apt-get install -y --no-install-recommends nodejs npm && rm -rf /var/lib/apt/lists/*

# Copy published .NET application
COPY --from=build /app/publish .

# Copy whatsapp-gateway microservice and install its production dependencies
COPY whatsapp-gateway ./whatsapp-gateway
RUN cd whatsapp-gateway && npm install --omit=dev

# Render environment and stability configurations
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_USE_POLLING_FILE_WATCHER=true
ENV DOTNET_EnableDiagnostics=0

EXPOSE 8080

# Start whatsapp-gateway in background on port 5005, then start .NET app in foreground on port 8080
ENTRYPOINT ["sh", "-c", "node /app/whatsapp-gateway/server.js & if [ -f RaahSathi.Web.dll ]; then exec dotnet RaahSathi.Web.dll; else exec dotnet RaahSathi.dll; fi"]

