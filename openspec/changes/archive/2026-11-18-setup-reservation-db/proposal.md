# Proposal: Provisionar Banco de Dados reservation_db no PostgreSQL

## Why

O microsserviço de Reservas (`SVC_RESERVA` / `TR.API.Reservation`) necessita de um banco de dados relacional dedicado (`reservation_db`) para persistir o ciclo de vida das reservas de ingressos, seus itens vinculados, o log transacional de mensagens do Transactional Outbox Pattern e o log de desduplicação do Inbox Pattern. A criação antecipada deste banco e de seu esquema físico (DDL completo, tipos enumerados e índices parciais de alta performance) é o pré-requisito mandatório para permitir a implementação da solução .NET `TR.API.Reservation`.

## What Changes

- Criação do script DDL modular `docker/postgres/init-reservation-db.sql` contendo:
  - Extensão `uuid-ossp`.
  - Tipos enumerados nativos: `reservation_status_enum` (`AWAITING_PAYMENT`, `PROCESSING_PAYMENT`, `CONFIRMED`, `EXPIRED`, `CANCELLED`) e `outbox_status_enum` (`PENDING`, `PROCESSING`, `PUBLISHED`, `FAILED`).
  - Tabelas de domínio: `reservations` (com OCC versionado e restrições de total) e `reservation_items` (com chave estrangeira em cascata).
  - Tabelas de resiliência transacional: `outbox_messages` (com suporte a delay em segundos e payload jsonb) e `inbox_messages` (chave composta por `message_id` e `consumer_group`).
  - Índices especializados: `idx_outbox_pending` (filtrado por `status = 'PENDING'` para suportar `FOR UPDATE SKIP LOCKED`), `idx_reservations_timeout_check` e `idx_reservations_customer_status`.
- Execução determinística e idempotente do provisionamento da base lógica `reservation_db` no container ativo `ticketreserver-postgres`.
- Atualização da configuração Docker Compose para garantir que em reinicializações com volume limpo ambos os bancos (`catalog_events_db` e `reservation_db`) sejam criados automaticamente.

## Capabilities

### New Capabilities
- `reservation-db`: Armazenamento relacional PostgreSQL 16 para o microsserviço de Reservas (`SVC_RESERVA`), com suporte a Transactional Outbox, Inbox Pattern e índices de alta concorrência.

### Modified Capabilities
<!-- Nenhuma capability existente tem seus requisitos alterados nesta mudança de banco de dados. -->

## Impact

- **Banco de Dados Relacional:** Adição do banco lógico `reservation_db` no PostgreSQL sem afetar o banco existente `catalog_events_db`.
- **Habilitação de Serviços:** Permite o desenvolvimento da API `TR.API.Reservation` e do worker `TR.HW.OutboxRelay`.
