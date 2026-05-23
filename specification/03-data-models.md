# Modelagem de Dados e Esquemas (03-data-models.md)

## 1. Banco de Dados: `TicketReserver_Reservation_DB` (Postgres)

### Tabela: `Reservations`
*   `Id` (UUID, Primary Key)
*   `UserId` (UUID)
*   `EventId` (UUID)
*   `SeatNumber` (VARCHAR(10))
*   `Status` (VARCHAR(20)) -> Valores: `Pendente`, `Confirmada`, `Cancelada`
*   `CreatedAt` (TIMESTAMP)

### Tabela: `OutboxMessages`
*   `Id` (UUID, Primary Key)
*   `EventType` (VARCHAR(100)) -> Ex: `reservation.created`
*   `Payload` (JSONB)
*   `CreatedAt` (TIMESTAMP)
*   `ProcessedAt` (TIMESTAMP, Nullable)

---

## 2. Banco de Dados: `TicketReserver_Payment_DB` (Postgres)

### Tabela: `Payments`
*   `Id` (UUID, Primary Key)
*   `ReservationId` (UUID, Unique Index) -> Garante a Idempotência no Banco
*   `Amount` (NUMERIC(10,2))
*   `Status` (VARCHAR(20)) -> Valores: `Processando`, `Aprovado`, `Recusado`
*   `CreatedAt` (TIMESTAMP)

---

## 3. Banco de Dados: `TicketReserver_Ticketing_DB` (Postgres)

### Tabela: `Tickets`
*   `Id` (UUID, Primary Key)
*   `ReservationId` (UUID)
*   `UserId` (UUID)
*   `QrCodePayload` (TEXT)
*   `IssuedAt` (TIMESTAMP)