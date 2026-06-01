# Contratos de Eventos - Apache Kafka (02-contracts.md)

Todos os eventos trafegados no ecossistema devem conter os seguintes metadados obrigatórios nos Headers da mensagem Kafka:
*   `traceparent` (String, Padrão W3C contendo o TraceId para correlação do OpenTelemetry).

## Tópico: `reservation.created`
*   **Produtor:** Outbox-Relay (Contexto de Reserva)
*   **Consumidor:** Worker-Pagamento
*   **Payload Schema (JSON):**
```json
{
  "reservationId": "GUID",
  "userId": "GUID",
  "eventId": "GUID",
  "seatNumber": "String",
  "ticketPrice": "Decimal",
  "createdAt": "DateTime-ISO"
}
```

## Tópico: payment.approved
*   **Produtor**: Worker-Pagamento
*   **Consumidor**: Worker-Ticketing
*   **Payload Schema (JSON):**
```json
{
  "paymentId": "GUID",
  "reservationId": "GUID",
  "userId": "GUID",
  "amountPaid": "Decimal",
  "transactionReference": "String",
  "approvedAt": "DateTime-ISO"
}
```

## Tópico: payment.refused (Evento de Compensação SAGA)
*   **Produtor**: Worker-Pagamento
*   **Consumidor**: API-Reserva (Handler de Compensação)
*   **Payload Schema (JSON):**
```json
{
  "reservationId": "GUID",
  "userId": "GUID",
  "reason": "String",
  "refusedAt": "DateTime-ISO"
}
```