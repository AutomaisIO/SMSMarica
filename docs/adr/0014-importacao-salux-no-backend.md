# ADR-0014 — Importação de PEPs no backend (canal Oracle persistente, multi-base)

- **Status**: Aceito
- **Data**: 2026-06-06
- **Decisores**: Bernardo (product/eng)
- **Relaciona-se com / estende**: [ADR-0010](./0010-servico-fhir-autonomo.md) (hub FHIR autônomo), [ADR-0009](./0009-identidade-e-proveniencia-multi-pep.md) (identidade/proveniência multi-PEP), [ADR-0011](./0011-modulo-ia-consulta-linguagem-natural.md) (fontes de dados Oracle do módulo IA)

## Contexto

A importação do histórico clínico do Salux (Oracle PROD do HCML) para o hub FHIR era feita por **3 scripts Python** (`Salux/scripts/importar_{medicos,10,atendimentos}_fhir.py`) que falavam com o Oracle via **um `sqlplus.exe` por query**. Cada query reabria processo + reautenticava; sob carga o listener derrubava conexões (`ORA-28547`/`ORA-12170`) e a importação inteira abortava — **extremamente lenta e frágil** (benchmark: pacientes pesados falhavam mesmo com retry).

O backend `SMSMarica.server` tem **VPN ao Oracle** e **já usa `Oracle.ManagedDataAccess.Core`** (módulo IA: `SaluxOracleFonte`, conexão *pooled*, `SET TRANSACTION READ ONLY`, `SqlReadOnlyGuard`). Além disso, as bases de PEP já são cadastradas como **`IaFonte`** (tipo/dialeto/ambiente + conexão cifrada) — e logo haverá **mais de um PEP** (MV, Eco…), não só Salux.

## Decisão

### 1. A importação vive no backend, com canal Oracle único e persistente
Um novo subsistema `SMSMarica.Core/Integracoes/Pep/` abre **UMA** `OracleConnection`, marca `SET TRANSACTION READ ONLY` **uma vez** e reaproveita a conexão para **todas** as queries do run (`LeitorOracleHis`). Queries são **batched** por lote de pacientes (`IN`-list / tuple `IN`-list) — substituem o O(BAAs) do Python. Lê colunas direto do `OracleDataReader`, **sem `JSON_OBJECT`**, o que elimina o `ORA-40474` do charset legado e o fallback de texto-livre do Python.

### 2. Multi-base por seleção + estratégia por tipo de PEP
A importação **seleciona a base** (uma `IaFonte`) e despacha para a **estratégia daquele PEP** (`IEstrategiaImportacaoPep`, resolvida por `TipoFonte`). Hoje só `SaluxImportacaoStrategy` (`TipoFonte.Salux`); tipo sem estratégia → erro tratado. Credenciais/host vêm da `IaFonte` (decifradas via `IProtetorSegredos`) — **não** há config de credenciais nova.

### 3. PROD read-only em três camadas (não-negociável)
A leitura clínica usa a conta **capaz de ler `INFOSAUDE.*`** (a configurada na `IaFonte` — tipicamente SUPERVISOR, pois `salux_obs` só lê `V$`/`DBA_`). A segurança de PROD é garantida por **três camadas**: conta só-leitura + `SET TRANSACTION READ ONLY` na sessão + `SqlReadOnlyGuard` (só `SELECT`/`WITH`/`EXPLAIN`, statement único). Idêntico ao que os scripts Python já faziam (`modo="supervisor"`).

### 4. Job em background + status + cronometragem
O disparo (`POST /pep-sincronizacao/importar`) só **enfileira** (`Channel`, 1 por vez) e responde `202`; o `PepSincronizacaoRunner` (hosted) executa fora do request. Estado vivo em singleton + persistência em `pep_sincronizacao_execucao` (status, tempos por fase, contadores por tipo de recurso, falhas por paciente). `GET /pep-sincronizacao/status` devolve o run vivo ou o último. Permissão **`ModuloPermissao.SincronizacaoPep = 27`** (append-only).

### 5. Modos Completo e Incremental
- **Completo**: escopo **Limitado** (N médicos / N pacientes — fase de teste) ou **Tudo**; flag **apagar-antes** (full refresh do hub). Purga clínica por paciente antes de recriar (idempotente).
- **Incremental**: só registros criados/editados desde a última marca d'água (`pep_sincronizacao_estado` por base). Watermarks: `paciente.DT_ALTERACAO`, `edoc_movimento.DT_INCLUSAO`, BAA por `DT_ATENDIMENTO`. `medico` sem watermark confiável → re-importa só na 1ª vez. Atendimentos novos incrementais valem para **todos** os pacientes Salux já no hub.

### 6. FHIR idêntico ao importador Python
Os construtores (`SaluxFhirMapper`) produzem os **mesmos** campos FHIR R4 nativos + a extension `urn:salux:extras` que o Python gerava, então o caminho de leitura do servidor (`PacienteFhirMapper`/`MedicoFhirMapper`/`AtendimentosService`) continua funcionando sem mudança. Escrita no hub via `IHubFhirEscritor` (POST/PUT/DELETE genérico). Rigor com o padrão R4 mantido (campos obrigatórios + ValueSets válidos).

## Consequências

- **Positivas**: ordens de magnitude mais rápido e robusto (sem startup de processo nem reautenticação por query; sem flood de conexões); operável por uma tela com base selecionável, modos e progresso; extensível a novos PEPs só implementando outra `IEstrategiaImportacaoPep`; cronometragem embutida alimenta a estimativa da importação completa (1,73M BAAs / 360k pacientes / 3,7M eDocs).
- **A vigiar**: a conta clínica tem escrita ampla no schema Salux — as 3 camadas read-only são obrigatórias; watermark de `medico` e detecção de BAAs **editados** (via `LOG_BAA`) ficam como follow-up; importação completa em massa (paralelismo/particionamento) virá depois com base nos tempos medidos.

## Alternativas descartadas

- **Otimizar os scripts Python (held connection no `sqlplus`)**: `sqlplus` não mantém canal nem pooling utilizável; o gargalo é estrutural.
- **Config de credenciais Salux dedicada**: redundante — `IaFonte` já é o registro de bases de PEP.
- **`python-oracledb` thin no on-prem**: barrado pelo verifier de senha `0x939` e pelo client 32-bit (ver `Salux/CLAUDE.md`); o driver gerenciado .NET resolve ambos.
- **Manter a importação on-prem**: o backend já alcança o Oracle por VPN; centralizar simplifica deploy e operação. (Os scripts Python permanecem como ferramenta de discovery/fallback.)
