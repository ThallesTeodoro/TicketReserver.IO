-- Script de inicialização para o Postgres
-- Criação das bases de dados segregadas para cada microsserviço

CREATE DATABASE reservation_db;
CREATE DATABASE payment_db;
CREATE DATABASE ticketing_db;

\c reservation_db;

CREATE TABLE IF NOT EXISTS Reservations (
    Id UUID PRIMARY KEY,
    UserId UUID NOT NULL,
    EventId UUID NOT NULL,
    SeatNumber VARCHAR(10) NOT NULL,
    Price NUMERIC(10,2) NOT NULL,
    Status VARCHAR(20) NOT NULL,
    CreatedAt TIMESTAMP NOT NULL
);

CREATE TABLE IF NOT EXISTS OutboxMessages (
    Id UUID PRIMARY KEY,
    CorrelationId VARCHAR(50) NOT NULL,
    EventName VARCHAR(100) NOT NULL,
    EventType VARCHAR(100) NOT NULL,
    Payload JSONB NOT NULL,
    Status SMALLINT NOT NULL,
    Attempts INT NOT NULL DEFAULT 0,
    LastAttemptAt TIMESTAMP,
    ErrorReason TEXT,
    CreatedAt TIMESTAMP NOT NULL,
    ProcessedAt TIMESTAMP
);

-- Índices para performance do Outbox
CREATE INDEX IF NOT EXISTS IX_OutboxMessages_Status ON OutboxMessages (Status) WHERE Status IN (1, 2);

\c payment_db;

CREATE TABLE IF NOT EXISTS Payments (
    Id UUID PRIMARY KEY,
    ReservationId UUID NOT NULL UNIQUE,
    Amount NUMERIC(10,2) NOT NULL,
    Status VARCHAR(20) NOT NULL,
    CreatedAt TIMESTAMP NOT NULL
);

\c ticketing_db;

CREATE TABLE IF NOT EXISTS Tickets (
    Id UUID PRIMARY KEY,
    ReservationId UUID NOT NULL,
    UserId UUID NOT NULL,
    QrCodePayload TEXT NOT NULL,
    IssuedAt TIMESTAMP NOT NULL
);
