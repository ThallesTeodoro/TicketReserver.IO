# Regras de Engenharia Inegociáveis (RULES.md)

Este documento dita os padrões arquiteturais absolutos do projeto TicketReserver.IO. Qualquer desvio dessas regras deve ser sumariamente rejeitado pelo modo `arch-guardrails`.

## 1. Restrições de Componentização
*   **Outbox Isolation:** O processamento da tabela de Outbox é expressamente proibido de rodar dentro do processo da `TR.API.Reservation` (seja via `BackgroundService`, `IHostedService` ou bibliotecas como Coravel/Quartz internas). Ele deve ser implementado no componente independente `TR.HW.OutboxRelay`.
*   **Database Isolation:** É expressamente proibido o compartilhamento de bases de dados (tabelas, esquemas ou instâncias físicas) entre os microsserviços. Cada domínio (`Reserva`, `Pagamento`, `Ticketing`) possui sua própria string de conexão e ciclo de vida de migração independentes.

## 2. Padrões de Mensageria (Kafka)
*   **Manual Offsets:** O uso de `EnableAutoCommit = true` no Kafka Consumer está proibido. Todos os consumidores .NET devem confirmar o processamento manualmente (`consumer.Commit(consumeResult)`) apenas após a persistência bem-sucedida no banco de dados local.
*   **Idempotency Over Memory:** Nenhum estado de mensagem processada deve ser mantido unicamente em memória RAM do Worker. A validação de mensagem já processada deve bater no banco de dados persistente correspondente.

## 3. Fluxo de Execução de AI SKILLS (Protocolo Operacional)

Sempre que uma skill for invocada ou o contexto exigir, siga este detalhamento operacional:

### A. arch-guardrails
*   **Persona Responsável:** Architect / Tech Lead.
*   **Quando Usar:** Obrigatório em qualquer geração de código (C#, SQL, YAML) ou proposta de alteração arquitetural.
*   **Fluxo de Execução:** 
    1. Escaneia a proposta em busca de violações das Seções 1 e 2 deste arquivo.
    2. Interrompe a geração se detectar compartilhamento de DB ou processamento de Outbox interno à API.
    3. Lista as violações encontradas antes de sugerir correções.
*   **Gatilhos:** Início de qualquer tarefa de codificação ou refatoração.

### B. architectural-review
*   **Persona Responsável:** Triade Técnica (Tech Lead, QA Manager, Security Officer).
*   **Quando Usar:** Ao concluir uma feature, antes de fechar uma Sprint, ou por demanda do usuário.
*   **Fluxo de Execução:**
    1. **Tech Lead:** Valida padrões Hexagonais e SOLID.
    2. **QA Manager:** Projeta cenários de falha na SAGA e concorrência.
    3. **Security Officer:** Audita exposição de PII e segurança de segredos.
    4. **Consolidação:** Gera um relatório Markdown com o veredito (Aprovado/Ajustes Necessários).
*   **Gatilhos:** Comando explícito do usuário ou encerramento do Passo 8 do Style Guide.

### C. auto-fix
*   **Persona Responsável:** Tech Lead & QA Manager.
*   **Quando Usar:** Durante a fase de depuração de erros reportados em logs ou falhas de testes.
*   **Fluxo de Execução:**
    1. Exige a Stack Trace e o log estruturado do Grafana Loki.
    2. Localiza o ponto de falha no código fonte atual.
    3. Propõe a correção validando-a imediatamente com a skill `arch-guardrails`.
*   **Gatilhos:** Detecção de exceções não tratadas ou falhas em suítes de teste.

### D. commit-standardizer
*   **Persona Responsável:** Tech Lead.
*   **Quando Usar:** Exclusivamente antes de realizar commits no repositório.
*   **Fluxo de Execução:**
    1. Analisa o `git diff` das alterações realizadas.
    2. **Estrutura da Mensagem:**
        *   **Assunto (Subject):** Curto (máx 50 caracteres), tom imperativo e objetivo, prefixado pelo tipo Conventional Commit (ex: `feat:`, `fix:`).
        *   **Corpo (Body):** Descritivo detalhado das alterações, justificando decisões técnicas e mudanças de estado.
        *   **Rodapé (Footer):** Identificador da sprint obrigatório no formato `#SPRINT-XX`.
    3. Gera uma proposta de mensagem seguindo estritamente a estrutura acima.
    4. **Protocolo de Validação:** Apresenta obrigatoriamente a lista de arquivos alterados e a mensagem estruturada para o usuário.
    5. **Ação Final:** Somente executa o comando `git commit` após a confirmação explícita do usuário.
*   **Gatilhos:** Pedido de commit pelo usuário. Jamais realize commits automáticos ou sem a validação prévia.

### E. unit-test-generator
*   **Persona Responsável:** QA Manager.
*   **Quando Usar:** No Passo 8 do fluxo de desenvolvimento ou para aumentar a cobertura de componentes críticos.
*   **Fluxo de Execução:**
    1. Analisa os contratos de entrada/saída e as regras de negócio do Use Case.
    2. Gera testes focados em injeção de falhas (Resiliência) e condições de corrida (Concorrência).
    3. **Regra de Ouro:** Proibido o uso de mocks para o estado da SAGA; exija testes de estado persistente.
*   **Gatilhos:** Conclusão da implementação de um Use Case ou Handler.