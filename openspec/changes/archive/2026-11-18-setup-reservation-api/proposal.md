# Proposal: Setup da Arquitetura da API de Reserva (TR.API.Reservation) em .NET 10

## Why

Com a infraestrutura de mensageria Apache Kafka (KRaft), o cluster Redis e a base relacional `reservation_db` (PostgreSQL) devidamente provisionados, é necessário inicializar a aplicação central de reservas (`SVC_RESERVA` / `TR.API.Reservation`). O estabelecimento inicial desta solução em Clean Architecture com CQRS Lite nativo e Minimal APIs via Carter é o alicerce para suportar os fluxos críticos de garantia de assento (`/hold`), envio para pagamento (`/checkout`) e publicação confiável de eventos via Transactional Outbox.

## What Changes

- Criação da solução `TR.API.Reservation.sln` em .NET 10 estruturada em 5 projetos seguindo rigorosamente a Clean Architecture:
  - `source/TR.API.Reservation.Domain/`: Agregado `Reservation`, entidade `ReservationItem`, entidades de mensageria `OutboxMessage` e `InboxMessage`, enums nativos (`ReservationStatus`, `OutboxStatus`), exceções de domínio e contratos de repositório/serviço (sem dependências de frameworks).
  - `source/TR.API.Reservation.Application/`: Contratos nativos de CQRS Lite (`ICommand`, `IQuery`, `ICommandHandler`, `IQueryHandler`), injeção de dependência via Assembly Scan e validadores FluentValidation (sem `MediatR` ou `AutoMapper`).
  - `source/TR.API.Reservation.Infrastructure/`: `ReservationDbContext` (EF Core) com mapeamento dos tipos ENUM do PostgreSQL via `NpgsqlDataSourceBuilder`, integração Dapper para queries parametrizadas, e abstração do serviço de lock distribuído Redis (`RedisHoldService`).
  - `source/TR.API.Reservation.WebApi/`: Ponto de entrada HTTP com Carter (`CarterModule`), envelope padronizado `JsonResponse<TData, TError>`, middleware global de tratamento de exceções (`ExceptionHandlerMiddleware`) e endpoint de health check (`/healthz`).
  - `tests/TR.API.Reservation.UnitTests/`: Suíte de testes unitários com `xUnit`, `Moq` e `FluentAssertions`.
- Criação de Dockerfile multi-stage otimizado (.NET 10 SDK e ASP.NET runtime) e compose modular em `TR.API.Reservation/docker-compose.yml` expondo a porta `5003` conectada à rede `ticketreserver-internal-net`.
- Inclusão do compose do serviço no `docker-compose.yml` raiz da aplicação via diretiva `include:`.

## Capabilities

### New Capabilities
- `reservation-api`: Ponto de entrada HTTP e camadas arquiteturais do microsserviço de Reservas em .NET 10 (Clean Architecture, CQRS Lite nativo, Carter Minimal APIs, envelope JsonResponse e integração com PostgreSQL e Redis).

### Modified Capabilities
<!-- Nenhuma capability existente tem seus requisitos alterados nesta mudança de setup arquitetural. -->

## Impact

- **Nova Aplicação .NET:** Solução `TR.API.Reservation` adicionada na raiz do repositório.
- **Topologia Docker:** Container `ticketreserver-reservation-api` mapeando a porta host `5003:8080` conectado na rede interna com acesso ao `postgres`, `redis` e `kafka`.
