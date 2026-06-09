# Pendência — Qualidade de dados na importação de PEPs

- **Aberta em**: 2026-06-06
- **Contexto**: o hub FHIR será o **repositório fiel** da Secretaria de Saúde de Maricá. Ao importar
  prontuários de várias bases de PEP (Salux e futuros; ver [ADR-0014](../adr/0014-importacao-salux-no-backend.md)
  e identidade/proveniência multi-PEP [ADR-0009](../adr/0009-identidade-e-proveniencia-multi-pep.md)),
  precisamos **medir e garantir a qualidade** dos dados — não só importar.

Esta pendência reúne três frentes pedidas pelo usuário.

## 1. Relatório de importação (incongruências / registros saltados)

Hoje a importação **filtra na origem** pacientes sem CPF e médicos sem CPF (cláusula SQL), então
eles nem entram — e isso some do radar. Precisamos **registrar e mostrar** o que foi pulado e por quê:

- **Pacientes** sem **CPF e CNS** (ambos ausentes) — improvável, mas é o caso "não identificável".
- **Médicos/enfermeiros** sem **CPF e conselho** (CRM/COREN) — idem.
- Cada execução (`pep_sincronizacao_execucao`) passa a guardar **contadores de saltados** + uma
  amostra de **incongruências** (motivo por registro), além das falhas que já guarda.
- **Relaxar os filtros**: importar também quem tem **CNS sem CPF** (dedup canônica cai pra CNS).
  Só fica de fora — e é **anotado** — quem não tem CPF **nem** CNS (paciente) / nem conselho (médico).

## 2. Status de saúde da base (nossa **e** a de origem) — no menu de Sincronização

Painel de **diagnóstico** por base, lado a lado:

| Métrica | Origem (Salux/PEP) | Hub (nosso) |
|---|---|---|
| Pacientes total | `COUNT(paciente)` | `Patient?_summary=count` |
| Pacientes sem CPF | `COUNT(... cpf IS NULL)` | Patients sem identifier CPF |
| Pacientes sem CNS | idem | idem |
| Pacientes sem CPF **e** CNS | idem | idem |
| Médicos total / sem CPF / sem conselho | `COUNT(medico ...)` | Practitioners sem CPF/conselho |

- **Origem**: queries `COUNT` via `LeitorOracleHis` (rápidas, read-only) — sem os filtros da importação.
- **Hub**: contagens via API FHIR (`_summary=count`); "sem CPF" exige varrer/contar em código
  (custoso em escala — avaliar `_total` + amostragem, ou um índice no hub).
- Exibir junto: **última importação** (importados, saltados, incongruências) pra fechar o ciclo
  "o que existe na origem × o que entrou no hub × o que ficou de fora".

**Design backend**: `GET /pep-sincronizacao/diagnostico?fonteId=` → DTO com os blocos origem/hub/última.
Estratégia por PEP expõe as `COUNT` (cada PEP sabe suas tabelas). UI: painel "Saúde da base" no menu.

## 3. Bases legadas + deduplicação de documentos (FUTURO — anotado a pedido)

Existem **bases antigas, já desativadas**, possivelmente **migradas parcialmente** para os prontuários
que as sucederam. Ao importar essas bases legadas, o **mesmo documento** pode chegar por duas origens.
Critério de qualidade necessário:

- **Detectar documentos do mesmo paciente com a mesma data/hora** (ex.: `DocumentReference`/`Encounter`
  com `subject` igual e `date`/`period.start` coincidentes) como **prováveis duplicatas** entre bases.
- Estratégias possíveis: marcar como `relatesTo` (replaces/transforms) em vez de duplicar; ou regra de
  precedência (base sucessora vence) com a legada anexada como proveniência; ou fila de revisão manual.
- Hoje os identifiers são namespacados por slug (não colidem), então **não há merge automático** entre
  bases para clínica — de propósito. Esta dedup por (paciente, data/hora) é o passo seguinte.
- Vale também para **medições/observações** repetidas e para reconciliar com dados já presentes.

## 4. Trilha durável de falhas (PARCIALMENTE FEITO — no working tree, **não commitado/deployado**)

**Problema descoberto (2026-06-08)**: as falhas por recurso (ex.: `Condition` com CID-10
cruz-estrela `"H82 *"` rejeitado pelo hub com 400) só eram persistidas em `FalhasJson` **na
conclusão limpa** do run. No ramo de crash (`ORA-50000`, OOM, órfã — o padrão recente) a lista
detalhada **se perdia**; sobrava só a contagem + `mensagem_erro`. O endpoint de status ainda
expõe apenas as **últimas 5** falhas (`TakeLast(5)`), e a lista completa vive só em memória.

**Implementado (aguardando o fim do run atual para commit + deploy):**
- Tabela `smsmarica.pep_sincronizacao_falha` (1 linha por recurso que falhou):
  `execucao_id, fonte_id, fonte_slug, cd_paciente, mensagem, criado_em, resolvido_em`.
  Migration `20260608232753_AddPepSincronizacaoFalha` (só CREATE TABLE — não-destrutiva).
- Sink `RegistradorFalhasPep` (Channel + `IDbContextFactory`): grava **incremental, na hora**,
  concorrência-segura, sobrevive a crash. Plugado no funil único `Falhou(cd, ex)` da estratégia Salux.
- `GET /pep-sincronizacao/falhas?execucaoId=&fonteId=&somentePendentes=true` para consultar
  (com `somentePendentes` → os cds a reprocessar).

**Falta (próximos passos):**
1. **Commit + deploy** (reinicia o backend) — leva junto o fix do CID (`LimparCodigoCid` em
   `SaluxFhirMapper.cs`, também no working tree). Só fazer **depois** que o run atual terminar.
2. **Aplicar a migration** no Postgres.
3. **Fechar o loop de resolução**: expor `CdsPacientes` no `IniciarAsync`/`IniciarImportacaoRequest`
   (o path de reimport direcionado já existe na estratégia, linha ~101) e **marcar `resolvido_em`**
   ao reprocessar com sucesso. Isso transforma a tabela no insumo do **reimport rápido por cd**
   (em vez do "Tudo", que leva o mesmo tempo do run inteiro).

> Nota: as falhas do **run atual** (em andamento por mais dias) **não** entram na tabela — o código
> só vale após o deploy. Mas o reimport idempotente pós-fix reconcilia tudo de qualquer forma.

## Encaminhamento

- **Próximo a construir**: (1) relatório de incongruências na execução + (2) painel de saúde da base
  (origem + hub) no menu. São focados e de alto valor.
- **Futuro**: (3) dedup de documentos legados por (paciente, data/hora) — exige regra de negócio/curadoria.
- Princípio: **medir antes de confiar**. O hub é o repositório fiel; toda importação deve deixar
  rastro do que entrou, do que ficou de fora e por quê.
