# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /app

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

COPY TR.HW.Ticketing.sln ./
COPY source/TR.HW.Ticketing.Domain/TR.HW.Ticketing.Domain.csproj source/TR.HW.Ticketing.Domain/
COPY source/TR.HW.Ticketing.Application/TR.HW.Ticketing.Application.csproj source/TR.HW.Ticketing.Application/
COPY source/TR.HW.Ticketing.Infrastructure/TR.HW.Ticketing.Infrastructure.csproj source/TR.HW.Ticketing.Infrastructure/
COPY source/TR.HW.Ticketing.Worker/TR.HW.Ticketing.Worker.csproj source/TR.HW.Ticketing.Worker/
COPY tests/TR.HW.Ticketing.UnitTests/TR.HW.Ticketing.UnitTests.csproj tests/TR.HW.Ticketing.UnitTests/

RUN dotnet restore TR.HW.Ticketing.sln

COPY . .
WORKDIR /app/source/TR.HW.Ticketing.Worker
RUN dotnet publish -c Release -o /app/publish

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/runtime:10.0-preview AS runtime
WORKDIR /app

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "TR.HW.Ticketing.Worker.dll"]
