# Design: Implementação das Regras de Negócio da API de Reserva

## Context

A fundação arquitetural da API de Reserva (`TR.API.Reservation`) foi consolidada em .NET 10 com persistência relacional (`reservation_db`), suporte a Redis e contratos de CQRS Lite nativo. O presente design detalha como os casos de uso de negócio (`HoldReservation`, `CheckoutReservation`, `GetReservationById` e `CancelReservation`) serão estruturados e orquestrados de forma transacional e resiliente.

## Goals / Non-Goals

**Goals:**
- Implementar o caso de uso `HoldReservation` com garantia de retenção em memória no Redis (assentos numerados e cotas gerais) com política All-or-Nothing e gravação no PostgreSQL com Transactional Outbox (delay de 10 min).
- Implementar o caso de uso `CheckoutReservation` validando o tempo limite de hold, aplicando controle de concorrência otimista (OCC via `version`) e gerando evento imediato de outbox para a saga de pagamento.
- Implementar o caso de uso `GetReservationById` com Dapper para leitura ultra rápida com campos calculados `remainingHoldSeconds` e `isExpired`.
- Implementar o caso de uso `CancelReservation` com liberação voluntária de locks no Redis e transição para `CANCELLED`.
- Expor todos os fluxos via Minimal APIs com Carter envelopadas em `JsonResponse<TData, TError>`.

**Non-Goals:**
- Implementação dos workers em background de consumo do Kafka (`TR.HW.Payment`, `TR.HW.ResTimeout`, `Worker Compensação`), que pertencem a serviços dedicados.
- Integração de rede com gateways de pagamento externos reais (o envio da transação é feito de forma assíncrona via Outbox/Kafka para o worker de pagamento).

## Decisions

### 1. Gestão Híbrida de Inventário no Redis
* **Decisão:** O `RedisHoldService` suportará dois mecanismos atômicos:
  1. *Assentos Numerados (`SeatId != null`):* `SET hold:event:{eventId}:seat:{seatId} "{reservationId}" NX EX 600`.
  2. *Lotes Gerais / Pista (`SeatId == null`):* Script Lua atômico avaliando e decrementando a chave `hold:event:{eventId}:tier:{tierId}:quota`.
  3. *Rollback Seguro:* Em caso de colisão de qualquer item em um pedido múltiplo, todas as chaves e cotas daquela operação são revertidas imediatamente.
* **Alternativa rejeitada:** Bloquear linhas no PostgreSQL via `SELECT FOR UPDATE` diretamente no banco relacional (rejeitada por gerar alta contenção, degradação de throughput e risco de deadlocks em picos de abertura de vendas).

### 2. Consistência Transacional com Transactional Outbox
* **Decisão:** Na mesma transação relacional (`IDbContextTransaction`) da reserva:
  * No `/hold`: grava o evento `reservation.hold_created` com `delay_seconds = 600` e `deliver_after = NOW() + 600s`.
  * No `/checkout`: grava o evento `reservation.checkout_req` com `delay_seconds = 0` e `deliver_after = NOW()`.
* **Alternativa rejeitada:** Publicar diretamente no Kafka a partir do endpoint HTTP da API (rejeitada pelo risco de dual-write inconsistency: commit no banco e falha no broker, ou vice-versa).

### 3. Vertical Slices e CQRS Lite Nativo
* **Decisão:** Cada caso de uso é isolado sob `Application/Reservations/{SliceName}/`:
  * `HoldReservation/`: `HoldReservationCommand`, `HoldReservationCommandHandler`, `HoldReservationCommandValidator`, `HoldReservationResponse`.
  * `CheckoutReservation/`: `CheckoutReservationCommand`, `CheckoutReservationCommandHandler`, `CheckoutReservationCommandValidator`, `CheckoutReservationResponse`.
  * `GetReservationById/`: `GetReservationByIdQuery`, `GetReservationByIdQueryHandler`, `ReservationDetailsResponse`.
  * `CancelReservation/`: `CancelReservationCommand`, `CancelReservationCommandHandler`, `CancelReservationResponse`.
* **Racional:** Alta coesão, facilidade de manutenção e zero dependência de mediadores pesados (`MediatR`).

### 4. Consultas Rápidas com Dapper
* **Decisão:** A query `GetReservationById` utiliza `IDbConnectionFactory` executando consulta SQL parametrizada via Dapper, juntando `reservations` e `reservation_items` e calculando dinamicamente `remainingHoldSeconds = Math.Max(0, (int)(hold_expires_at - UTC_NOW).TotalSeconds)` e `isExpired`.
* **Racional:** Desempenho máximo para consultas frequentes de tela de checkout sem tracking do EF Core.

## Risks / Trade-offs

- **[Risco: Falha de conexão com PostgreSQL após locks concedidos no Redis]**
  * *Mitigação:* O handler de `/hold` envolve a persistência em bloco `try/catch`; qualquer falha no banco dispara imediatamente a liberação dos assentos e cotas no Redis antes de propagar a exceção.
- **[Risco: Cliques simultâneos de checkout do mesmo usuário]**
  * *Mitigação:* O EF Core utiliza a propriedade `Version` configurada como `IsConcurrencyToken` (`DbUpdateConcurrencyException`), garantindo que apenas uma transição ocorra com sucesso.
- **[Risco: Sobrecarga no Redis]**
  * *Mitigação:* TTL rigoroso de 10 minutos em todas as chaves criadas, permitindo expiração natural em memória mesmo em caso de panes catastróficas.
