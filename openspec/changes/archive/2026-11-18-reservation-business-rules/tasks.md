# Tasks

## 1. Expansão de Infraestrutura e Caching

- [x] 1.1 Expandir o `IRedisHoldService` e `RedisHoldService` com suporte a scripts Lua para cotas de lotes não-numerados e liberação idempotente em lote, verificando com testes unitários em `UnitTests`.
- [x] 1.2 Implementar o repositório de leitura `IReservationReadRepository` com Dapper e `IDbConnectionFactory` executando queries parametrizadas para `GetReservationById`, validando com testes de consulta.

## 2. Implementação das Fatias Verticais de Aplicação (CQRS Lite)

- [x] 2.1 Implementar a fatia `HoldReservation` (`Command`, `Handler`, `FluentValidation` `Validator`, `Response` e `MappingExtensions`) com garantia de rollback no Redis em caso de colisão/erro e persistência de outbox com delay de 600s, verificando com testes unitários.
- [x] 2.2 Implementar a fatia `CheckoutReservation` (`Command`, `Handler`, `Validator`, `Response`) validando expiração de hold, OCC via `version` e persistência de outbox imediato, verificando com testes unitários.
- [x] 2.3 Implementar as fatias `GetReservationById` e `CancelReservation` com liberação antecipada no Redis e cálculo dinâmico de tempo restante, verificando com testes unitários.

## 3. Endpoints WebApi (Carter) e Validação Integrada

- [x] 3.1 Implementar `ReservationEndpoints` herdando de `ICarterModule` mapeando `/reserva/hold` (201), `/reserva/checkout` (202), `GET /reserva/{id}` (200) e `DELETE /reserva/{id}` (200) envelopados em `JsonResponse` e com metadados OpenAPI.
- [x] 3.2 Executar suíte completa de testes unitários com `dotnet test` e validação estrita de estilo com `dotnet format --verify-no-changes`, garantindo zero warnings e zero falhas.
