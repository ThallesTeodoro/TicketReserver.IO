# Esquema de Observabilidade OpenTelemetry (04-observability.md)

## 1. Padrão de Ingestão de Logs (Serilog)
Todos os logs gerados em C# devem obrigatoriamente incluir os seguintes atributos estruturados:

*   `Environment`: `development` | `production`
*   `Service`: Nome do microsserviço (ex: `tr-api-reservation`)
*   `TraceId`: Capturado nativamente da thread corrente do OpenTelemetry.
*   `SpanId`: Capturado nativamente da thread corrente do OpenTelemetry.

### Exemplo de Log Proibido (Interpolação):
`_logger.LogInformation($"Processando pagamento da reserva {id}");`

### Exemplo de Log Obrigatório (Estruturado):
`_logger.LogInformation("Processando pagamento da reserva {ReservationId}", id);`

## 2. Rastreamento de Spans (Grafana Tempo)
A árvore de spans do OpenTelemetry deve seguir estritamente o grafo abaixo para monitoramento de latência:

```text
[HTTP POST /reserva] -> Inicia Trace
   └── [Redis SET NX] (Mapeia latência do cache de entrada)
   └── [SQL INSERT Reservation + Outbox] (Mapeia latência do Postgres)
       └── [TR.HW.OutboxRelay Polling] (Cria Span Link para o Kafka Producer)
           └── [Kafka Topic: reservation.created]
               └── [TR.HW.Payment Consumer] (Extrai Contexto dos Headers)
                   └── [SQL INSERT Payment] (Mapeia latência financeira)
```