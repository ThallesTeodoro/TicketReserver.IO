# Design

## Context

In the event-driven ticketing choreography, `TR.HW.Ticketing` acts as the physical concurrency barrier (`UNIQUE(event_id, seat_id)`). When a collision occurs (`23505`), `TR.HW.Ticketing` publishes `ticketing.v1.failed`. The payment domain already holds the captured funds in `payment_db` and has pre-provisioned tables (`payments`, `refunds`, `payment_transactions`, `outbox_messages`, `inbox_messages`). `TR.HW.Refund` is the dedicated worker responsible for executing the automatic financial compensation (*Auto-Refund*). See `proposal.md` and `specs/payment-refund-worker/spec.md`.

## Goals / Non-Goals

**Goals:**
- Consume `ticketing.v1.failed` reliably under consumer group `cg-payment-refund` with inbox deduplication.
- Retrieve the `CAPTURED` payment for the affected `reservation_id` and execute a resilient refund against the payment gateway using Polly (3 retries with exponential backoff and random jitter).
- Atomically update `payments` (`status = 'REFUNDED'`, `version = version + 1`), insert `refunds` (`status = 'COMPLETED'`), append `payment_transactions` (`transaction_type = 'REFUND'`), and write `payment.v1.refunded` to `outbox_messages`.
- Publish `payment.v1.refunded` to Apache Kafka with W3C `traceparent` header propagation so downstream reservation cancellation and notification workers can complete the saga.
- Maintain strict Clean Architecture (.NET 10) across 5 projects with native CQRS Lite (zero MediatR / zero AutoMapper).

**Non-Goals:**
- Partial refunds or manual back-office dispute UI (this worker specifically handles full automatic compensation triggered by saga failures).
- Direct mutation of `reservation_db` (handled by the reservation compensation consumer listening to `payment.v1.refunded`).

## Decisions

### Decision 1: Dedicated Multi-Project Solution (`TR.HW.Refund`) Sharing `payment_db`
- **Choice**: Create `TR.HW.Refund` with 5 projects (`Domain`, `Application`, `Infrastructure`, `Worker`, `UnitTests`) connecting to `payment_db`.
- **Rationale**: In `arch-decisions.md` and `payment-db.md` (Section 5.2), `TR.HW.Refund` is a dedicated worker within the `SVC_PAGAMENTO` bounded context. Separating the worker runtime allows independent scaling and fault isolation between high-volume checkout processing (`TR.HW.Payment`) and compensation refund processing (`TR.HW.Refund`).

### Decision 2: Double Idempotency Guard (Inbox + Payment Status Check)
- **Choice**:
  1. Check `inbox_messages` for `(message_id, 'cg-payment-refund')`.
  2. Query `payments` by `reservation_id`:
     - If `status == 'REFUNDED'`, log idempotent completion, record in `inbox_messages`, and commit offset without calling the gateway.
     - If `status != 'CAPTURED'` (or not found), log warning, record in `inbox_messages`, and commit offset.
- **Rationale**: Prevents double-refunding a customer even if two different failure events for the same reservation are emitted.

### Decision 3: Polly Resilience Pipeline on Gateway Refund Calls
- **Choice**: Wrap external gateway refund execution in a Polly `AsyncRetryPolicy` handling `HttpRequestException`, `TimeoutException`, and transient `GATEWAY_TIMEOUT` outcomes with 3 retries, exponential backoff ($2^{attempt} \times 100\text{ms}$), and random jitter.
- **Rationale**: Payment gateways can experience brief network blips; transient retries prevent stranding a customer's refund.

## Risks / Trade-offs

- **[Risk: Poison pill / malformed JSON in `ticketing.v1.failed`]** → Mitigation: Catch deserialization errors, route the raw payload to `ticketing.v1.failed.DLQ` with diagnostic headers, and commit the offset to prevent consumer lag blockage.
- **[Risk: Concurrent modification of `payments` row]** → Mitigation: Increment `version = version + 1` and filter `WHERE id = @PaymentId AND status = 'CAPTURED'::payment_status_enum` inside the PostgreSQL transaction.
