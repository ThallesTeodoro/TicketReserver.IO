# Tasks: Setup TR.API.Events (.NET 10 & Modular Compose)

## 1. Project Guidelines Update

- [x] 1.1 Update `PROJECT.md` with the component folder layout (`TR.API.Events/`), namespace standard (`TR.[API|HW|KFK].[Description]`), and Docker Compose `include` architecture, verifying that changes match design decisions.

## 2. Solution and Projects Scaffolding

- [x] 2.1 Scaffold `TR.API.Events.sln` and the 5 projects (`Domain`, `Application`, `Infrastructure`, `WebApi`, `UnitTests`) targeting .NET 10 (`net10.0`) with inter-project references and `<Nullable>enable</Nullable>`, verifying all projects build cleanly.
- [x] 2.2 Install approved NuGet packages (FluentValidation, Carter, Serilog, Npgsql, EF Core, Dapper, xUnit, FluentAssertions, Moq, Bogus) and verify absence of MediatR and AutoMapper via dependency inspection.

## 3. Core Architecture & Diagnostics Implementation

- [x] 3.1 Implement native CQRS Lite interfaces (`ICommand`, `IQuery`, `ICommandHandler`, `IQueryHandler`) and Assembly Scan DI extension in `TR.API.Events.Application`, verifying type resolution.
- [x] 3.2 Implement `JsonResponse<TData, TError>`, `ExceptionHandlerMiddleware`, Serilog logging, and Carter setup in `TR.API.Events.WebApi`, verifying pipeline initialization.
- [x] 3.3 Implement persistence foundation (`IDbConnectionFactory`, `EventsDbContext`) in `TR.API.Events.Infrastructure` and Carter health check endpoints (`/health`, `/health/ready`) in `TR.API.Events.WebApi`, verifying endpoint routes.
- [x] 3.4 Implement unit tests in `TR.API.Events.UnitTests` verifying `JsonResponse` serialization, exception middleware handling, and health check contracts via `dotnet test`.

## 4. Containerization & Docker Compose Modularization

- [x] 4.1 Create multi-stage `TR.API.Events/Dockerfile` targeting .NET 10 SDK and ASP.NET 10 runtime with `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`, verifying image build succeeds.
- [x] 4.2 Create `TR.API.Events/docker-compose.yml` defining the `events-api` service connected to `ticketreserver-internal-net` with `depends_on: postgres: condition: service_healthy`.
- [x] 4.3 Update root `docker-compose.yml` using `include: [ ./TR.API.Events/docker-compose.yml ]` and verify configuration synthesis via `docker compose config`.

## 5. Quality Gate & Integration Verification

- [x] 5.1 Run `dotnet build` and `dotnet format --verify-no-changes` on `TR.API.Events.sln` to guarantee code formatting and compilation compliance.
- [x] 5.2 Start containers via `docker compose up -d` and verify HTTP GET to `http://localhost:5001/health` returns HTTP 200 with healthy status.
