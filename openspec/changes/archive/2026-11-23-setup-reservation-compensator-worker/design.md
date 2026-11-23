# Design

## Context

In `SVC_RESERVA`, `TR.API.Reservation` transitions a reservation to `PROCESSING_PAYMENT` during `/checkout` and emits `reservation.v1.checkout_req`. Downstream services later publish terminal outcomes:
1. `ticketing.v1.issued` (happy path completed)
2. `payment.v1.declined` (payment rejected by gateway)
3. `payment.v1.refunded` (auto-refund executed by `TR.HW.Refund` after seat collision)

`TR.HW.ResCompensator` is the dedicated worker in `SVC_RESERVA` that listens to these terminal events, reconciles the authoritative reservation state in `reservation_db`, and immediately frees held seats in Redis on cancellation. See `proposal.md` and `specs/reservation-compensation-worker/spec.md`.

## Goals / Non-Goals

**Goals:**
- Subscribe to `payment.v1.refunded`, `payment.v1.declined`, and `ticketing.v1.issued` under consumer group `cg-reservation-compensator`.
- Enforce idempotent execution via `inbox_messages` in `reservation_db` and state machine guards on `ReservationState`.
- Atomically transition `reservations` to `CONFIRMED` (on `ticketing.v1.issued`) or `CANCELLED` (on `payment.v1.refunded` / `payment.v1.declined`).
- Immediately release Redis seat locks (`hold:event:{eventId}:seat:{seatId}`) via an ownership-checking Lua script whenever a reservation is cancelled.
- Maintain strict Clean Architecture (.NET 10) across 5 projects with native CQRS Lite (zero MediatR / zero AutoMapper).

**Non-Goals:**
- Timeout expiration after 10 minutes (already owned by `TR.HW.ResTimeout` consuming `reservation.v1.hold_created.delayed`).
- Customer email/push notification delivery (owned by `TR.HW.Notification`).

## Decisions

### Decision 1: Unified Multi-Topic Consumer (`TR.HW.ResCompensator`)
- **Choice**: Configure `KafkaConsumer` to subscribe to the list of terminal outcome topics (`payment.v1.refunded`, `payment.v1.declined`, `ticketing.v1.issued`) and map the topic/eventType into a `ReservationOutcomeType` enum (`Confirmed`, `CancelledByRefund`, `CancelledByDecline`).
- **Rationale**: All three events mutate the terminal status of the `reservations` aggregate in `reservation_db`. Consolidating them into a single cohesive worker avoids duplicating database/Redis infrastructure while keeping `TR.HW.ResTimeout` isolated for delayed TTL messages.

### Decision 2: Safe Ownership-Checked Redis Lock Release via Lua Script
- **Choice**: Reuse the atomic Lua script pattern:
  ```lua
  if redis.call("get", KEYS[1]) == ARGV[1] then
      return redis.call("del", KEYS[1])
  else
      return 0
  end
  ```
- **Rationale**: Prevents accidental deletion of a seat lock if the original TTL expired and another customer already acquired a new hold on the same seat.

### Decision 3: Idempotent Domain State Machine
- **Choice**:
  - `Confirm()` succeeds from `ProcessingPayment` or `AwaitingPayment`; if already `Confirmed`, returns idempotently.
  - `Cancel(reason)` succeeds from `ProcessingPayment` or `AwaitingPayment` (or even `Confirmed` if a late refund arrives); if already `Cancelled` or `Expired`, returns idempotently.

## Risks / Trade-offs

- **[Risk: Redis transient unavailability during seat release]** → Mitigation: The PostgreSQL transaction (`status = 'CANCELLED'`) is the source of truth and commits first; Redis keys have a natural 600-second TTL fallback, and errors during early release are logged without rolling back the database cancellation.
- **[Risk: Poison pill message on any subscribed topic]** → Mitigation: Route malformed payloads to the configured DLQ topic with diagnostic headers and commit the offset.
