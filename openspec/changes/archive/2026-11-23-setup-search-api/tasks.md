# Tasks

## 1. Solution Scaffolding & Project Setup

- [x] 1.1 Scaffold `TR.API.Search` solution (`.sln` and `.slnx`) with 5 projects (`TR.API.Search.Domain`, `TR.API.Search.Application`, `TR.API.Search.Infrastructure`, `TR.API.Search.WebApi`, and `TR.API.Search.UnitTests`) targeting .NET 10, configure inter-project references and NuGet packages (`Elastic.Clients.Elasticsearch`, `Carter`, `Polly`, `FluentValidation`, `Serilog`, `xUnit`, `Moq`, `FluentAssertions`), and verify `dotnet build` succeeds.

## 2. Domain Layer Implementation

- [x] 2.1 Implement search domain entities, filter value objects (`SearchFilter`, `GeoLocation`, `PriceRangeFilter`, `DateRangeFilter`), pagination parameters, search sort options, and domain exceptions in `TR.API.Search.Domain`, and verify through compilation.
- [x] 2.2 Implement domain unit tests in `TR.API.Search.UnitTests/Domain` verifying filter invariants, geo-distance calculations, and pagination constraints, and confirm `dotnet test` passes.

## 3. Application Layer (CQRS Lite & Queries)

- [x] 3.1 Implement CQRS Lite query interfaces (`IQuery`, `IQueryHandler`), query records (`SearchEventsQuery`, `AutocompleteEventsQuery`, `GetEventByIdQuery`), result DTOs (`EventSearchResultDto`, `EventSearchItemDto`, `AutocompleteSuggestionDto`), and static mapping extension methods (`EventSearchDtoMappingExtensions`) in `TR.API.Search.Application`.
- [x] 3.2 Implement FluentValidation query validators (`SearchEventsQueryValidator`, `AutocompleteEventsQueryValidator`, `GetEventByIdQueryValidator`) and define search repository port `IEventSearchEngine` in `TR.API.Search.Application`.
- [x] 3.3 Implement query handlers (`SearchEventsQueryHandler`, `AutocompleteEventsQueryHandler`, `GetEventByIdQueryHandler`) and DI registration in `TR.API.Search.Application`.
- [x] 3.4 Implement application unit tests in `TR.API.Search.UnitTests/Application` verifying query handling, validator assertions, and response mapping, and confirm `dotnet test` passes.

## 4. Infrastructure Layer (ElasticSearch Query Engine & Resilience)

- [x] 4.1 Implement ElasticSearch configuration options (`ElasticsearchOptions`) with fail-fast validation in `TR.API.Search.Infrastructure`.
- [x] 4.2 Implement `ElasticsearchSearchEngine` in `TR.API.Search.Infrastructure` implementing `IEventSearchEngine` via `Elastic.Clients.Elasticsearch` (v8), building multi-match full-text queries with field boosting (`title^3`, `category^2`, `tags^1.5`, `description`), boolean filter context for categories, dates, prices, and geo-distance, edge n-gram autocomplete queries, and facet aggregations wrapped in Polly retry policies.
- [x] 4.3 Implement cluster health check client in `TR.API.Search.Infrastructure` verifying connectivity to ElasticSearch and configure DI registration.
- [x] 4.4 Implement infrastructure unit tests in `TR.API.Search.UnitTests/Infrastructure` verifying query construction, error handling, and Polly policies, and confirm `dotnet test` passes.

## 5. WebApi Layer (Carter Modules & Minimal APIs)

- [x] 5.1 Implement standardized HTTP response envelope `JsonResponse<TData, TError>`, error handling middleware, and Carter modules (`SearchEventsModule`, `HealthModule`) exposing `GET /api/v1/search/events`, `GET /api/v1/search/events/autocomplete`, `GET /api/v1/search/events/{id}`, and `GET /api/v1/search/health`.
- [x] 5.2 Configure `Program.cs`, Serilog logging, `appsettings.json`, and `appsettings.Development.json` with fail-fast validation in `TR.API.Search.WebApi`.
- [x] 5.3 Implement WebApi endpoint tests in `TR.API.Search.UnitTests/WebApi` verifying route mapping, query parameter binding, status code contracts, and error responses, and confirm `dotnet test` passes.

## 6. Containerization & Stack Verification

- [x] 6.1 Create multi-stage `Dockerfile` and `docker-compose.yml` for `TR.API.Search` and register `./TR.API.Search/docker-compose.yml` in the root `docker-compose.yml`.
- [x] 6.2 Execute `dotnet test` across all solution test suites in the repository, verify that all tests pass, and validate OpenSpec change artifacts with `openspec validate setup-search-api`.
