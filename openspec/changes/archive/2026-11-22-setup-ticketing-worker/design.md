# Design

## Context

Following payment authorization in `TR.HW.Payment`, the system transitions to the ticketing issuance phase. In high-concurrency ticket sales, multiple transactions could potentially race to allocate the same physical seat if memory locks expire or are bypassed. The database layer must serve as the authoritative physical concurrency barrier. See `proposal.md` and `specs/ticket-issuance-worker/spec.md`.

## Goals / Non-Goals

**Goals:**
- Provide definitive, atomic seat allocation in PostgreSQL using `seat_allocations` with a `UNIQUE(event_id, seat_id)` constraint.
- Detect seat collisions via PostgreSQL exception `23505 (unique_violation)`, rolling back issuance and atomically publishing `ticketing.v1.failed` to trigger the compensation saga (`TR.HW.Refund`).
- Generate anti-fraud cryptographic QR code tokens signed with HMAC-SHA256.
- Maintain transactional consistency with Transactional Outbox and Inbox deduplication (`cg-ticketing-worker`).
- Implement strict Clean Architecture (.NET 10) with native CQRS Lite (zero MediatR / zero AutoMapper).

**Non-Goals:**
- Real-time gate turnstile HTTP check-in API (the schema supports check-in audit, but gate operations belong to operational APIs).
- Direct refund execution (handled asynchronously by `TR.HW.Refund` consuming `ticketing.v1.failed`).

## Decisions

### Decision 1: Multi-Project Solution Architecture
- **Choice**: Scaffold `TR.HW.Ticketing` with 5 separate projects: `TR.HW.Ticketing.Domain`, `TR.HW.Ticketing.Application`, `TR.HW.Ticketing.Infrastructure`, `TR.HW.Ticketing.Worker`, and `TR.HW.Ticketing.UnitTests`.
- **Rationale**: Complies strictly with project guidelines in `PROJECT.md` and preserves clean separation of concerns.

### Decision 2: Dapper & Npgsql over Heavy ORMs
- **Choice**: Use Dapper and raw Npgsql connection/transaction management in `TicketingRepository`.
- **Rationale**: Dapper provides sub-millisecond execution and explicit, granular transaction control required to catch PostgreSQL error code `23505` and branch into a separate compensation transaction.

### Decision 3: Concurrency Collision Handling (The Saga Trigger)
- **Choice**:
  1. Begin transaction.
  2. Attempt inserting into `seat_allocations` and `tickets`.
  3. If successful, insert `ticketing.v1.issued` into `outbox_messages` and record in `inbox_messages`, then commit.
  4. If `PostgresException` with `SqlState == "23505"` occurs, roll back the transaction.
  5. Open a separate transaction to insert `ticketing.v1.failed` into `outbox_messages` and record in `inbox_messages`, then commit.
  6. Commit the Kafka offset manually in both cases.
- **Alternative considered**: Pre-checking seat existence via `SELECT`. Rejected because concurrent transactions can pass the check simultaneously (Time-of-check to time-of-use bug). Physical unique constraint is the only race-free mechanism.

### Decision 4: Cryptographic QR Code Hash
- **Choice**: `HMAC-SHA256(TicketId + ":" + EventId + ":" + CustomerId + ":" + IssuedAt, SecretKey)` encoded as Base64 URL-safe string.
- **Rationale**: Prevents ticket forgery and enables gate readers to verify ticket authenticity mathematically offline or online.

## Risks / Trade-offs

- **[Risk: Multiple seats in single checkout]** → Mitigation: Process all items within the same PostgreSQL transaction. If any seat in the reservation is occupied, the entire batch rolls back and triggers compensation for the entire reservation.
- **[Risk: Poison pill / malformed message]** → Mitigation: Catch JSON parsing exceptions, route original message and error details to DLQ (`payment.v1.approved.DLQ`), and commit offset to avoid partition starvation.
