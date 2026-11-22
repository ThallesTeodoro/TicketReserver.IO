# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /app

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

COPY TR.HW.Refund.sln ./
COPY source/TR.HW.Refund.Domain/TR.HW.Refund.Domain.csproj source/TR.HW.Refund.Domain/
COPY source/TR.HW.Refund.Application/TR.HW.Refund.Application.csproj source/TR.HW.Refund.Application/
COPY source/TR.HW.Refund.Infrastructure/TR.HW.Refund.Infrastructure.csproj source/TR.HW.Refund.Infrastructure/
COPY source/TR.HW.Refund.Worker/TR.HW.Refund.Worker.csproj source/TR.HW.Refund.Worker/
COPY tests/TR.HW.Refund.UnitTests/TR.HW.Refund.UnitTests.csproj tests/TR.HW.Refund.UnitTests/

RUN dotnet restore TR.HW.Refund.sln

COPY . .
WORKDIR /app/source/TR.HW.Refund.Worker
RUN dotnet publish -c Release -o /app/publish

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/runtime:10.0-preview AS runtime
WORKDIR /app

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "TR.HW.Refund.Worker.dll"]
