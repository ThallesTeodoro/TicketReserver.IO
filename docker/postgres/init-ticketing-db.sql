-- ============================================================================
-- TicketReserver.IO - Database Initialization: ticketing_db
-- Authoritative Schema for Ticketing Service (SVC_TICKETING)
-- ============================================================================

-- Criação idempotente da base lógica ticketing_db se ainda não existir
SELECT 'CREATE DATABASE ticketing_db'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'ticketing_db');
\gexec

\connect ticketing_db

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- ============================================================================
-- 1. TIPOS ENUMERADOS NATIVOS
-- ============================================================================
DO $$ BEGIN
    CREATE TYPE ticket_status_enum AS ENUM (
        'ACTIVE',               -- Ingresso válido e pronto para uso
        'USED',                 -- Já utilizado na catraca (check-in efetuado)
        'REVOKED',              -- Revogado (estorno, fraude ou cancelamento)
        'BLOCKED'               -- Bloqueado temporariamente por investigação
    );
EXCEPTION
    WHEN duplicate_object THEN null;
END $$;

DO $$ BEGIN
    CREATE TYPE allocation_type_enum AS ENUM (
        'NUMBERED_SEAT',        -- Assento numerado físico
        'GENERAL_ADMISSION'     -- Cota de lote / pista comum
    );
EXCEPTION
    WHEN duplicate_object THEN null;
END $$;

DO $$ BEGIN
    CREATE TYPE checkin_status_enum AS ENUM (
        'APPROVED',
        'ALREADY_USED',
        'REVOKED_TICKET',
        'DEVICE_UNAUTHORIZED'
    );
EXCEPTION
    WHEN duplicate_object THEN null;
END $$;

DO $$ BEGIN
    CREATE TYPE ticketing_outbox_status_enum AS ENUM (
        'PENDING',
        'PROCESSING',
        'PUBLISHED',
        'FAILED'
    );
EXCEPTION
    WHEN duplicate_object THEN null;
END $$;

-- ============================================================================
-- 2. TABELA: TICKETS (Ingressos Emitidos)
-- ============================================================================
CREATE TABLE IF NOT EXISTS tickets (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    ticket_code VARCHAR(32) NOT NULL,           -- Código amigável (ex: 'TKT-2026-X89J21')
    qr_code_hash VARCHAR(256) NOT NULL,         -- Token criptográfico assinado (HMAC-SHA256)
    reservation_id UUID NOT NULL,
    payment_id UUID NOT NULL,
    customer_id UUID NOT NULL,
    event_id UUID NOT NULL,
    ticket_tier_id UUID NOT NULL,
    seat_id UUID,                               -- NULL para ingressos sem assento numerado (ex: Pista)
    seat_label VARCHAR(80),                     -- 'Setor Oeste, Fila C, Assento 12'
    status ticket_status_enum NOT NULL DEFAULT 'ACTIVE',
    price_paid NUMERIC(12, 2) NOT NULL CHECK (price_paid >= 0),
    issued_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    checked_in_at TIMESTAMPTZ,
    revoked_at TIMESTAMPTZ,
    revocation_reason VARCHAR(150),
    version INT NOT NULL DEFAULT 1,             -- Optimistic Concurrency Control
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT uq_tickets_code UNIQUE (ticket_code),
    CONSTRAINT uq_tickets_qr_hash UNIQUE (qr_code_hash)
);

-- ============================================================================
-- 3. TABELA: SEAT_ALLOCATIONS (Garantia Atômica de Inventário / Anti-Double Booking)
-- ============================================================================
CREATE TABLE IF NOT EXISTS seat_allocations (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    event_id UUID NOT NULL,
    seat_id UUID NOT NULL,                      -- Para pista sem cadeira, ID sintetizado de slot
    reservation_id UUID NOT NULL,
    ticket_id UUID NOT NULL,
    allocation_type allocation_type_enum NOT NULL DEFAULT 'NUMBERED_SEAT',
    allocated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT fk_allocations_ticket FOREIGN KEY (ticket_id) REFERENCES tickets(id) ON DELETE RESTRICT,
    -- BARREIRA DE CONCORRÊNCIA: Se 2 workers tentarem gravar a mesma vaga, o Postgres rejeita (23505)!
    CONSTRAINT uq_seat_allocations_event_seat UNIQUE (event_id, seat_id)
);

-- ============================================================================
-- 4. TABELA: TICKET_CHECKINS (Auditoria de Validação em Catracas)
-- ============================================================================
CREATE TABLE IF NOT EXISTS ticket_checkins (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    ticket_id UUID NOT NULL,
    gate_name VARCHAR(60) NOT NULL,             -- ex: 'Portão 3 - Acesso Pista'
    device_id VARCHAR(60) NOT NULL,             -- Identificador do terminal/leitor
    status checkin_status_enum NOT NULL,
    details TEXT,
    validated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT fk_checkins_ticket FOREIGN KEY (ticket_id) REFERENCES tickets(id) ON DELETE CASCADE
);

-- ============================================================================
-- 5. TABELA: OUTBOX_MESSAGES (Transactional Outbox)
-- ============================================================================
CREATE TABLE IF NOT EXISTS outbox_messages (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    correlation_id UUID NOT NULL,
    event_type VARCHAR(150) NOT NULL,           -- 'ticketing.v1.issued', 'ticketing.v1.failed'
    aggregate_type VARCHAR(100) NOT NULL,       -- 'Ticket'
    aggregate_id UUID NOT NULL,
    payload JSONB NOT NULL,
    status ticketing_outbox_status_enum NOT NULL DEFAULT 'PENDING',
    retry_count INT NOT NULL DEFAULT 0,
    error_message TEXT,
    processed_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ============================================================================
-- 6. TABELA: INBOX_MESSAGES (Idempotência de Consumo)
-- ============================================================================
CREATE TABLE IF NOT EXISTS inbox_messages (
    message_id VARCHAR(150) NOT NULL,
    consumer_group VARCHAR(100) NOT NULL,
    event_type VARCHAR(150) NOT NULL,
    processed_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT pk_ticketing_inbox PRIMARY KEY (message_id, consumer_group)
);

-- ============================================================================
-- 7. ÍNDICES DE PERFORMANCE E CONSULTA EM PORTARIA
-- ============================================================================
-- Busca rápida de ingressos por cliente (Carteira / App Mobile)
CREATE INDEX IF NOT EXISTS idx_tickets_customer 
ON tickets (customer_id, status);

-- Busca de ingressos por evento (para borderô e relatórios)
CREATE INDEX IF NOT EXISTS idx_tickets_event 
ON tickets (event_id, status);

-- Validação instantânea por código ou hash nas catracas (leitura de alta frequência)
CREATE INDEX IF NOT EXISTS idx_tickets_hash_lookup 
ON tickets (qr_code_hash) 
WHERE status = 'ACTIVE';

-- Polling de Outbox
CREATE INDEX IF NOT EXISTS idx_ticketing_outbox_pending 
ON outbox_messages (created_at, status) 
WHERE status = 'PENDING';

-- Histórico de leituras por ingresso
CREATE INDEX IF NOT EXISTS idx_checkins_ticket 
ON ticket_checkins (ticket_id);
