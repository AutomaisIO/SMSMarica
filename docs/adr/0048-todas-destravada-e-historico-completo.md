# ADR-0048 — Aba "Todas" destravada + histórico completo do paciente na thread

**Status:** aceito · **Data:** 2026-08-25
**Supersede parcial de:** [ADR-0047](./0047-posse-de-conversa-filas.md) (posse de conversa — filas
disjuntas e posse como trava de acesso).
**Relacionado:** [ADR-0044](./0044-app-meta-unico-e-roteador-whatsapp.md) (roteador de WhatsApp),
[ADR-0043](./0043-instancia-por-municipio.md) (multi-tenant do módulo continua sendo *unidade*).

## Contexto

O ADR-0047 fechou a Central de Atendimento em torno de **posse como trava de acesso**: listas
disjuntas (Minhas × Fila), aba **Todas** exclusiva de quem tem `ConversasSupervisao`, e **404**
ao ler/abrir qualquer conversa fora do escopo (dono ∨ unidade vinculada ∨ triagem geral ∨
supervisão). E deixou registrado, como *gap conhecido*, que as **mensagens automáticas**
(confirmação de agendamento, laudo etc.) saem por `WhatsAppCliente` **sem** `conversa_id` e por
isso **não apareciam** na thread.

Na operação isso doeu: um atendente que não é da unidade/dono não conseguia **ver** o que já foi
falado com o cidadão, e mesmo dentro de uma thread faltava o histórico automático e o que foi
trocado em outras conversas/unidades com o **mesmo número**. Decisão do gestor (admin): liberar a
visibilidade **sem restrição, para depois rever**.

## Decisão

**1. Aba "Todas" destravada para todo operador do módulo Conversas.** Sai a exigência de
`ConversasSupervisao`: qualquer usuário com o módulo vê todas as conversas em `Todas`. `Minhas` e
`Fila` **não mudam** (seguem disjuntas e recortadas por posse/unidade).

**2. Leitura sem trava de escopo.** Abrir o cabeçalho (`ObterAsync`), ler as mensagens
(`ObterMensagensAsync`) e listar os pacientes do telefone (`ListarPacientesDoTelefoneAsync`) não
dependem mais de posse — qualquer operador lê qualquer conversa. O 404 fora de escopo, que era
trava de **acesso**, passa a ser trava só de **ação**.

**3. A posse continua trava das AÇÕES.** Responder (`EnviarTextoAsync`), marcar lida
(`MarcarLidaAsync`), assumir/devolver/encaminhar/transferir seguem gated por
dono ∨ unidade ∨ triagem ∨ supervisão — conversa fora do escopo ainda dá **404** para agir. Ou
seja: **visibilidade total, controle de ação intacto**. As mecânicas de claim e concorrência do
ADR-0047 ficam idênticas.

**4. A thread mostra o histórico COMPLETO do interlocutor.** `ObterMensagensAsync` deixa de
filtrar por `conversa_id` e passa a trazer todas as mensagens do **mesmo telefone canônico**
(qualquer conversa, qualquer unidade/operador) **+ as automáticas** (`conversa_id` nulo) **+ as do
mesmo `paciente_id`** em outros números — em ordem cronológica, teto nas 500 mais recentes. O
telefone canônico é a mesma chave em toda origem (`Canonizar` no inbound; `NormalizarTelefone`, o
mesmo algoritmo, no outbound), então o casamento é exato.

## Consequências

- **PII mais exposta, por decisão explícita.** Todo operador do módulo passa a ler a conversa de
  qualquer paciente de qualquer unidade, e a thread agrega **todo o histórico do número** —
  inclusive mensagens automáticas e de **outros pacientes que compartilham o telefone** (celular
  de família). É intencional e temporário ("para depois rever"); um ADR futuro pode reintroduzir
  recorte (ex.: `Todas` só-leitura mas mascarada, ou trilha de auditoria de quem abriu o quê).
- **Sem migration.** Só lógica de consulta e visibilidade; nenhum schema muda.
- **SignalR:** o roteamento por grupos do ADR-0047 não muda — ele governa *notificação em tempo
  real* (quem recebe o ping de conversa nova/mensagem), não *quem pode abrir*. A thread aberta já
  assina `conversa:{id}` explicitamente. A aba `Todas` atualiza pelo poll de fallback (30s) como
  antes.
- **Resposta fora de escopo:** um operador pode abrir, em `Todas`, conversa de outra unidade e o
  compositor continua visível; ao tentar enviar, o backend recusa com 404 (posse). É o
  comportamento correto do ADR-0047, agora possível de encostar pela leitura destravada — melhoria
  de UX (desabilitar o compositor fora de escopo) fica registrada para frente própria.
- **Reversão:** basta restaurar o gate de leitura (`Todas when supervisor` + `NoEscopoAsync` nos
  caminhos de leitura) e o filtro por `conversa_id` em `ObterMensagensAsync`.
