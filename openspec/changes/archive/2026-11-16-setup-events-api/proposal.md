# Proposal: Setup TR.API.Events (.NET 10 & Modular Compose)

## Why

The Event Catalog service (`SVC_EVENTOS`) requires its foundational API application (`TR.API.Events`) to expose event catalog endpoints, execute domain operations, and interact with the provisioned PostgreSQL database (`catalog_events_db`). Setting up the Clean Architecture solution in .NET 10 with a dedicated multi-stage Dockerfile and a modular, fragmented Docker Compose setup ensures deterministic local orchestration and compliance with project architectural gates.

## What Changes

- **Update Project Guidelines (`PROJECT.md`)**:
  - Enforce component directory convention: `[ComponentNamespace]/` (e.g., `TR.API.Events/`).
  - Enforce strict namespace pattern: `TR.[API|HW|KFK].[Description]`.
  - Document Docker Compose fragmentation via native `include` directive.
- **Scaffold Solution `TR.API.Events` in .NET 10**:
  - Create solution file `TR.API.Events/TR.API.Events.sln`.
  - Create 5 standard projects:
    - `source/TR.API.Events.Domain`: Domain entities, value objects, domain exceptions (zero external package dependencies).
    - `source/TR.API.Events.Application`: CQRS Lite native contracts (`ICommand`, `IQuery`), pipeline validators (FluentValidation), DI scan.
    - `source/TR.API.Events.Infrastructure`: Persistence hybrid foundation (EF Core + Dapper Npgsql connection factory).
    - `source/TR.API.Events.WebApi`: Carter Minimal APIs, `JsonResponse<TData, TError>` envelope, global `ExceptionHandlerMiddleware`, Serilog, Swagger, `/health` endpoint.
    - `tests/TR.API.Events.UnitTests`: Unit testing suite with xUnit, FluentAssertions, Moq, and Bogus.
- **Containerization (.NET 10 Dockerfile)**:
  - Create dedicated multi-stage `TR.API.Events/Dockerfile` using .NET 10 SDK and ASP.NET 10 runtime images.
  - Configure `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` for environment compatibility.
- **Modular Docker Compose Orchestration**:
  - Create `TR.API.Events/docker-compose.yml` defining the `events-api` service attached to `ticketreserver-internal-net` and waiting on `postgres` healthy condition.
  - Update root `docker-compose.yml` to include `./TR.API.Events/docker-compose.yml` using Docker Compose native `include:`.

## Capabilities

### New Capabilities
- `catalog-events-api`: Containerized HTTP API application foundation for Event Catalog (`TR.API.Events`), featuring Carter modules, standard JSON response envelopment, structured health checks, and database connectivity.

### Modified Capabilities
<!-- None -->

## Impact

- **Code & Solutions**: Introduces directory `TR.API.Events/` containing the new .NET 10 solution and projects.
- **Documentation**: Updates `PROJECT.md` sections 1, 3, and 4 with the new namespace standards and compose architecture.
- **Infrastructure**: Updates root `docker-compose.yml` with `include` statement. Port `5001` exposed to host mapped to `8080` in container.
- **Dependencies**: Uses .NET 10 SDK/runtimes, Carter, FluentValidation, Npgsql, EF Core, Dapper. No MediatR or AutoMapper.
