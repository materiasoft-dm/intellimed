# Stage 1: Build
# IntelliMed.Api references IntelliMed.Web (Blazor WASM), IntelliMed.Core, and IntelliMed.Infrastructure.
# `dotnet publish` on the API project automatically builds the Blazor client and copies its
# wwwroot/_framework output into the API's publish folder.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY IntelliMed.sln .
COPY src/IntelliMed.Core/IntelliMed.Core.csproj src/IntelliMed.Core/
COPY src/IntelliMed.Infrastructure/IntelliMed.Infrastructure.csproj src/IntelliMed.Infrastructure/
COPY src/IntelliMed.Api/IntelliMed.Api.csproj src/IntelliMed.Api/
COPY src/IntelliMed.Web/IntelliMed.Web.csproj src/IntelliMed.Web/
COPY src/IntelliMed.Desktop/IntelliMed.Desktop.csproj src/IntelliMed.Desktop/
COPY src/IntelliMed.Tests/IntelliMed.Tests.csproj src/IntelliMed.Tests/
RUN dotnet restore src/IntelliMed.Api/IntelliMed.Api.csproj

COPY . .
RUN dotnet publish src/IntelliMed.Api/IntelliMed.Api.csproj -c Release -o /app/publish

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
RUN sed -i 's|Data Source=intellimed.db|Data Source=/app/data/intellimed.db|' appsettings.json
RUN mkdir -p /app/data && chmod 777 /app/data
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80
ENTRYPOINT ["dotnet", "IntelliMed.Api.dll"]