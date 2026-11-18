# Design: Setup da Arquitetura da API de Reserva (TR.API.Reservation)

## Context

A infraestrutura conteinerizada (PostgreSQL com `reservation_db`, Redis e Kafka KRaft) já está operacional. O microsserviço de Reservas (`SVC_RESERVA`) precisa ter sua estrutura .NET 10 inicializada seguindo os padrões arquiteturais de `PROJECT.md` e as regras de ferro de `dotnet-clean-cqrs-scaffold` e `dotnet-carter-cqrs-guard`.

## Goals / Non-Goals

**Goals:**
- Criar a solução `TR.API.Reservation.sln` com isolamento estrito entre 5 projetos (`Domain`, `Application`, `Infrastructure`, `WebApi`, `UnitTests`).
- Implementar CQRS Lite nativo com interfaces C# enxutas (`ICommand`, `IQuery`, `ICommandHandler`, `IQueryHandler`) e registro determinístico via DI (zero `MediatR` / zero `AutoMapper`).
- Estabelecer as entidades ricas do agregado de reserva (`Reservation`, `ReservationItem`, `OutboxMessage`, `InboxMessage`) com invariantes, construtores privados e coleções protegidas como `IReadOnlyCollection<T>`.
- Configurar persistência com `ReservationDbContext` (EF Core) mapeando os tipos ENUM nativos do PostgreSQL (`reservation_status_enum`, `outbox_status_enum`) via `NpgsqlDataSourceBuilder`.
- Estruturar a WebApi com Carter (`CarterModule`), envelope padronizado `JsonResponse<TData, TError>`, middleware global de tratamento de exceções e endpoint `/healthz`.
- Criar Dockerfile multi-stage (.NET 10 SDK & runtime) e compose fragmentado integrado ao `docker-compose.yml` raiz.
- Garantir 100% de aprovação na compilação, formatação e suíte inicial de testes unitários.

**Non-Goals:**
- Implementação detalhada dos handlers de negócio de `/hold` (com lock distribuído no Redis) e `/checkout` (será realizado na change seguinte).
- Implementação dos workers em background de consumo do Kafka (`TR.HW.Payment`, `TR.HW.ResTimeout`, etc.).

## Decisions

### 1. CQRS Lite Nativo vs MediatR
* **Decisão:** Não utilizar pacotes de Mediação (`MediatR`). Adotar interfaces internas sob `Application/Common/Messaging/` e registro automático via Assembly Scan.
* **Racional:** Elimina sobrecarga reflexiva, reduz dependências externas e acelera o tempo de inicialização em contêineres e testes.

### 2. Mapeamento de Tipos e Compatibilidade com PostgreSQL
* **Decisão:** Configurar `NpgsqlDataSourceBuilder.MapEnum<ReservationStatus>()` e `MapEnum<OutboxStatus>()` utilizando `NpgsqlNullNameTranslator` na camada de Infraestrutura.
* **Racional:** Garante que o EF Core persista e leia os enums nativos do Postgres criados em `init-reservation-db.sql` sem erros de conversão de inteiros para strings.

### 3. Pinning de Versões de Pacotes para .NET 10
* **Decisão:** Fixar versões de pacotes compatíveis com .NET 10:
  * `Carter`: `10.0.0`
  * `Microsoft.AspNetCore.OpenApi`: `10.0.11`
  * `Npgsql.EntityFrameworkCore.PostgreSQL`: `10.0.0`
  * `StackExchange.Redis`: `2.8.24`
* **Racional:** Evita problemas de incompatibilidade de pacotes pré-release ou que visam net11.0.

### 4. Envelopamento e Tratamento de Exceções
* **Decisão:** Implementar a classe `JsonResponse<TData, TError>` e o `ExceptionHandlerMiddleware` na WebApi para capturar `DomainException`, `ValidationException`, `NotFoundException` e falhas 500, produzindo payloads uniformes para os clientes.
* **Racional:** Garante previsibilidade e contrato consistente em todas as APIs do ecossistema.

## Risks / Trade-offs

- **[Risco: Problemas com dependência de ICU no Linux durante build/runtime]**
  * *Mitigação:* Declarar `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` no Dockerfile e nas variáveis de ambiente do Compose, replicando a solução bem-sucedida de `TR.API.Events`.
- **[Risco: Conflito de rede ou portas no Docker Compose]**
  * *Mitigação:* Usar porta host `5003` (interna 8080) e conectar exclusivamente na rede externa `ticketreserver-internal-net` já declarada no Compose raiz.
