# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /app

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

COPY TR.API.Events.sln ./
COPY source/TR.API.Events.Domain/TR.API.Events.Domain.csproj source/TR.API.Events.Domain/
COPY source/TR.API.Events.Application/TR.API.Events.Application.csproj source/TR.API.Events.Application/
COPY source/TR.API.Events.Infrastructure/TR.API.Events.Infrastructure.csproj source/TR.API.Events.Infrastructure/
COPY source/TR.API.Events.WebApi/TR.API.Events.WebApi.csproj source/TR.API.Events.WebApi/
COPY tests/TR.API.Events.UnitTests/TR.API.Events.UnitTests.csproj tests/TR.API.Events.UnitTests/

RUN dotnet restore TR.API.Events.sln

COPY . .
WORKDIR /app/source/TR.API.Events.WebApi
RUN dotnet publish -c Release -o /app/publish

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS runtime
WORKDIR /app

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
ENV ASPNETCORE_HTTP_PORTS=8080

COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "TR.API.Events.WebApi.dll"]
