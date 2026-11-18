# Design: Provisionamento do Banco de Dados reservation_db

## Context

O cluster PostgreSQL 16 (`ticketreserver-postgres`) já está em execução no Docker Compose na rede `ticketreserver-internal-net`, operando com a base `catalog_events_db` e volume `postgres_data`. A especificação técnica `reservation-db.md` detalha o esquema físico do microsserviço de Reservas (`SVC_RESERVA`), compreendendo entidades ricas, controle de concorrência otimista (OCC), enums nativos, e tabelas com índices parciais para Transactional Outbox e Inbox Pattern.

## Goals / Non-Goals

**Goals:**
- Criar a base de dados lógica `reservation_db` no cluster PostgreSQL existente de forma idempotente, sem perda ou interferência no `catalog_events_db`.
- Definir o script DDL canônico em `docker/postgres/init-reservation-db.sql` contendo todos os tipos enumerados, tabelas, foreign keys e índices parciais.
- Assegurar compatibilidade nativa com o futuro mapeamento Npgsql 10 / EF Core da solução `TR.API.Reservation`.
- Atualizar a configuração do Docker Compose para que reinicializações limpas executem tanto o script de catálogo quanto o de reserva na pasta `/docker-entrypoint-initdb.d/`.

**Non-Goals:**
- Criação da solução em C# .NET da API `TR.API.Reservation` (será tratada na próxima change).
- Implementação dos jobs ou workers em background que consom as tabelas de outbox e inbox.

## Decisions

### 1. Separação Modular do Script DDL (`init-reservation-db.sql`)
* **Decisão:** Criar um arquivo DDL independente `docker/postgres/init-reservation-db.sql` dedicado ao domínio de reservas.
* **Racional:** Mantém a separação estrita de responsabilidades entre microsserviços (Database-per-Service lógico). Cada serviço possui seu próprio contrato e script DDL.
* **Alternativas consideradas:**
  * *Unificar todos os esquemas em um único script:* Rejeitado por acoplar o ciclo de evolução do catálogo e da reserva.

### 2. Idempotência em Enums e Estruturas Relacionais
* **Decisão:** Tratar a criação de tipos enumerados (`reservation_status_enum` e `outbox_status_enum`) através de blocos PL/pgSQL anônimos (`DO $$ BEGIN ... EXCEPTION WHEN duplicate_object THEN NULL; END $$`) e tabelas com `CREATE TABLE IF NOT EXISTS`.
* **Racional:** Permite que o script seja reexecutado múltiplas vezes em qualquer ambiente de desenvolvimento ou CI sem falhar caso os tipos já existam.

### 3. Provisionamento em Dois Estágios (Ambiente Atual vs Fresh Boot)
* **Decisão:** 
  1. No container em execução (`ticketreserver-postgres`), criar a base lógica `reservation_db` (verificando via `pg_database` para evitar erro de banco existente) e aplicar o script DDL diretamente via `psql`.
  2. No `docker-compose.yml`, renomear os pontos de montagem para ordem determinística no `docker-entrypoint-initdb.d/` (`01-init-events-db.sql` e `02-init-reservation-db.sql`), assegurando que em fresh volumes ambos os bancos sejam provisionados.
* **Racional:** O diretório `/docker-entrypoint-initdb.d/` oficial do Postgres só é acionado na primeira inicialização de um volume limpo. O provisionamento imediato via `psql` garante que o ambiente atual fique pronto de imediato sem destruir o volume `postgres_data`.

## Risks / Trade-offs

- **[Risco: Conexão concorrente durante a criação do banco]**
  * *Mitigação:* A criação do banco e aplicação de DDL será executada diretamente via usuário `postgres` administrativo com script sequencial.
- **[Risco: Incompatibilidade de nomes de enums com C#]**
  * *Mitigação:* Os valores dos enums no Postgres utilizam snake_case/uppercase (`AWAITING_PAYMENT`, `PENDING`), 100% aderentes à especificação `reservation-db.md` e testados com o `NpgsqlNullNameTranslator` no `TR.API.Events`.
