# Sprint 02 - Core de Reservas & Idempotência de Borda (sprint-02.md)

## Objetivo da Sprint
Implementar o fluxo principal de criação de reservas no microsserviço `TR.API.Reservation`, garantindo a integridade dos dados através do padrão Outbox e proteção contra duplicidade via middleware de idempotência no Redis.

## Tarefas Detalhadas

### 1. Setup da Solução e Projeto
*   **Descrição:** Criar a solução .NET específica para o componente (`TR.API.Reservation.sln`). Estruturar o componente em projetos .NET distintos para cada camada da Arquitetura Hexagonal: `TR.API.Reservation.Domain`, `TR.API.Reservation.Application`, `TR.API.Reservation.Infrastructure` e `TR.API.Reservation.WebApi`. Os projetos de teste devem seguir o mesmo padrão.
*   **DoD:** Solução do componente compilando com todos os projetos referenciados corretamente e API respondendo a um health check básico.

### 2. Middleware de Idempotência (Redis)
*   **Descrição:** Implementar um middleware que intercepta requisições POST, valida o header `X-Idempotency-Key` e utiliza o Redis (`SET NX`) para garantir que a mesma chave não seja processada simultaneamente ou duplicada.
*   **DoD:** Teste funcional simulando duas requisições idênticas, onde a segunda deve retornar um status de conflito ou o resultado da primeira.

### 3. Implementação do Domínio (Passos 1 e 2 do Style Guide)
*   **Descrição:** Criar a entidade rica `Reservation` com validações e a interface `IReservationRepository`.
*   **DoD:** Entidade testada unitariamente quanto às suas regras de estado (Ex: não permitir reserva sem UserId ou EventId).

### 4. Implementação de Application e Mappers (Passos 3 e 4 do Style Guide)
*   **Descrição:** Criar `CreateReservationCommand`, `ReservationOutput` e os mappers estáticos. Implementar o Use Case `CreateReservationHandler`.
*   **DoD:** Lógica de negócio isolada e mapeamentos validados.

### 5. Adaptador de Infraestrutura - Dapper & Outbox (Passo 5 do Style Guide)
*   **Descrição:** Implementar o `ReservationRepository` usando Dapper. A persistência deve ser atômica, salvando a `Reservation` e a `OutboxMessage` na mesma transação.
*   **DoD:** Registro inserido com sucesso nas tabelas `Reservations` e `OutboxMessages` no Postgres.

### 6. Delivery - Minimal API (Passo 7 do Style Guide)
*   **Descrição:** Configurar o endpoint `POST /v1/reservations` e realizar a injeção de dependências.
*   **DoD:** Endpoint funcional recebendo JSON e retornando 201 Created.

### 7. Validação de Unidade (Passo 8 do Style Guide)
*   **Descrição:** Suite de testes completa cobrindo os cenários de sucesso, falhas de validação e concorrência (simulada).
*   **DoD:** Cobertura de testes conforme definido no Style Guide.
