# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /app

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

COPY TR.HW.Notification.sln ./
COPY source/TR.HW.Notification.Domain/TR.HW.Notification.Domain.csproj source/TR.HW.Notification.Domain/
COPY source/TR.HW.Notification.Application/TR.HW.Notification.Application.csproj source/TR.HW.Notification.Application/
COPY source/TR.HW.Notification.Infrastructure/TR.HW.Notification.Infrastructure.csproj source/TR.HW.Notification.Infrastructure/
COPY source/TR.HW.Notification.Worker/TR.HW.Notification.Worker.csproj source/TR.HW.Notification.Worker/
COPY tests/TR.HW.Notification.UnitTests/TR.HW.Notification.UnitTests.csproj tests/TR.HW.Notification.UnitTests/

RUN dotnet restore TR.HW.Notification.sln

COPY . .
WORKDIR /app/source/TR.HW.Notification.Worker
RUN dotnet publish -c Release -o /app/publish

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/runtime:10.0-preview AS runtime
WORKDIR /app

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "TR.HW.Notification.Worker.dll"]
