# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /app

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

COPY TR.HW.Payment.sln ./
COPY source/TR.HW.Payment.Domain/TR.HW.Payment.Domain.csproj source/TR.HW.Payment.Domain/
COPY source/TR.HW.Payment.Application/TR.HW.Payment.Application.csproj source/TR.HW.Payment.Application/
COPY source/TR.HW.Payment.Infrastructure/TR.HW.Payment.Infrastructure.csproj source/TR.HW.Payment.Infrastructure/
COPY source/TR.HW.Payment.Worker/TR.HW.Payment.Worker.csproj source/TR.HW.Payment.Worker/
COPY tests/TR.HW.Payment.UnitTests/TR.HW.Payment.UnitTests.csproj tests/TR.HW.Payment.UnitTests/

RUN dotnet restore TR.HW.Payment.sln

COPY . .
WORKDIR /app/source/TR.HW.Payment.Worker
RUN dotnet publish -c Release -o /app/publish

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/runtime:10.0-preview AS runtime
WORKDIR /app

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "TR.HW.Payment.Worker.dll"]
