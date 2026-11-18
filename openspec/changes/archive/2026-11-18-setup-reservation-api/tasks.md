# Tasks

## 1. Scaffolding da Solução e Projetos .NET 10

- [x] 1.1 Criar a solução `TR.API.Reservation.sln` e os 5 projetos (`Domain`, `Application`, `Infrastructure`, `WebApi`, `UnitTests`) em .NET 10, configurando as referências entre camadas conforme Clean Architecture e verificando com `dotnet sln list`.
- [x] 1.2 Instalar pacotes NuGet permitidos fixados para .NET 10 (FluentValidation, Carter, Npgsql EF Core, Dapper, StackExchange.Redis, OpenApi, Moq, FluentAssertions, Bogus) e validar compilação inicial com `dotnet build`.

## 2. Implementação das Camadas de Domínio, Aplicação e Infraestrutura

- [x] 2.1 Implementar em `Domain` as entidades `Reservation`, `ReservationItem`, `OutboxMessage`, `InboxMessage`, enums e exceções, garantindo construtores protegidos, coleções `IReadOnlyCollection<T>` e zero dependências de terceiros.
- [x] 2.2 Implementar em `Application` os contratos de CQRS Lite (`ICommand`, `IQuery`, `ICommandHandler`, `IQueryHandler`), classes base e registro determinístico por Assembly Scan em `DependencyInjection.cs`.
- [x] 2.3 Implementar em `Infrastructure` o `ReservationDbContext` (EF Core) com mapeamento dos tipos ENUM via `NpgsqlDataSourceBuilder`, `IDbConnectionFactory` para Dapper e contratos de `IRedisHoldService`.

## 3. WebApi, Envelopamento, Docker e Testes Unitários

- [x] 3.1 Implementar na camada `WebApi` o envelope `JsonResponse<TData, TError>`, o `ExceptionHandlerMiddleware`, Carter Module de inicialização e endpoint `/healthz`.
- [x] 3.2 Criar suíte de testes unitários em `UnitTests` cobrindo invariantes de domínio, envelope `JsonResponse`, middleware de exceções e contratos CQRS, verificando execução com `dotnet test`.
- [x] 3.3 Criar Dockerfile multi-stage .NET 10, `.dockerignore` e compose fragmentado `TR.API.Reservation/docker-compose.yml` (porta 5003), integrando-o via `include:` no `docker-compose.yml` raiz e validando com `docker compose config`.
