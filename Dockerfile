# Stage 1: build the React web client (src/IntelliMed.Client) — the API serves its dist/ as wwwroot.
FROM node:22 AS client
WORKDIR /client
COPY src/IntelliMed.Client/package.json src/IntelliMed.Client/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY src/IntelliMed.Client/ ./
RUN npm run build

# Stage 2: build the API. SkipClientBuild stops IntelliMed.Api.csproj running npm itself (this image
# has no Node) — the dist/ from stage 1 is copied in instead, and the publish step copies it to wwwroot.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY IntelliMed.sln .
COPY src/IntelliMed.Core/IntelliMed.Core.csproj src/IntelliMed.Core/
COPY src/IntelliMed.Infrastructure/IntelliMed.Infrastructure.csproj src/IntelliMed.Infrastructure/
COPY src/IntelliMed.Api/IntelliMed.Api.csproj src/IntelliMed.Api/
RUN dotnet restore src/IntelliMed.Api/IntelliMed.Api.csproj

COPY . .
COPY --from=client /client/dist src/IntelliMed.Client/dist
RUN dotnet publish src/IntelliMed.Api/IntelliMed.Api.csproj -c Release -o /app/publish -p:SkipClientBuild=true

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
RUN sed -i 's|Data Source=intellimed.db|Data Source=/app/data/intellimed.db|' appsettings.json
RUN mkdir -p /app/data && chmod 777 /app/data
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80
ENTRYPOINT ["dotnet", "IntelliMed.Api.dll"]
