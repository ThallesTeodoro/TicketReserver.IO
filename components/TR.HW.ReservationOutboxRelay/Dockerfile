# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /app

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

COPY TR.HW.ReservationOutboxRelay.sln ./
COPY source/TR.HW.ReservationOutboxRelay/TR.HW.ReservationOutboxRelay.csproj source/TR.HW.ReservationOutboxRelay/
COPY tests/TR.HW.ReservationOutboxRelay.UnitTests/TR.HW.ReservationOutboxRelay.UnitTests.csproj tests/TR.HW.ReservationOutboxRelay.UnitTests/

RUN dotnet restore TR.HW.ReservationOutboxRelay.sln

COPY . .
WORKDIR /app/source/TR.HW.ReservationOutboxRelay
RUN dotnet publish -c Release -o /app/publish

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/runtime:10.0-preview AS runtime
WORKDIR /app

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "TR.HW.ReservationOutboxRelay.dll"]
