# Proposal: Setup do Worker de Despacho Transacional (TR.HW.ReservationOutboxRelay)

## Why

A API de Reserva (`TR.API.Reservation`) implementa o Transactional Outbox Pattern, gravando eventos de negócio de forma atômica na tabela `outbox_messages` do PostgreSQL (`reservation_db`). No entanto, esses eventos permanecem retidos no banco até que um despachador assíncrono os encaminhe para o Apache Kafka.

Para viabilizar a continuidade da saga distribuída de compra (consumo pelo futuro worker de pagamento) e a reconciliação de holds expirados após 10 minutos (consumo pelo worker de timeout), é necessário criar o worker desacoplado `TR.HW.ReservationOutboxRelay`. Esse componente garante a entrega *At-Least-Once*, elimina contenção de concorrência com `SKIP LOCKED` e mantém o isolamento arquitetural previsto no desenho do sistema.

## What Changes

- **Criação do Worker Service .NET 10 (`TR.HW.ReservationOutboxRelay`):** Background service resiliente operando como daemon desacoplado.
- **Leitura Segura de Outbox com Dapper e PostgreSQL:** Execução de queries com `SELECT ... FOR UPDATE SKIP LOCKED` para lotes configuráveis com `deliver_after <= NOW()` e `status = PENDING`.
- **Roteamento Inteligente e Particionamento Kafka:**
  - `reservation.checkout_req` ➔ publicado no tópico `reservation.v1.checkout_req`.
  - `reservation.hold_created` ➔ publicado no tópico `reservation.v1.hold_created.delayed`.
  - Chave de partição (`Key`): `AggregateId.ToString()` (ReservationId), assegurando garantia estrita de ordenação por reserva.
- **Confirmação Transacional e Política de Retries:** Atualização para `PUBLISHED` somente após ACK do broker Kafka (`Acks = All`). Tratamento de erros com backoff exponencial no `deliver_after` e envio para DLQ (`reservation.v1.checkout_req.DLQ`) em caso de esgotamento de retentativas.
- **Rastreabilidade e Observabilidade:** Propagação de contexto W3C `traceparent` nos headers das mensagens Kafka e integração de logs estruturados com Serilog.
- **Empacotamento e Execução Conteinerizada:** Dockerfile multi-stage .NET 10 e `docker-compose.yml` integrado à rede interna `ticketreserver-internal-net`.

## Capabilities

### New Capabilities

- `reservation-outbox-relay`: Despacho resiliente, transacional e particionado de eventos da tabela outbox da base de reserva para os tópicos do Apache Kafka.

### Modified Capabilities

*(Nenhuma. As capacidades da API de Reserva e do barramento Kafka já provisionadas permanecem preservadas).*

## Impact

- **Nova Solução e Projetos:** `TR.HW.ReservationOutboxRelay/` contendo projeto executável do Worker e projeto de testes unitários `TR.HW.ReservationOutboxRelay.UnitTests`.
- **Acesso a Dados:** Conexão com `reservation_db` (PostgreSQL) usando Dapper sem alterar a estrutura de tabelas existente.
- **Dependências de Infraestrutura:** Conexão com o broker Kafka (`kafka:9092` internamente, `localhost:9094` para debug) utilizando `Confluent.Kafka`.
- **Zero Breaking Changes:** A API de Reserva continua gravando no outbox sem modificações nos contratos de domínio.
