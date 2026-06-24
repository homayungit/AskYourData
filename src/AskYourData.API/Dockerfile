# ── Build stage ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore (cache layer)
COPY ErpRagChatbot.slnx ./
COPY src/ErpRagChatbot.Core/ErpRagChatbot.Core.csproj               src/ErpRagChatbot.Core/
COPY src/ErpRagChatbot.Infrastructure/ErpRagChatbot.Infrastructure.csproj  src/ErpRagChatbot.Infrastructure/
COPY src/ErpRagChatbot.API/ErpRagChatbot.API.csproj                 src/ErpRagChatbot.API/
RUN dotnet restore src/ErpRagChatbot.API/ErpRagChatbot.API.csproj

# Copy and publish
COPY . .
RUN dotnet publish src/ErpRagChatbot.API/ErpRagChatbot.API.csproj \
    -c Release -o /app/publish --no-restore

# ── Runtime stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080

RUN apt-get update && apt-get install -y curl --no-install-recommends && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "ErpRagChatbot.API.dll"]
