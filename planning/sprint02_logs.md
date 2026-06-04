# Sprint 02 Logs - TicketReserver.IO

Este arquivo documenta todas as ações, decisões técnicas e execuções realizadas durante a Sprint 02.

## [01/06/2026] - Planejamento e Início
*   **Status:** Sprint 02 iniciada.
*   **Ações:**
    *   Arquivos de planejamento (`sprint-02.md` e `sprint02_tasks.md`) criados.
    *   Definição do fluxo de trabalho baseado no Style Guide (8 passos).
    *   **Ajuste Arquitetural:** Alterada a estratégia de organização do código para "Uma Solução (.sln) por Componente", garantindo isolamento total e testes com escopo local.
    *   **Ajuste de Granularidade:** Definido que cada camada hexagonal (Domain, Application, Infrastructure, WebApi/Worker) será um projeto .NET individual dentro da solução do componente para reforçar as fronteiras arquiteturais.
    *   **Setup Concluído:** Criada a solução `TR.API.Reservation.sln` com os projetos `Domain`, `Application`, `Infrastructure`, `WebApi` e `UnitTests`. Referências e pacotes NuGet configurados.
    *   **Idempotência Implementada:** Criado `IdempotencyMiddleware` e `RedisIdempotencyService`. Configurado TTL de 24h e padrão de chave `idempotency:reservation:{key}` no Redis conforme ADR 02. Chave de idempotência obrigatória via header `X-Idempotency-Key` para métodos POST.

## [03/06/2026] - Implementação do Core de Reservas
*   **Status:** Task 03 a 07 concluídas.
*   **Ações:**
    *   **Domínio:** Implementada a entidade rica `Reservation` com validações no construtor e regras de estado (Pendente -> Confirmada/Cancelada). Criada interface `IReservationRepository`.
    *   **Ajuste de Modelo:** Adicionado campo `Price` à entidade `Reservation` e à tabela de banco de dados para suportar o contrato do evento de integração.
    *   **Aplicação:** Criados `CreateReservationCommand`, `ReservationOutput` e mappers estáticos. Implementado `CreateReservationUseCase`.
    *   **Infraestrutura:** Implementado `ReservationRepository` utilizando Dapper e `NpgsqlDataSource`. Garantida a atomicidade da persistência (Reserva + Outbox) via transação do banco de dados. Adicionada resiliência com Polly (Retry policy).
    *   **Delivery:** Configurada Minimal API com endpoint `POST /v1/reservations`. Implementado `GlobalExceptionHandler` para converter exceções de domínio em Problem Details (RFC 7807).
    *   **Testes:** Criada suíte de testes unitários cobrindo regras de negócio da entidade e o caso de uso (Mocking do repositório). 100% de sucesso nos 13 testes executados.
