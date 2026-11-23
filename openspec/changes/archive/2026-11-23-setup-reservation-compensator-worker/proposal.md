# Proposal

## Why

Once checkout begins, a reservation remains in `PROCESSING_PAYMENT` status inside `reservation_db`. When the downstream choreography finishes — either with successful ticket issuance (`ticketing.v1.issued`), payment gateway decline (`payment.v1.declined`), or automatic saga compensation refund (`payment.v1.refunded`) — the reservation record must transition to its final authoritative state (`CONFIRMED` or `CANCELLED`), and any held seats in Redis must be released immediately on cancellation.

## What Changes

- Scaffold `TR.HW.ResCompensator` solution with clean separation across 5 projects (`Domain`, `Application`, `Infrastructure`, `Worker`, `UnitTests`) following .NET 10 and CQRS Lite guidelines (zero MediatR and zero AutoMapper).
- Implement `ReservationState` aggregate root and `ReservedSeatItem` entity in `TR.HW.ResCompensator.Domain` enforcing terminal transition rules (`Confirm`, `Cancel`).
- Implement CQRS Lite `ReconcileReservationOutcomeCommand`, `ReconcileReservationOutcomeCommandHandler`, and `ReconcileReservationOutcomeCommandValidator` in `TR.HW.ResCompensator.Application`.
- Implement `ReservationCompensationRepository` using Dapper and Npgsql operating against `reservation_db` (`reservations`, `reservation_items`, and `inbox_messages`).
- Implement `RedisSeatLockReleaser` using `StackExchange.Redis` and an atomic Lua script (`if redis.call("get", KEYS[1]) == ARGV[1] then return redis.call("del", KEYS[1]) else return 0 end`) to release held seats immediately when a reservation is cancelled due to payment decline or refund.
- Implement `ReservationCompensationWorker` (`BackgroundService`) subscribing to `payment.v1.refunded`, `payment.v1.declined`, and `ticketing.v1.issued` under consumer group `cg-reservation-compensator` with inbox deduplication and manual offset acknowledgment.
- Configure multi-stage `Dockerfile` and `docker-compose.yml` for `TR.HW.ResCompensator`, registering it in the root `docker-compose.yml`.

## Capabilities

### New Capabilities
- `reservation-compensation-worker`: Consumes terminal saga events (`payment.v1.refunded`, `payment.v1.declined`, `ticketing.v1.issued`), transitions reservation records to `CONFIRMED` or `CANCELLED` in `reservation_db`, and atomically releases Redis seat holds upon cancellation.

### Modified Capabilities
<!-- None -->

## Impact

- **New Projects**: `TR.HW.ResCompensator` (`Domain`, `Application`, `Infrastructure`, `Worker`, `UnitTests`).
- **Database & Cache**: Connects to existing `reservation_db` (`reservations`, `reservation_items`, `inbox_messages`) and Redis seat hold keys (`hold:event:{eventId}:seat:{seatId}`).
- **Kafka**: Multi-topic consumer for `payment.v1.refunded`, `payment.v1.declined`, and `ticketing.v1.issued` (`cg-reservation-compensator`) with DLQ fallback.
- **Root Docker Compose**: Registration of `ticketreserver-reservation-compensator-worker` via `include`.
