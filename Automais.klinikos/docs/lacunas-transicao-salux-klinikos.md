# Lacunas da transição Salux → Klinikos no hub FHIR (medido 16/09/2026)

> Tudo abaixo foi **medido**: os volumes do hub vêm de `SELECT` no Postgres de produção
> (`fhir.encounter`, read-only) e os volumes do Klinikos vêm dos relatórios web deste
> laboratório (instância do Conde, `unid_codigo` 0005). Nenhum dado de paciente entrou no
> documento. **Nada foi escrito no hub** — isto é diagnóstico, não carga.

## 1. Retrato do hub por origem (`fhir.encounter.meta.source`)

| Origem (`meta.source`) | Encounters | Primeiro | Último |
|---|---:|---|---|
| `…/salux/salux-hcml` | 1.160.635 | 2021-02-09 | **2026-08-21** |
| `…/klinikos/upa24h-marica-sqlserver` | 196.275 | 2025-04-07 | 2026-09-16 (hoje) |
| `…/klinikos/santarita-marica-sqlserver` | 123.626 | 2006-07-06 | 2026-09-16 (hoje) |
| `…/salux` (legado, quase vazio) | 120 | 2021-01-07 | 2024-09-16 |

A base Salux `salux-hcml` serve **3 hospitais** (o identifier `urn:salux:baa` = `salux-hcml:<CD_HOSPITAL>-<ano>-<n>`): **1 = Conde (HMCML)**, **2 = UPA Inoã**, **3 = Santa Rita**.
Quebrando por hospital:

| CD_HOSPITAL | Unidade | Encounters (Salux) | Último no Salux |
|---|---|---:|---|
| 1 | Conde / HMCML | 721.939 | **2026-08-21** (real: 06–07/08) |
| 2 | UPA Inoã | 268.145 | 2025-04-07 |
| 3 | Santa Rita | 163.620 | 2025-04-08 |

## 2. UPA Inoã e Santa Rita — **sem lacuna** (corrige memória antiga)

A migração aconteceu em **07–08/04/2025** e o conector Klinikos **pegou no mesmo dia**:

- UPA Inoã: Salux termina `2025-04-07 16:13`, Klinikos `upa24h` começa `2025-04-07 13:52`.
- Santa Rita: Salux termina `2025-04-08 06:48`, Klinikos `santarita` já vinha de 2006.

E os dois conectores Klinikos estão **correntes até hoje** (último encounter 16/09/2026). Ou
seja, o motor do Klinikos das UPAs está **LIGADO** — a nota de memória "hub cego nas UPAs desde
abr/2025 / motor OFF" está **desatualizada** e foi corrigida.

> **Ponta a investigar (não é lacuna, é possível DUPLICAÇÃO):** Santa Rita tem no Klinikos
> história desde **2006**, enquanto o Salux tem Santa Rita de 2021 a abr/2025. O intervalo
> 2021–2025 pode estar nas DUAS origens. Isso é dedup por identidade, não buraco de dado —
> tratar com o `UpsertCanonicoPep` (âncora CPF/CNS), não com nova carga.

## 3. Conde (HMCML) — **a lacuna real, e ela cresce**

Sequência medida:

| Data | Salux-Conde no hub | Klinikos-Conde (web, relatório 407) |
|---|---|---|
| até 06/08/2026 | ~500–670 boletins/dia (normal) | **nada** (02–03/08 "Nenhum registro") |
| **04/08/2026** | ainda ativo | **começa** (cutover) |
| 05–06/08 | ativo (overlap) | ativo (759+/dia em 15/08) |
| 07/08/2026 | **135** (dia parcial — colapsa) | ativo |
| 08/08 → 20/08 | **0** | ativo |
| 21/08/2026 | **1** registro solto (straggler) | ativo |
| 22/08 → hoje | **0** | ativo (922 em 05/09, 1.317 em 15/09) |

