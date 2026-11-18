# Tasks

## 1. Definição do Script DDL e Ajuste no Compose

- [x] 1.1 Criar o arquivo `docker/postgres/init-reservation-db.sql` contendo a extensão `uuid-ossp`, os enums `reservation_status_enum` e `outbox_status_enum`, as tabelas `reservations`, `reservation_items`, `outbox_messages`, `inbox_messages` e os índices parciais especificados em `reservation-db.md`.
- [x] 1.2 Atualizar o arquivo raiz `docker-compose.yml` para mapear deterministamente os scripts de inicialização de eventos e reservas em `/docker-entrypoint-initdb.d/`, e validar a sintaxe com `docker compose config`.

## 2. Provisionamento no PostgreSQL e Validação das Estruturas

- [x] 2.1 Criar o banco `reservation_db` de forma idempotente no container `ticketreserver-postgres` em execução e aplicar o script DDL `docker/postgres/init-reservation-db.sql` via `psql`.
- [x] 2.2 Inspecionar as tabelas, tipos e índices criados no banco `reservation_db` utilizando comandos administrativos do PostgreSQL (`\dt`, `\dT`, `\di`).
- [x] 2.3 Executar testes de fumaça relacionais (inserção de reserva com status default, inserção de item com cascade, inserção de evento outbox e validação de CHECK constraints de valores negativos), limpando os registros de teste em seguida.
