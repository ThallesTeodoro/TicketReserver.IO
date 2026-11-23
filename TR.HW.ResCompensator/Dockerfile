# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /app

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

COPY TR.HW.ResCompensator.sln ./
COPY source/TR.HW.ResCompensator.Domain/TR.HW.ResCompensator.Domain.csproj source/TR.HW.ResCompensator.Domain/
COPY source/TR.HW.ResCompensator.Application/TR.HW.ResCompensator.Application.csproj source/TR.HW.ResCompensator.Application/
COPY source/TR.HW.ResCompensator.Infrastructure/TR.HW.ResCompensator.Infrastructure.csproj source/TR.HW.ResCompensator.Infrastructure/
COPY source/TR.HW.ResCompensator.Worker/TR.HW.ResCompensator.Worker.csproj source/TR.HW.ResCompensator.Worker/
COPY tests/TR.HW.ResCompensator.UnitTests/TR.HW.ResCompensator.UnitTests.csproj tests/TR.HW.ResCompensator.UnitTests/

RUN dotnet restore TR.HW.ResCompensator.sln

COPY . .
WORKDIR /app/source/TR.HW.ResCompensator.Worker
RUN dotnet publish -c Release -o /app/publish

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/runtime:10.0-preview AS runtime
WORKDIR /app

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "TR.HW.ResCompensator.Worker.dll"]
