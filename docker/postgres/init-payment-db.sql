-- ============================================================================
-- TicketReserver.IO - Database Initialization: payment_db
-- Authoritative Schema for Payment Service (SVC_PAGAMENTO)
-- ============================================================================

-- Criação idempotente da base lógica payment_db se ainda não existir
SELECT 'CREATE DATABASE payment_db'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'payment_db');
\gexec

\connect payment_db

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- ============================================================================
-- 1. TIPOS ENUMERADOS NATIVOS
-- ============================================================================
DO $$ BEGIN
    CREATE TYPE payment_status_enum AS ENUM (
        'PENDING',              -- Registro criado, aguardando resposta do gateway
        'AUTHORIZED',           -- Pré-autorizado no cartão
        'CAPTURED',             -- Capturado com sucesso (Dinheiro liquidado)
        'DECLINED',             -- Recusado pelo gateway/emissor (saldo, antifraude, etc.)
        'REFUNDED',             -- Estornado integralmente
        'PARTIALLY_REFUNDED',   -- Estornado parcialmente
        'FAILED'                -- Falha técnica irrecuperável de comunicação
    );
EXCEPTION
    WHEN duplicate_object THEN null;
END $$;

DO $$ BEGIN
    CREATE TYPE payment_method_enum AS ENUM (
        'CREDIT_CARD',
        'DEBIT_CARD',
        'PIX'
    );
EXCEPTION
    WHEN duplicate_object THEN null;
END $$;

DO $$ BEGIN
    CREATE TYPE transaction_type_enum AS ENUM (
        'AUTHORIZE',
        'CAPTURE',
        'VOID',
        'REFUND'
    );
EXCEPTION
    WHEN duplicate_object THEN null;
END $$;

DO $$ BEGIN
    CREATE TYPE transaction_status_enum AS ENUM (
        'SUCCESS',
        'FAILED'
    );
EXCEPTION
    WHEN duplicate_object THEN null;
END $$;

DO $$ BEGIN
    CREATE TYPE refund_status_enum AS ENUM (
        'PENDING',
        'COMPLETED',
        'FAILED'
    );
EXCEPTION
    WHEN duplicate_object THEN null;
END $$;

DO $$ BEGIN
    CREATE TYPE payment_outbox_status_enum AS ENUM (
        'PENDING',
        'PROCESSING',
        'PUBLISHED',
        'FAILED'
    );
EXCEPTION
    WHEN duplicate_object THEN null;
END $$;

-- ============================================================================
-- 2. TABELA: PAYMENTS (Agregado Raiz de Pagamento)
-- ============================================================================
CREATE TABLE IF NOT EXISTS payments (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    reservation_id UUID NOT NULL,
    customer_id UUID NOT NULL,
    amount NUMERIC(12, 2) NOT NULL CHECK (amount > 0),
    currency VARCHAR(3) NOT NULL DEFAULT 'BRL',
    payment_method payment_method_enum NOT NULL DEFAULT 'CREDIT_CARD',
    status payment_status_enum NOT NULL DEFAULT 'PENDING',
    gateway_provider VARCHAR(50) NOT NULL DEFAULT 'STRIPE',
    gateway_transaction_id VARCHAR(100),
    authorization_code VARCHAR(50),
    card_last_four VARCHAR(4),
    card_brand VARCHAR(30),
    failure_reason VARCHAR(255),
    version INT NOT NULL DEFAULT 1,             -- Optimistic Concurrency Control
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    -- REGRA DE OURO: Impede que uma mesma reserva seja cobrada duas vezes
    CONSTRAINT uq_payments_reservation_id UNIQUE (reservation_id)
);

-- ============================================================================
-- 3. TABELA: PAYMENT_TRANSACTIONS (Ledger / Histórico Imutável)
-- ============================================================================
CREATE TABLE IF NOT EXISTS payment_transactions (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    payment_id UUID NOT NULL,
    transaction_type transaction_type_enum NOT NULL,
    amount NUMERIC(12, 2) NOT NULL CHECK (amount > 0),
    status transaction_status_enum NOT NULL,
    gateway_reference VARCHAR(150),
    request_payload JSONB,
    response_payload JSONB,
    error_details TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT fk_transactions_payment FOREIGN KEY (payment_id) REFERENCES payments(id) ON DELETE RESTRICT
);

-- ============================================================================
-- 4. TABELA: REFUNDS (Registro de Estornos / Auto-Refund)
-- ============================================================================
CREATE TABLE IF NOT EXISTS refunds (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    payment_id UUID NOT NULL,
    reservation_id UUID NOT NULL,
    amount NUMERIC(12, 2) NOT NULL CHECK (amount > 0),
    currency VARCHAR(3) NOT NULL DEFAULT 'BRL',
    reason VARCHAR(150) NOT NULL,
    status refund_status_enum NOT NULL DEFAULT 'PENDING',
    gateway_refund_id VARCHAR(100),
    gateway_response JSONB,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT fk_refunds_payment FOREIGN KEY (payment_id) REFERENCES payments(id) ON DELETE RESTRICT
);

-- ============================================================================
-- 5. TABELA: OUTBOX_MESSAGES (Transactional Outbox)
-- ============================================================================
CREATE TABLE IF NOT EXISTS outbox_messages (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    correlation_id UUID NOT NULL,
    event_type VARCHAR(150) NOT NULL,           -- 'payment.approved', 'payment.declined', 'payment.refunded'
    aggregate_type VARCHAR(100) NOT NULL DEFAULT 'Payment',
    aggregate_id UUID NOT NULL,
    payload JSONB NOT NULL,
    status payment_outbox_status_enum NOT NULL DEFAULT 'PENDING',
    retry_count INT NOT NULL DEFAULT 0,
    error_message TEXT,
    processed_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ============================================================================
-- 6. TABELA: INBOX_MESSAGES (Idempotência no Consumo Kafka)
-- ============================================================================
CREATE TABLE IF NOT EXISTS inbox_messages (
    message_id VARCHAR(150) NOT NULL,
    consumer_group VARCHAR(100) NOT NULL,
    event_type VARCHAR(150) NOT NULL,
    processed_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT pk_payment_inbox PRIMARY KEY (message_id, consumer_group)
);

-- ============================================================================
-- 7. ÍNDICES DE PERFORMANCE E AUDITORIA
-- ============================================================================
CREATE INDEX IF NOT EXISTS idx_payments_customer_status 
ON payments (customer_id, status);

CREATE INDEX IF NOT EXISTS idx_payments_gateway_tx 
ON payments (gateway_transaction_id) 
WHERE gateway_transaction_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_payment_outbox_pending 
ON outbox_messages (created_at, status) 
WHERE status = 'PENDING';

CREATE INDEX IF NOT EXISTS idx_transactions_payment 
ON payment_transactions (payment_id);

CREATE INDEX IF NOT EXISTS idx_refunds_reservation 
ON refunds (reservation_id);
