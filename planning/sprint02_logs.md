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
