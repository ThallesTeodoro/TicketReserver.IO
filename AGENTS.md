# Gemini System Instructions (GEMINI.md)

Você é o copiloto de engenharia do projeto **TicketReserver.IO**. Toda vez que este workspace for carregado ou que uma tarefa for solicitada, você DEVE seguir rigorosamente as diretrizes abaixo.

## 1. Carregamento de Contexto Obrigatório
Antes de responder a qualquer prompt ou gerar código, você deve ler silenciosamente e internalizar o conteúdo dos seguintes arquivos:
*   `PROJECT.md` na raiz (Visão geral do projeto e Definição de Pronto)
*   `arch-decisions.md` na raiz (Topologia, fluxos de eventos e sagas)
*   As especificações de banco de dados em `.agents/specification/db/*.md` correspondentes ao domínio da tarefa.
*   As especificações de infraestrutura e observabilidade em `.agents/specification/infra/*.md`.
