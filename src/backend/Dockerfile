# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0-windowsservercore-ltsc2022 AS build
WORKDIR /src

COPY ADManager.sln .
COPY ADManager.Api/ADManager.Api.csproj ADManager.Api/
COPY ADManager.Application/ADManager.Application.csproj ADManager.Application/
COPY ADManager.Domain/ADManager.Domain.csproj ADManager.Domain/
COPY ADManager.Infrastructure/ADManager.Infrastructure.csproj ADManager.Infrastructure/
RUN dotnet restore ADManager.Api/ADManager.Api.csproj

COPY ADManager.Api/ ADManager.Api/
COPY ADManager.Application/ ADManager.Application/
COPY ADManager.Domain/ ADManager.Domain/
COPY ADManager.Infrastructure/ ADManager.Infrastructure/
RUN dotnet publish ADManager.Api/ADManager.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-windowsservercore-ltsc2022 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
COPY frontend-dist/ ./wwwroot/

EXPOSE 8080 8443

HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
    CMD powershell -command "try { (Invoke-WebRequest -UseBasicParsing http://localhost:8080/health/ready).StatusCode -eq 200 } catch { exit 1 }"

ENTRYPOINT ["dotnet", "ADManager.Api.dll"]
