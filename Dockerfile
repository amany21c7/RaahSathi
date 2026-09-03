# Stage 1: Build WhatsApp Gateway using official Node.js (zero apt-get, clean Linux dependencies)
FROM node:20-bookworm-slim AS node-build
WORKDIR /app/whatsapp-gateway
COPY whatsapp-gateway/package*.json ./
RUN npm install --omit=dev
COPY whatsapp-gateway/ ./

# Stage 2: Build .NET application
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS dotnet-build
WORKDIR /source
COPY . .
RUN dotnet restore RaahSathi.csproj
RUN dotnet publish RaahSathi.csproj -c Release -o /app/publish

# Stage 3: Final runtime container
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Copy Node binary directly from official Node image
COPY --from=node-build /usr/local/bin/node /usr/local/bin/node

# Copy pre-installed WhatsApp gateway (with all production node_modules ready to run)
COPY --from=node-build /app/whatsapp-gateway ./whatsapp-gateway

# Copy published .NET application
COPY --from=dotnet-build /app/publish .

# Render environment and stability configurations
ENV PORT=8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_USE_POLLING_FILE_WATCHER=true
ENV DOTNET_EnableDiagnostics=0

EXPOSE 8080


# Start whatsapp-gateway in background on port 5005, then start .NET app in foreground on port 8080
ENTRYPOINT ["sh", "-c", "node /app/whatsapp-gateway/server.js & if [ -f RaahSathi.Web.dll ]; then exec dotnet RaahSathi.Web.dll; else exec dotnet RaahSathi.dll; fi"]


