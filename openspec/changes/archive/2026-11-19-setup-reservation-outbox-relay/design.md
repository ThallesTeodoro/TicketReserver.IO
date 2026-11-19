# Design: Worker de Despacho Transacional (TR.HW.ReservationOutboxRelay)

## Context

A API de Reserva (`TR.API.Reservation`) grava eventos de negócio na tabela `outbox_messages` do PostgreSQL (`reservation_db`). O cluster Kafka em modo KRaft já está provisionado com os tópicos `reservation.v1.checkout_req`, `reservation.v1.hold_created.delayed` e `reservation.v1.checkout_req.DLQ` (conforme especificado em `infra-kafka-messaging`).

Este documento descreve as decisões de arquitetura e implementação para o worker desacoplado `TR.HW.ReservationOutboxRelay`.

## Goals / Non-Goals

**Goals:**
- Implementar o worker service .NET 10 (`BackgroundService`) com ciclo contínuo e adaptativo de polling.
- Executar leitura transacional atômica e não-bloqueante com Dapper e `Npgsql` usando `SELECT ... FOR UPDATE SKIP LOCKED`.
- Publicar eventos no Kafka via `Confluent.Kafka` com `Acks = All`, serialização JSON padronizada e chave de partição por `AggregateId` (ReservationId).
- Rotear mensagens com base no `EventType` para tópicos imediatos e delayed.
- Tratar falhas com recálculo de backoff exponencial e roteamento para DLQ após esgotamento de retentativas.
- Injetar headers de correlação e rastreabilidade W3C (`traceparent`).
- Disponibilizar Dockerfile multi-stage e inclusão no `docker-compose.yml`.

**Non-Goals:**
- Consumir eventos publicados no Kafka (responsabilidade de `TR.HW.Payment` e `TR.HW.ResTimeout`).
- Despachar eventos do catálogo de eventos (já atendido por CDC com Debezium).

## Decisions

### 1. Worker Service Desacoplado vs HostedService na API
* **Decisão:** Criar um projeto de aplicação de console/worker service dedicado (`TR.HW.ReservationOutboxRelay`) executado em container próprio.
* **Racional:** Preserva a separação de responsabilidades (SoC), evita consumo de threads de background na API de requisições HTTP e permite escalar o processamento de outbox independentemente da API de reservas.
* **Alternativa rejeitada:** Registrar como `IHostedService` dentro de `TR.API.Reservation.WebApi` (rejeitada por acoplar o ciclo de vida do worker ao web server e degradar a escalabilidade).

### 2. Concorrência Segura: `SELECT ... FOR UPDATE SKIP LOCKED`
* **Decisão:** A consulta de lote de mensagens utiliza a sintaxe nativa do PostgreSQL:
  ```sql
  SELECT id AS Id, correlation_id AS CorrelationId, event_type AS EventType,
         aggregate_type AS AggregateType, aggregate_id AS AggregateId,
         payload AS Payload, retry_count AS RetryCount
  FROM outbox_messages
  WHERE status = 0 -- PENDING
    AND deliver_after <= @UtcNow
  ORDER BY created_at ASC
  LIMIT @BatchSize
  FOR UPDATE SKIP LOCKED;
  ```
* **Racional:** Garante que múltiplas instâncias ou réplicas do worker possam processar mensagens simultaneamente sem que ocorram conflitos de lock ou processamento duplicado de uma mesma linha.
* **Alternativa rejeitada:** Bloqueio pessimista de tabela inteira ou polling sem lock (geraria deadlocks e leituras sujas entre réplicas concorrentes).

### 3. Garantia de Entrega At-Least-Once
* **Decisão:** O status da mensagem só é atualizado para `PUBLISHED` (`UPDATE outbox_messages SET status = 1, processed_at = @Now WHERE id = @Id`) após a confirmação síncrona do broker Kafka (`PersistenceStatus.Persisted`).
* **Racional:** Elimina qualquer risco de perda de mensagens (*zero data loss*).
* **Mitigação de duplicidade:** Em caso de reinicialização inesperada do container após o envio e antes do UPDATE no Postgres, os serviços consumidores subsequentes utilizarão o padrão Inbox (tabela `inbox_messages`) com checagem de idempotência pelo `Id` do evento.

### 4. Particionamento Estrito por Reserva
* **Decisão:** O Kafka Producer define `Message.Key = aggregate_id.ToString()`.
* **Racional:** Garante que todas as mensagens da mesma reserva caiam na mesma partição do Kafka, mantendo a ordem estrita dos eventos daquela reserva.

### 5. Roteamento de Tópicos e Tratamento de DLQ
* **Decisão:** O mapeamento de tópicos é determinístico:
  - `reservation.checkout_req` ➔ `reservation.v1.checkout_req`
  - `reservation.hold_created` ➔ `reservation.v1.hold_created.delayed`
  - Mensagens que excederem o limite de retentativas (`MaxRetries = 5`) são marcadas como `FAILED` e despachadas para `reservation.v1.checkout_req.DLQ`.

## Risks / Trade-offs

- **[Risco: Queda temporária de rede com o Kafka]**
  - *Mitigação:* O worker captura a exceção do Confluent.Kafka, incrementa o `retry_count`, atualiza o `deliver_after` com backoff exponencial (`DeliverAfter = UtcNow + (RetryCount * 2)s`) e mantém a mensagem com status `PENDING` para reprocessamento futuro.
- **[Risco: Sobrecarga de polling na base quando a fila estiver vazia]**
  - *Mitigação:* Polling adaptativo: se o lote retornar vazio, o worker aguarda um intervalo suave configurável (ex: 500ms a 1000ms) antes do próximo ciclo. Se encontrar mensagens, processa o próximo lote imediatamente.
