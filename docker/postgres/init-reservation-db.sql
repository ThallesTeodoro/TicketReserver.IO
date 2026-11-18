-- ============================================================================
-- TicketReserver.IO - Database Initialization: reservation_db
-- Authoritative Schema for Reservation Service (SVC_RESERVA)
-- ============================================================================

-- Criação idempotente da base lógica reservation_db se ainda não existir
SELECT 'CREATE DATABASE reservation_db'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'reservation_db');
\gexec

\connect reservation_db

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- ============================================================================
-- 1. TIPOS ENUMERADOS NATIVOS
-- ============================================================================
DO $$ BEGIN
    CREATE TYPE reservation_status_enum AS ENUM (
        'AWAITING_PAYMENT',     -- Hold ativo, aguardando início do checkout
        'PROCESSING_PAYMENT',   -- Usuário iniciou pagamento (checkout disparado)
        'CONFIRMED',            -- Pagamento aprovado e ingresso emitido com sucesso
        'EXPIRED',              -- 10 minutos esgotados sem confirmação de pagamento
        'CANCELLED'             -- Cancelada pelo usuário ou compensada por estorno
    );
EXCEPTION
    WHEN duplicate_object THEN null;
END $$;

DO $$ BEGIN
    CREATE TYPE outbox_status_enum AS ENUM (
        'PENDING',              -- Gravado atomicamente com a reserva
        'PROCESSING',           -- Em processamento pelo worker de relay
        'PUBLISHED',            -- Enviado com sucesso ao Kafka
        'FAILED'                -- Falha persistente após retentativas
    );
EXCEPTION
    WHEN duplicate_object THEN null;
END $$;

-- ============================================================================
-- 2. TABELA: RESERVATIONS (Agregado Raiz de Reserva)
-- ============================================================================
CREATE TABLE IF NOT EXISTS reservations (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    customer_id UUID NOT NULL,
    event_id UUID NOT NULL,
    status reservation_status_enum NOT NULL DEFAULT 'AWAITING_PAYMENT',
    total_amount NUMERIC(12, 2) NOT NULL CHECK (total_amount >= 0),
    currency VARCHAR(3) NOT NULL DEFAULT 'BRL',
    hold_expires_at TIMESTAMPTZ NOT NULL,
    payment_attempted_at TIMESTAMPTZ,
    confirmed_at TIMESTAMPTZ,
    cancelled_at TIMESTAMPTZ,
    cancellation_reason VARCHAR(150),
    version INT NOT NULL DEFAULT 1,             -- Optimistic Concurrency Control (OCC)
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ============================================================================
-- 3. TABELA: RESERVATION_ITEMS (Assentos ou Lotes Vinculados à Reserva)
-- ============================================================================
CREATE TABLE IF NOT EXISTS reservation_items (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    reservation_id UUID NOT NULL,
    ticket_tier_id UUID NOT NULL,
    seat_id UUID,                               -- NULL para assentos não-numerados (ex: Pista)
    seat_label VARCHAR(50),                     -- Denormalizado para agilidade visual
    quantity INT NOT NULL DEFAULT 1 CHECK (quantity > 0),
    unit_price NUMERIC(12, 2) NOT NULL CHECK (unit_price >= 0),
    subtotal NUMERIC(12, 2) NOT NULL CHECK (subtotal >= 0),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT fk_res_items_reservation FOREIGN KEY (reservation_id) REFERENCES reservations(id) ON DELETE CASCADE
);

-- ============================================================================
-- 4. TABELA: OUTBOX_MESSAGES (Transactional Outbox Pattern)
-- ============================================================================
CREATE TABLE IF NOT EXISTS outbox_messages (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    correlation_id UUID NOT NULL,
    event_type VARCHAR(150) NOT NULL,           -- 'reservation.hold_created', 'reservation.checkout_req'
    aggregate_type VARCHAR(100) NOT NULL,       -- 'Reservation'
    aggregate_id UUID NOT NULL,
    payload JSONB NOT NULL,
    delay_seconds INT NOT NULL DEFAULT 0,       -- 0 = Imediato, 600 = Tópico com delay de 10 min
    deliver_after TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    status outbox_status_enum NOT NULL DEFAULT 'PENDING',
    retry_count INT NOT NULL DEFAULT 0,
    error_message TEXT,
    processed_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ============================================================================
-- 5. TABELA: INBOX_MESSAGES (Idempotência / Desduplicação de Consumo)
-- ============================================================================
CREATE TABLE IF NOT EXISTS inbox_messages (
    message_id VARCHAR(150) NOT NULL,
    consumer_group VARCHAR(100) NOT NULL,
    event_type VARCHAR(150) NOT NULL,
    processed_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT pk_inbox_messages PRIMARY KEY (message_id, consumer_group)
);

-- ============================================================================
-- 6. ÍNDICES DE PERFORMANCE E ALTA CONCORRÊNCIA
-- ============================================================================
-- Busca rápida de reservas ativas por cliente (para tela de checkout)
CREATE INDEX IF NOT EXISTS idx_reservations_customer_status 
ON reservations (customer_id, status);

-- Otimização extrema para o Outbox Relay (Worker lê pendentes ordenados por liberação)
CREATE INDEX IF NOT EXISTS idx_outbox_pending 
ON outbox_messages (deliver_after, status) 
WHERE status = 'PENDING';

-- Otimização para expiração rápida via job de contingência caso o Kafka atrase
CREATE INDEX IF NOT EXISTS idx_reservations_timeout_check 
ON reservations (hold_expires_at, status) 
WHERE status = 'AWAITING_PAYMENT';

-- Itens por reserva
CREATE INDEX IF NOT EXISTS idx_res_items_reservation 
ON reservation_items (reservation_id);
