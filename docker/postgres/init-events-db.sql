-- ============================================================================
-- TicketReserver.IO - Database Initialization: catalog_events_db
-- Authoritative Schema for Event Catalog Service (SVC_EVENTOS)
-- ============================================================================

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- ============================================================================
-- 1. TABELA: VENUES (Locais, Arenas, Estádios e Teatros)
-- ============================================================================
CREATE TABLE IF NOT EXISTS venues (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name VARCHAR(150) NOT NULL,
    slug VARCHAR(160) NOT NULL,
    description TEXT,
    address VARCHAR(255) NOT NULL,
    city VARCHAR(100) NOT NULL,
    state VARCHAR(50) NOT NULL,
    country VARCHAR(50) NOT NULL DEFAULT 'BR',
    postal_code VARCHAR(20) NOT NULL,
    timezone VARCHAR(50) NOT NULL DEFAULT 'America/Sao_Paulo',
    latitude NUMERIC(10, 8),
    longitude NUMERIC(11, 8),
    total_capacity INT NOT NULL CHECK (total_capacity > 0),
    metadata JSONB DEFAULT '{}'::jsonb,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT uq_venues_slug UNIQUE (slug)
);

-- ============================================================================
-- 2. TABELA: SEAT_SECTIONS (Setores Físicos do Local)
-- ============================================================================
CREATE TABLE IF NOT EXISTS seat_sections (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    venue_id UUID NOT NULL,
    name VARCHAR(100) NOT NULL,
    code VARCHAR(30) NOT NULL,
    is_numbered BOOLEAN NOT NULL DEFAULT FALSE,
    capacity INT NOT NULL CHECK (capacity > 0),
    display_order INT NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT fk_sections_venue FOREIGN KEY (venue_id) REFERENCES venues(id) ON DELETE RESTRICT,
    CONSTRAINT uq_sections_venue_code UNIQUE (venue_id, code)
);

-- ============================================================================
-- 3. TABELA: SEATS (Assentos Físicos para Setores Numerados)
-- ============================================================================
CREATE TABLE IF NOT EXISTS seats (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    section_id UUID NOT NULL,
    row_name VARCHAR(10) NOT NULL,
    seat_number VARCHAR(10) NOT NULL,
    seat_label VARCHAR(30) NOT NULL,
    seat_type VARCHAR(30) NOT NULL DEFAULT 'STANDARD',
    is_accessible BOOLEAN NOT NULL DEFAULT FALSE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT fk_seats_section FOREIGN KEY (section_id) REFERENCES seat_sections(id) ON DELETE CASCADE,
    CONSTRAINT uq_seats_section_row_number UNIQUE (section_id, row_name, seat_number)
);

-- ============================================================================
-- 4. TABELA: EVENTS (Shows, Jogos, Festivais e Espetáculos)
-- ============================================================================
DO $$ 
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'event_status_enum') THEN
        CREATE TYPE event_status_enum AS ENUM (
            'DRAFT',
            'PUBLISHED',
            'SALES_OPEN',
            'SALES_PAUSED',
            'SOLD_OUT',
            'CANCELLED',
            'FINISHED'
        );
    END IF;
END $$;

CREATE TABLE IF NOT EXISTS events (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    venue_id UUID NOT NULL,
    title VARCHAR(200) NOT NULL,
    slug VARCHAR(220) NOT NULL,
    description TEXT,
    category VARCHAR(60) NOT NULL,
    status event_status_enum NOT NULL DEFAULT 'DRAFT',
    sales_start_at TIMESTAMPTZ NOT NULL,
    sales_end_at TIMESTAMPTZ NOT NULL,
    event_start_at TIMESTAMPTZ NOT NULL,
    event_end_at TIMESTAMPTZ NOT NULL,
    age_restriction INT NOT NULL DEFAULT 0,
    banner_url VARCHAR(500),
    max_tickets_per_user INT NOT NULL DEFAULT 4 CHECK (max_tickets_per_user > 0),
    version INT NOT NULL DEFAULT 1,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT fk_events_venue FOREIGN KEY (venue_id) REFERENCES venues(id) ON DELETE RESTRICT,
    CONSTRAINT uq_events_slug UNIQUE (slug),
    CONSTRAINT chk_events_sales_window CHECK (sales_end_at > sales_start_at),
    CONSTRAINT chk_events_schedule CHECK (event_end_at > event_start_at),
    CONSTRAINT chk_events_dates_order CHECK (event_start_at >= sales_start_at)
);

