# Spec Delta

## Purpose

Fornecer a base de dados relacional `reservation_db` no PostgreSQL 16 com suporte a enums nativos, tabelas de domínio do ciclo de vida de reservas e tabelas com índices parciais dedicados aos padrões Transactional Outbox e Inbox para o microsserviço de Reservas (SVC_RESERVA).

## ADDED Requirements

### Requirement: Provisionamento da Base Lógica reservation_db
O sistema SHALL disponibilizar o banco de dados lógico `reservation_db` no cluster PostgreSQL 16 com a extensão `uuid-ossp` habilitada para geração de identificadores únicos.

#### Scenario: Conexão bem-sucedida ao banco de reservas
- **WHEN** uma aplicação ou cliente administrativo conecta no PostgreSQL especificando a base `reservation_db`
- **THEN** a sessão é aberta com sucesso e permite operações DDL e DML

#### Scenario: Disponibilidade de geração nativa de UUIDs
- **WHEN** uma instrução SQL invoca a função `uuid_generate_v4()`
- **THEN** um identificador UUID versão 4 válido é retornado sem falhas de extensão ausente

### Requirement: Esquema Físico do Domínio de Reservas
O sistema SHALL implementar o tipo `reservation_status_enum` e as tabelas relacionais `reservations` e `reservation_items` garantindo integridade referencial e validação de invariantes numéricos.

#### Scenario: Criação de reserva com status padrão inicial
- **WHEN** um registro de reserva é inserido omitindo o campo status
- **THEN** o registro é persistido com o status `AWAITING_PAYMENT` e versão de concorrência otimista (OCC) igual a 1

#### Scenario: Rejeição de valores monetários e quantidades inválidas
- **WHEN** uma tentativa de inserção define `total_amount` negativo ou `quantity` menor ou igual a zero em itens
- **THEN** o banco de dados rejeita a operação com violação de CHECK constraint

#### Scenario: Exclusão em cascata dos itens da reserva
- **WHEN** uma reserva existente é deletada
- **THEN** todos os registros vinculados em `reservation_items` são excluídos automaticamente por CASCADE

### Requirement: Tabelas e Índices de Resiliência Transacional
O sistema SHALL disponibilizar o tipo `outbox_status_enum` e as tabelas `outbox_messages` e `inbox_messages` com índices parciais otimizados para busca concorrente sem bloqueios desnecessários.

#### Scenario: Registro de mensagem transacional no outbox
- **WHEN** um evento de domínio é inserido na tabela `outbox_messages` com `delay_seconds` maior que zero
- **THEN** o registro é salvo com status padrão `PENDING` e payload em formato JSONB

#### Scenario: Otimização de consulta com índice parcial do outbox
- **WHEN** o worker de relay executa `SELECT` filtrando por `status = 'PENDING'` ordenado por `deliver_after`
- **THEN** o PostgreSQL utiliza o índice parcial `idx_outbox_pending` garantindo consulta otimizada para `FOR UPDATE SKIP LOCKED`

#### Scenario: Garantia de idempotência no inbox
- **WHEN** duas inserções concorrentes tentam gravar o mesmo `message_id` para o mesmo `consumer_group` na tabela `inbox_messages`
- **THEN** a segunda inserção é rejeitada por violação de chave primária composta
