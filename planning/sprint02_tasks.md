# Sprint 02 Tasks - Core de Reservas (sprint02_tasks.md)

## Task 01: Setup da Solução .NET (Componente Reservation)
- [x] **1.1:** Criar solução `TR.API.Reservation.sln` na pasta `source/TR.API.Reservation/`.
- [x] **1.2:** Criar projeto Class Library `TR.API.Reservation.Domain`.
- [x] **1.3:** Criar projeto Class Library `TR.API.Reservation.Application` (referencia Domain).
- [x] **1.4:** Criar projeto Class Library `TR.API.Reservation.Infrastructure` (referencia Application).
- [x] **1.5:** Criar projeto Web API (Minimal API) `TR.API.Reservation.WebApi` (referencia Application e Infrastructure).
- [x] **1.6:** Configurar `appsettings.json` e adicionar bibliotecas base (Dapper, Redis, etc) nos projetos corretos.

## Task 02: Idempotência de Borda
- [ ] **2.1:** Implementar `IdempotencyMiddleware`.
- [ ] **2.2:** Configurar registro de chaves no Redis com padrão `idempotency:reservation:{key}` e TTL de 24h.

## Task 03: Domínio (Hexagonal Step 1-2)
- [ ] **3.1:** Criar Entidade `Reservation` (Rich Domain Model).
- [ ] **3.2:** Criar interface `IReservationRepository`.

## Task 04: Aplicação (Hexagonal Step 3-4)
- [ ] **4.1:** Criar `CreateReservationCommand` e `ReservationOutput`.
- [ ] **4.2:** Criar mappers estáticos em `ReservationMappings`.
- [ ] **4.3:** Implementar `CreateReservationUseCase`.

## Task 05: Infraestrutura (Hexagonal Step 5)
- [ ] **5.1:** Implementar `ReservationRepository` com Dapper.
- [ ] **5.2:** Implementar lógica de persistência atômica (Reserva + Outbox).

## Task 06: Delivery (Hexagonal Step 7)
- [ ] **6.1:** Configurar Injeção de Dependência no `Program.cs`.
- [ ] **6.2:** Criar endpoint `POST /v1/reservations`.

## Task 07: Testes Unitários (Escopo do Componente)
- [x] **7.1:** Criar projeto `TR.API.Reservation.UnitTests` dentro da mesma solução.
- [ ] **7.2:** Testar regras de negócio da Entidade `Reservation`.
- [ ] **7.3:** Testar Use Case `CreateReservationUseCase` isolando o repositório.
