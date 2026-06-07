# Pendência — Importação resumível, de-para (reconciliação) e fila adaptativa à memória

- **Aberta em**: 2026-06-06
- **Relaciona-se com**: [ADR-0014](../adr/0014-importacao-salux-no-backend.md),
  [qualidade-dados-importacao](./qualidade-dados-importacao.md). Base: a importação já é
  **streaming/paginada** (keyset por `cd`, memória constante) — o que habilita o resume.

Três capacidades pedidas para tornar o import de bases grandes robusto e operável.

## 1. Contador + RESUME (retomar de onde parou) — não recomeçar do zero

Hoje, se o import cai no meio (OOM, deploy, queda de túnel), recomeça do zero. Como o import é
**keyset por `cd_paciente DESC`**, o **cursor é o ponto de retomada natural**.

- **Checkpoint**: persistir periodicamente (ex.: a cada bloco / ~10s) na execução: `ultimo_cd_paciente`
  processado + contadores correntes. Exige um persist em background (DbContext próprio, fora da
  thread do job) lendo o `ProgressoImportacao`.
- **Retomar**: opção "continuar" na tela → a nova execução lê o checkpoint da última execução
  incompleta da base e **começa o keyset a partir daquele `cd`** (em vez de `null`).
- **Idempotência**: o upsert é merge (paciente/médico por CPF) e a clínica é purgada por
  paciente+base antes de regravar → reprocessar um bloco repetido **não duplica**. Logo, retomar
  (mesmo reprocessando o último bloco) é seguro.
- **UX**: a tela mostra "linha/cd atual" e permite *Continuar* além de *Iniciar do zero*.

## 2. De-para / reconciliação (varredura: o que falta?)

Função para **comparar origem × hub** e apontar o que está faltando/divergente:

- **Pacientes**: conjunto de `cd_paciente` na origem (Salux) × conjunto no hub (identifier
  `urn:salux:cd_paciente` com o slug da base) → **faltando no hub**, **a mais no hub**.
- **Médicos**: por CPF/conselho.
- **Atendimentos**: por chave do BAA (`urn:salux:baa` = `{slug}:{cd_hospital}-{ano}-{nr}`) × origem.
- Saída: relatório (contagens + amostra dos faltantes) e, opcionalmente, **enfileirar só os faltantes**
  para reimportar (reaproveita o caminho por lista explícita de `cds`/chaves — já existe
  `Opcoes.CdsPacientes`).
- Pesado em escala (milhões de chaves) → fazer por **paginação** dos dois lados e comparação por
  conjuntos (hash), nunca tudo na memória. Casa com o painel de saúde da base.

## 3. Fila adaptativa à memória disponível

Hoje o bloco é fixo (`ChunkPacientes`) e a concorrência é fixa. Evoluir para **adaptativo**:

- Medir memória disponível (ex.: `GC.GetGCMemoryInfo().TotalAvailableMemoryBytes` / RSS do processo;
  ou `/proc/meminfo` no Linux) e **ajustar dinamicamente** o tamanho do bloco e a concorrência —
  alocar mais quando há folga, recuar sob pressão (evita OOM no droplet pequeno).
- "Fila inteligente": back-pressure — se a memória/hub estão sob pressão, reduzir o ritmo; quando
  aliviar, acelerar. Combina com o monitor de saúde.
- Curto prazo, mais simples e robusto: **droplet maior** (≥2-4GB) + bloco/concorrência por
  config. O adaptativo é o passo fino depois.

## Encaminhamento (ordem sugerida)
1. **Resume/checkpoint** (maior dor: "recomeçar é complicado") — barato agora que o import é keyset.
2. **De-para/reconciliação** — fecha o ciclo de qualidade (o que entrou × o que falta).
3. **Fila adaptativa à memória** — refinamento; antes disso, redimensionar o droplet.
