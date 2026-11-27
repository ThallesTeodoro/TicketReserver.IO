# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /app

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

COPY TR.HW.Repl.sln ./
COPY source/TR.HW.Repl.Domain/TR.HW.Repl.Domain.csproj source/TR.HW.Repl.Domain/
COPY source/TR.HW.Repl.Application/TR.HW.Repl.Application.csproj source/TR.HW.Repl.Application/
COPY source/TR.HW.Repl.Infrastructure/TR.HW.Repl.Infrastructure.csproj source/TR.HW.Repl.Infrastructure/
COPY source/TR.HW.Repl.Worker/TR.HW.Repl.Worker.csproj source/TR.HW.Repl.Worker/
COPY tests/TR.HW.Repl.UnitTests/TR.HW.Repl.UnitTests.csproj tests/TR.HW.Repl.UnitTests/

RUN dotnet restore TR.HW.Repl.sln

COPY . .
WORKDIR /app/source/TR.HW.Repl.Worker
RUN dotnet publish -c Release -o /app/publish

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/runtime:10.0-preview AS runtime
WORKDIR /app

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "TR.HW.Repl.Worker.dll"]
