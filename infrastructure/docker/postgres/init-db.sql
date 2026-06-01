-- Script de inicialização para o Postgres
-- Criação das bases de dados segregadas para cada microsserviço

CREATE DATABASE reservation_db;
CREATE DATABASE payment_db;
CREATE DATABASE ticketing_db;

-- Permissões básicas (opcional, dependendo do usuário configurado no docker-compose)
-- GRANT ALL PRIVILEGES ON DATABASE reservation_db TO postgres;
-- GRANT ALL PRIVILEGES ON DATABASE payment_db TO postgres;
-- GRANT ALL PRIVILEGES ON DATABASE ticketing_db TO postgres;