-- ============================================================================
-- 5. TABELA: TICKET_TIERS (Lotes e Preços por Setor / Tipo de Ingresso)
-- ============================================================================
DO $$ 
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'ticket_tier_type_enum') THEN
        CREATE TYPE ticket_tier_type_enum AS ENUM (
            'STANDARD',
            'HALF_PRICE',
            'VIP_EXPERIENCE',
            'EARLY_BIRD',
            'SPONSOR'
        );
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'ticket_tier_status_enum') THEN
        CREATE TYPE ticket_tier_status_enum AS ENUM (
            'SCHEDULED',
            'ACTIVE',
            'SOLD_OUT',
            'EXPIRED'
        );
    END IF;
END $$;

CREATE TABLE IF NOT EXISTS ticket_tiers (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    event_id UUID NOT NULL,
    section_id UUID NOT NULL,
    name VARCHAR(100) NOT NULL,
    tier_type ticket_tier_type_enum NOT NULL DEFAULT 'STANDARD',
    price NUMERIC(12, 2) NOT NULL CHECK (price >= 0),
    currency VARCHAR(3) NOT NULL DEFAULT 'BRL',
    total_quantity INT NOT NULL CHECK (total_quantity > 0),
    sales_start_at TIMESTAMPTZ NOT NULL,
    sales_end_at TIMESTAMPTZ NOT NULL,
    status ticket_tier_status_enum NOT NULL DEFAULT 'SCHEDULED',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT fk_ticket_tiers_event FOREIGN KEY (event_id) REFERENCES events(id) ON DELETE CASCADE,
    CONSTRAINT fk_ticket_tiers_section FOREIGN KEY (section_id) REFERENCES seat_sections(id) ON DELETE RESTRICT,
    CONSTRAINT chk_tiers_sales_dates CHECK (sales_end_at > sales_start_at)
);

-- ============================================================================
-- 6. ÍNDICES DE ALTA PERFORMANCE
-- ============================================================================
CREATE INDEX IF NOT EXISTS idx_events_status_start_at 
ON events (status, event_start_at) 
WHERE status IN ('PUBLISHED', 'SALES_OPEN');

CREATE INDEX IF NOT EXISTS idx_events_category ON events (category);
CREATE INDEX IF NOT EXISTS idx_venues_city ON venues (city);
CREATE INDEX IF NOT EXISTS idx_ticket_tiers_event_status ON ticket_tiers (event_id, status);
CREATE INDEX IF NOT EXISTS idx_seats_section ON seats (section_id);

-- ============================================================================
-- 7. REPLICA IDENTITY (CDC / Debezium)
-- ============================================================================
ALTER TABLE venues REPLICA IDENTITY FULL;
ALTER TABLE events REPLICA IDENTITY FULL;
ALTER TABLE seat_sections REPLICA IDENTITY FULL;
ALTER TABLE ticket_tiers REPLICA IDENTITY FULL;
ALTER TABLE seats REPLICA IDENTITY FULL;

-- ============================================================================
-- 8. DETERMINISTIC INITIAL SEED (VENUES & SEAT SECTIONS)
-- ============================================================================
INSERT INTO venues (id, name, slug, description, address, city, state, country, postal_code, timezone, total_capacity)
VALUES
    ('11111111-1111-1111-1111-111111111111', 'Allianz Parque', 'allianz-parque', 'Arena multiuso localizada em Sao Paulo', 'Av. Francisco Matarazzo, 1705', 'Sao Paulo', 'SP', 'BR', '05001-200', 'America/Sao_Paulo', 45000),
    ('22222222-2222-2222-2222-222222222222', 'Autodromo de Interlagos', 'autodromo-interlagos', 'Autodromo Jose Carlos Pace para grandes festivais e corridas', 'Av. Sen. Teotonio Vilela, 261', 'Sao Paulo', 'SP', 'BR', '04801-010', 'America/Sao_Paulo', 80000)
ON CONFLICT (slug) DO NOTHING;

INSERT INTO seat_sections (id, venue_id, name, code, is_numbered, capacity, display_order)
VALUES
    ('11111111-1111-1111-1111-111111111101', '11111111-1111-1111-1111-111111111111', 'Pista Premium', 'PISTA-PREM', FALSE, 15000, 1),
    ('11111111-1111-1111-1111-111111111102', '11111111-1111-1111-1111-111111111111', 'Cadeira Inferior', 'CAD-INF', TRUE, 20000, 2),
    ('22222222-2222-2222-2222-222222222201', '22222222-2222-2222-2222-222222222222', 'Gramado Geral', 'GRAMADO', FALSE, 65000, 1),
    ('22222222-2222-2222-2222-222222222202', '22222222-2222-2222-2222-222222222222', 'Lounge VIP', 'LOUNGE-VIP', FALSE, 15000, 2)
ON CONFLICT (venue_id, code) DO NOTHING;

