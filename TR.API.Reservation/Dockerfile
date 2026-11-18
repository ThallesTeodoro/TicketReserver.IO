# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /app

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

COPY TR.API.Reservation.sln ./
COPY source/TR.API.Reservation.Domain/TR.API.Reservation.Domain.csproj source/TR.API.Reservation.Domain/
COPY source/TR.API.Reservation.Application/TR.API.Reservation.Application.csproj source/TR.API.Reservation.Application/
COPY source/TR.API.Reservation.Infrastructure/TR.API.Reservation.Infrastructure.csproj source/TR.API.Reservation.Infrastructure/
COPY source/TR.API.Reservation.WebApi/TR.API.Reservation.WebApi.csproj source/TR.API.Reservation.WebApi/
COPY tests/TR.API.Reservation.UnitTests/TR.API.Reservation.UnitTests.csproj tests/TR.API.Reservation.UnitTests/

RUN dotnet restore TR.API.Reservation.sln

COPY . .
WORKDIR /app/source/TR.API.Reservation.WebApi
RUN dotnet publish -c Release -o /app/publish

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS runtime
WORKDIR /app

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
ENV ASPNETCORE_HTTP_PORTS=8080

COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "TR.API.Reservation.WebApi.dll"]
