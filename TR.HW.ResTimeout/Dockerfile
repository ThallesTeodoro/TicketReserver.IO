# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /app

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

COPY TR.HW.ResTimeout.sln ./
COPY source/TR.HW.ResTimeout/TR.HW.ResTimeout.csproj source/TR.HW.ResTimeout/
COPY tests/TR.HW.ResTimeout.UnitTests/TR.HW.ResTimeout.UnitTests.csproj tests/TR.HW.ResTimeout.UnitTests/

RUN dotnet restore TR.HW.ResTimeout.sln

COPY . .
WORKDIR /app/source/TR.HW.ResTimeout
RUN dotnet publish -c Release -o /app/publish

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/runtime:10.0-preview AS runtime
WORKDIR /app

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "TR.HW.ResTimeout.dll"]
