FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY nuget.config ./
COPY src/InternLink.Web/InternLink.Web.csproj src/InternLink.Web/
RUN dotnet restore src/InternLink.Web/InternLink.Web.csproj

COPY src/InternLink.Web/ src/InternLink.Web/
COPY db/scripts/ db/scripts/
WORKDIR /src/src/InternLink.Web
RUN dotnet publish InternLink.Web.csproj --configuration Release --no-restore --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /app/data/resumes \
    && chown -R 1654:1654 /app

COPY --from=build --chown=1654:1654 /app/publish/ ./
COPY --from=build --chown=1654:1654 /src/db/scripts/ ./db/scripts/

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER 1654

HEALTHCHECK --interval=30s --timeout=5s --start-period=60s --retries=3 \
    CMD curl --fail --silent http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "InternLink.Web.dll"]
