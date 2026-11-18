# Proposal: Regras de Negócio e Endpoints da API de Reserva

## Why

A API de Reserva (`TR.API.Reservation`) teve seu scaffolding e fundação técnica estabelecidos, porém ainda não possui a implementação de seus casos de uso de negócio. É imperativo implementar os fluxos críticos de retenção de assentos/lotes (`/hold`), início de checkout (`/checkout`), consulta e cancelamento, assegurando alta performance, controle de concorrência com Redis e consistência transacional com Outbox pattern no PostgreSQL.

## What Changes

- Implementar caso de uso `HoldReservation` (`POST /reserva/hold`):
  - Bloqueio de inventário em memória no Redis (`SET NX EX 600` para assentos numerados e controle atômico de cota de pista).
  - Rollback atômico no Redis (Fast Fail) caso ocorra colisão de qualquer item da solicitação.
  - Persistência no PostgreSQL com status `AWAITING_PAYMENT` e expiração em 10 minutos.
  - Gravação transacional em `outbox_messages` do evento `reservation.hold_created` com delay de 600 segundos.
- Implementar caso de uso `CheckoutReservation` (`POST /reserva/checkout`):
  - Validação de existência, propriedade do cliente e prazo de validade do hold.
  - Expiração automática e liberação no Redis caso o prazo de 10 minutos tenha sido ultrapassado.
  - Transição de status para `PROCESSING_PAYMENT` com OCC (Optimistic Concurrency Control via `version`).
  - Gravação transacional em `outbox_messages` do evento imediato `reservation.checkout_req` contendo dados do pedido e metadados de pagamento.
- Implementar caso de uso `GetReservationById` (`GET /reserva/{id}`):
  - Consulta de leitura de alta performance com Dapper retornando status, itens, `remainingHoldSeconds` e `isExpired`.
- Implementar caso de uso `CancelReservation` (`DELETE /reserva/{id}`):
  - Liberação voluntária antecipada de assentos/cotas no Redis e transição para `CANCELLED` no PostgreSQL.
- Expor endpoints HTTP via Carter Minimal APIs encapsulados em `JsonResponse<TData, TError>`.
- Garantir 100% de cobertura com testes unitários em `UnitTests` para todos os novos Handlers, validadores e fluxos de domínio.

## Capabilities

### New Capabilities

*(Nenhuma nova capability criada; expande a capability existente)*

### Modified Capabilities

- `reservation-api`: Adiciona os requisitos funcionais e contratos de comportamento para criação de hold, processamento de checkout, consulta e cancelamento de reservas.

## Impact

- **Código impactado:**
  - `TR.API.Reservation.Domain`: Métodos ricos de negócio nas entidades `Reservation` e `ReservationItem`.
  - `TR.API.Reservation.Application`: Novas fatias verticais sob `Reservations/{HoldReservation, CheckoutReservation, GetReservationById, CancelReservation}`.
  - `TR.API.Reservation.Infrastructure`: Expansão do `RedisHoldService` para suporte a cotas de lote geral via scripts Lua atômicos e consultas Dapper em repositório de leitura.
  - `TR.API.Reservation.WebApi`: Módulo Carter `ReservationEndpoints` mapeando `/reserva/hold`, `/reserva/checkout`, `/reserva/{id}` e `DELETE /reserva/{id}`.
  - `TR.API.Reservation.UnitTests`: Suíte abrangente de testes para todos os casos de uso.
- **Sistemas externos:** Comunicação assíncrona orientada a eventos através da gravação na tabela `outbox_messages` para consumo posterior por `OutboxRelay` e workers do Kafka.