**Conclusões:**
- **Conde migrou para o Klinikos em 04/08/2026** (primeiro boletim em `klinikosconde`).
- **O sync do Salux-Conde morreu em 07/08/2026** (colapso de 659→135→0; o "21/08" é lixo).
- **Desde ~08/08/2026 o hub não tem NENHUM atendimento do Conde.** São ~40 dias e contando, a
  **~700–1.300 boletins/dia** → estimativa de **~35.000+ boletins do Conde fora do hub**, mais
  toda a clínica pendurada neles (evolução, CID, prescrição, sinais vitais, desfecho).
- **Não existe conector para o Conde-Klinikos.** O `KlinikosImportacaoStrategy` só conhece os
  slugs SQL `upa24h-marica-sqlserver` e `santarita-marica-sqlserver`. `klinikosconde` é outro
  servidor/base (o operador confirmou: os três são servers e bases distintos, só sincronizam
  login e cadastro) e **ninguém o lê**. A lacuna é estrutural, não um ponteiro travado.

## 4. Como preencher o buraco do Conde — opções (nenhuma executada)

O que o hub precisa por boletim do Conde: Encounter (chegada/desfecho), Condition (CID),
DocumentReference (narrativa), MedicationRequest (prescrição), Observation (sinais vitais) —
o mesmo conjunto que o conector SQL das UPAs monta.

| Caminho | O que entrega | Custo / risco |
|---|---|---|
| **A. Conector SQL do Conde** (novo slug `klinikosconde-*`, se houver rota de banco) | Tudo, com rowversion e a mesma qualidade das UPAs | Precisa de acesso ao banco do Conde — vai **contra** a direção de sair do banco; pode não existir rota |
| **B. Ingestão pelos relatórios web** (este laboratório) | Boletim, chegada, CID (815), classificação (667/526), desfecho (65), observação (630), cadastro (21) — tudo por XLS/1 GET | Alinhado à direção "como usuário"; **lossy**: narrativa médica, prescrição e sinais vitais **não têm relatório nominal** no menu (só tela por boletim). Ver `inventario-consultas-atuais.md §2` |
| **C. Pedir exportação ao fornecedor** (Eco Sistemas) | O que faltar de B | Depende de terceiro |

**Recomendação:** o buraco tem duas camadas. (1) A **espinha** (Encounter+chegada+CID+desfecho+
classificação+cadastro) é backfillável **já** pelos relatórios web do Conde, dia a dia, sem tocar
banco — é o que dá o Conde de volta ao Painel do Secretário e ao hub no nível de jornada.
(2) A **profundidade** (narrativa, prescrição, sinais vitais por boletim) não sai por relatório
e exige decisão: tela por boletim (caro), conector SQL do Conde (contra a direção) ou exportação
do fornecedor.

## 5. Ordem sugerida (cada passo é ação de produção — só com OK explícito)

1. **Confirmar** com o operador se existe rota de banco para o Conde-Klinikos (decide A vs B).
2. **Espinha do Conde no hub via web** (opção B): backfill 08/08→hoje, 1 dia por chamada,
   XLS, madrugada; depois incremento diário. Verificar a carga **pela API do hub** (regra:
   carga em massa se confere pela API, não pelo "salvou"). Idempotência por `spa_codigo`
   (identifier `urn:klinikos:boletim`), como as UPAs.
3. **Conde no Painel do Secretário** em paralelo (não depende do hub) — relatórios 629/631/630/
   407/56/57/526/667.
4. **Profundidade**: decidir o caminho da narrativa/prescrição/vitais.
5. Só então avaliar desligar o agente SQL das UPAs (o motor delas está LIGADO e corrente —
   não desligar antes de a web cobrir o mesmo por ≥ 1 semana com números conferidos).

> **Bloqueio deliberado:** este documento é diagnóstico. Popular o hub é escrita em produção e
> carga em massa — não roda sem OK por ação ([[feedback_producao_confirmar_antes]],
> [[feedback_carga_massa_verificar_pela_api]]).
