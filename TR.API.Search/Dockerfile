# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /app

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

COPY TR.API.Search.sln ./
COPY source/TR.API.Search.Domain/TR.API.Search.Domain.csproj source/TR.API.Search.Domain/
COPY source/TR.API.Search.Application/TR.API.Search.Application.csproj source/TR.API.Search.Application/
COPY source/TR.API.Search.Infrastructure/TR.API.Search.Infrastructure.csproj source/TR.API.Search.Infrastructure/
COPY source/TR.API.Search.WebApi/TR.API.Search.WebApi.csproj source/TR.API.Search.WebApi/
COPY tests/TR.API.Search.UnitTests/TR.API.Search.UnitTests.csproj tests/TR.API.Search.UnitTests/

RUN dotnet restore TR.API.Search.sln

COPY . .
WORKDIR /app/source/TR.API.Search.WebApi
RUN dotnet publish -c Release -o /app/publish

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS runtime
WORKDIR /app

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
ENV ASPNETCORE_HTTP_PORTS=8080

COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "TR.API.Search.WebApi.dll"]
