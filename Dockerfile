# Build from the repository root:
# docker build -t nestify-api .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Nestify.Shared/Nestify.Shared.csproj src/Nestify.Shared/
COPY src/Nestify.Api/Nestify.Api.csproj src/Nestify.Api/
RUN dotnet restore src/Nestify.Api/Nestify.Api.csproj

COPY src/Nestify.Shared/ src/Nestify.Shared/
COPY src/Nestify.Api/ src/Nestify.Api/
RUN dotnet publish src/Nestify.Api/Nestify.Api.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    --no-self-contained

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Nestify.Api.dll"]
