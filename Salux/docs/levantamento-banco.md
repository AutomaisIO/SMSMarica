# Levantamento do banco Salux

Compilação de descobertas via observação read-only do Oracle PROD do Salux HIS. Schema lógico: `INFOSAUDE` (owner real das tabelas) acessado via `SYBSA` (schema-owner com synonyms públicos).

**Data**: 2026-05-27 · **Conta de leitura**: `salux_obs` (SELECT_CATALOG_ROLE) · **Banco**: Oracle 12c em `10.50.0.18:1521/ORASX01`.

---

## 1. Stack do Salux desktop

Identificado via `EDOC_TIPO_ITEM.picture_name` (controles UI):
- **PowerBuilder** (controles `DropDownListBox!`, `RichTextEdit!`, `MonthCalendar!`, `RadioButton!`, etc).
- Executáveis: `portal.exe` (geral), `suprim.exe` (suprimentos/farmácia), `centro.exe` (centro cirúrgico), `fatsusii.exe` (faturamento SUS), `sadt.exe`, `cadastro.exe`, `p_senha.exe` (chamada de senha).
- Conta Oracle do app: **`SYBSA`** (todas as queries `parsing_schema_name = 'SYBSA'`).
- ~190 sessões `portal.exe` simultâneas em 93 máquinas distintas no momento da coleta.

## 2. Modelo de eDoc — o coração do sistema

**Tudo que é "documento" no Salux é um eDoc**: Boletim de Atendimento (BAA), Ficha de Internação (FIA), evolução, receituário, atestado, encaminhamento, escala clínica, sumário de óbito, SINAN, AIH, etc. **333 modelos** ativos, organizados em 6 categorias e 51 grupos.

### 2.1 Tabelas-chave do eDoc

| Tabela | Linhas | Função |
|---|---:|---|
| `EDOC_CATEGORIA` | 6 | Categorias macro (Urgência, Eletivo, Médicos, Enfermagem, Multi, Administrativos) |
| `EDOC_GRUPO_MODELO` | 51 | Subgrupos (por unidade/especialidade: HMCML, UPA Inoã, Sta Rita, Maternidade, COVID, SINAN, ...) |
| `EDOC_MODELO` | 333 | **Os 333 tipos de documento** (templates) |
| `EDOC_DOCUMENTO` | 1.038 | Versões/instâncias dos templates |
| `EDOC_DOCUMENTO_ITEM` | 36.684 | Definição dos campos de cada documento |
| `EDOC_TIPO_ITEM` | 18 | Tipos de widget (Data, CheckBox, DropDown, ListBox, Texto, RichText, Numérico, Botão, ...) |
| `EDOC_ITEM` | 4.272 | Catálogo global de campos reutilizáveis |
| `EDOC_ITEM_SUBITEM` | 2.061 | Subcampos / opções compostas |
| `EDOC_ITEM_VALORES` | 4.110 | Valores predefinidos (opções de listbox) |
| `EDOC_GRUPO_ITEM` | 36 | Grupos de campos |
| `EDOC_REGRA` | 324 | Regras de validação |
| `EDOC_REGRA_CONDICAO` | 837 | Condições das regras |
| `EDOC_REGRA_FORMULA` | 10 | Fórmulas |
| `EDOC_SCRIPT` | 153 | Scripts dinâmicos vinculados |
| `EDOC_CABECALHO` | 26 | Cabeçalhos reutilizáveis |
| `EDOC_RODAPE` | 1 | Rodapé |
| `EDOC_PERFIL` | 55 | Perfis de acesso |
| `EDOC_USUARIO_X_PERFIL` | 3.187 | Atribuição perfil ↔ usuário |
| `EDOC_CONFIG_PERFIL_X_MODELO` | 4.041 | Permissão perfil → modelo |
| `EDOC_CONFIG_USUARIO_X_MODELO` | 470.993 | Permissão usuário → modelo |
| `EDOC_ESCALAS` | 7 | Escalas clínicas (Glasgow, Braden, Morse, Fugulin, Maddox, ...) |
| `EDOC_MOVIMENTO` | **5.034.386** | **CADA documento preenchido** = 1 linha aqui |
| `EDOC_MOVIMENTO_ITEM` | **166.090.964** | **CADA campo respondido** = 1 linha aqui (EAV) |
| `EDOC_MOVIMENTO_ITEM_LISTBOX` | 1.667.277 | Respostas de listbox |
| `EDOC_MOVIMENTO_LOG` | 16.417.763 | Auditoria de alterações |

### 2.2 Modelo de dados do eDoc (relacionamentos lógicos)

```
EDOC_CATEGORIA (1) ─┬─< EDOC_MODELO (333)
                    │     │
                    │     │ 1:1 com
                    │     ▼
                    │   EDOC_DOCUMENTO (template versionado)
                    │     │
                    │     │ N:1
                    │     ▼
                    │   EDOC_DOCUMENTO_ITEM ── refere ── EDOC_ITEM
                    │                                       │
                    │                                       │ tipo
                    │                                       ▼
                    │                                   EDOC_TIPO_ITEM
                    │
EDOC_GRUPO_MODELO (51) ─< EDOC_MODELO

EDOC_MOVIMENTO ─── PK (CD_HOSPITAL, ANO_MOVIMENTO, ID_MOVIMENTO)
   │   ├── CD_MODELO ── EDOC_MODELO
   │   ├── CD_PACIENTE
   │   ├── (FIA_CD_HOSPITAL, DT_ANO_FIA, NR_FIA)       ← vínculo internação
   │   ├── (BAA_CD_HOSPITAL, DT_ANO_BAA, NR_BAA)       ← vínculo ambulatório
   │   ├── CD_UNIDADE
   │   ├── DT_INCLUSAO / CD_FUNCIONARIO_INC
   │   ├── IN_STATUS  ('parcial', 'definitivo', ...)
   │   ├── IN_ASS_DIGITAL / HASH_ASSINATURA_DIGITAL / DOC_ASSINADO (BLOB)
   │   └── ASS_ELE_PAC (BLOB)                          ← assinatura eletrônica paciente
   │
   └─< EDOC_MOVIMENTO_ITEM (166M de linhas)
         ├── PK: (CD_HOSPITAL, ANO_MOVIMENTO, ID_MOVIMENTO, SEQ_DOCTO, CD_MODELO, CD_DOCUMENTO, CD_ITEM)
         ├── DS_RESPOSTA VARCHAR2(4000)   ← O VALOR PREENCHIDO (string genérica)
         └── CD_ITEM_GRUPO
```

**Insight**: EDOC é um **EAV** (Entity-Attribute-Value) — toda resposta vira `DS_RESPOSTA` em VARCHAR2(4000). Mesmo data, número, booleano. Tipagem é semântica (definida pelo `EDOC_TIPO_ITEM` do `EDOC_ITEM`) mas armazenamento é genérico.

### 2.3 As 6 categorias de eDoc

| CD | Descrição |
|---:|---|
| 1 | 1º Atendimento Médico (Urgência) |
| 2 | 1º Atendimento Médico (Eletivo) |
| 3 | Registros Médicos |
| 4 | Registros de Enfermagem |
| 5 | Registros Multidisciplinar |
| 6 | Registros Administrativos |

### 2.4 Os 18 tipos de widget (`EDOC_TIPO_ITEM`)

| CD | DS | Picture (PB control) |
|---:|---|---|
| 1 | Raiz da árvore | Library! |
| 2 | Agrupador de Itens | GroupBox! |
| 3 | Data | MonthCalendar! |
| 4 | CheckBox | CheckBox! |
| 5 | RadioButton | RadioButton! |
| 6 | DropDown | DropDownListBox! |
| 7 | Texto (1 linha) | RichTextEdit! |
| 8 | Hora | ShowWatch! |
| 9 | ListBox | ListBox! |
| 10 | Figura | Picture5! |
| 11 | Pergunta | Help! |
| 12 | Texto (Multilinha) | ToDoList! |
| 13 | Botão | CommandButton! |
| 14 | Numérico | Custom023! |
| 15 | Título fixo | DataManip! |
| 16 | Data/Hora | DatePicker! |
| 17 | Numérico (casas decimais) | ComputeToday! |
| 18 | Agrupador de Itens (Banco de Dados) | DatabaseProfile5! |

### 2.5 Top 30 modelos mais usados (último ano)

| CD | Modelo | Grupo | Usos/ano |
|---:|---|---|---:|
| 10036 | Boletim de Atendimento de Urgência - HMCML | Médico HMCML | 193.563 |
| 10037 | Receituário Médico Simples - HMCML | Médico HMCML | 123.722 |
| 10046 | Atestado Médico - HMCML | Médico HMCML | 49.800 |
| 10146 | Escala de Braden - HMCML | Escalas Enfermagem | 41.766 |
| 10051 | Escala de Fugulin | Escalas Enfermagem | 41.525 |
| 10145 | Escala de Morse - HMCML | Escalas Enfermagem | 39.748 |
| 10050 | Escala de Maddox | Escalas Enfermagem | 36.783 |
| 10029 | Escala de Glasgow | Escalas Enfermagem | 34.447 |
| 10038 | Receituário de Controle Especial - HMCML | Médico HMCML | 33.241 |
| 10225 | Encaminhamento Para Atenção Primária | Médico HMCML | 15.860 |
| 10209 | Pesquisa de satisfação - Ambulatório | Humanização/Qualidade | 14.848 |
| 10055 | Solicitação de Parecer | Médico HMCML | 12.266 |
| 10232 | BA Urgência Obstetrícia | Médico Obstetra | 12.059 |
| 10172 | Evolução Diária do Enfermeiro | Enfermagem HMCML | 11.861 |
| 10043 | Classificação de Risco Maternidade | Enfermagem HMCML | 11.181 |
| 10282 | SAE - 2º ETAPA - Diagnóstico de Enfermagem | Enfermagem HMCML | 11.033 |
| 10306 | Atestado Pediatria | Médico HMCML | 7.718 |
| 10163 | Ficha de Bloqueio de Entrada | Enfermagem HMCML | 6.737 |
| 10281 | Evolução de Lesão Diária - Enfermeiro | Enfermagem HMCML | 6.046 |
| 10039 | Laudo AIH | Médico HMCML | 5.794 |
| 10047 | Resumo de Alta | Médico HMCML | 3.987 |
| 10208 | Pesquisa satisfação - Internação | Humanização | 3.986 |
| 10280 | SAE - 1ª ETAPA - Histórico de Enfermagem | Enfermagem HMCML | 2.713 |
| 10277 | Sumário de Óbito - HMCML | Médico HMCML | (menos comum) |
| 10283 | Ficha de cuidados paliativos | Médico HMCML | — |
| 10222 | SINAN - Ficha de notificação | SINAN HMCML | — |

### 2.6 Os 51 grupos de modelo (organização por unidade/área)

Padrão SX (1), Enfermagem (2), Odontologia (3), Termos de Consentimento (4), Grupo Importados (9999), Customizados (10000), Médico PA Sta Rita (10001), Médico UPA Inoã (10002), Serviço Social UPA Inoã (10003), Contingência UPA (10004), Contingência Sta Rita (10005), Médico HMCML (10006), Enfermagem HMCML (10007), Uso Todos os Setores (10008), Escalas Enfermagem HMCML (10009), Contingência HMCML (10010), Escalas de Enfermagem UPA (10011), Nutrição (10012), Fisioterapia HMCML (10013), Enfermeiro UPA (10014), Técnico de Enfermagem UPA (10015), Enfermagem Sta Rita (10016), Serviço Social HMCML (10018), Psicologia HMCML (10019), Protocolo Médico (10020), Protocolo Médico Sta Rita (10021), Protocolo Médico Upa Inoã (10022), Centro Cirurgico (10023), Fonoaudiologia (10024), Impressos Médicos (10025), Banco de Sangue (10026), CCIH (10027), Maternidade (10028), Enfermagem Técnico (10029), Protocolos (10030), Médico Maternidade (10031), Médico COVID (10032), Enfermagem Maternidade (10033), Humanização/Qualidade (10034), SAD (10035 — Serviço de Atenção Domiciliar), Odontologia UPA (10036), SINAN HMCML (10037), Ouvidoria UPA (10038), Ouvidoria Sta Rita (10039), Médico Obstetra (10040), Fisioterapia UPA Inoã (10041), Laboratório (10042), Formulários (10043), SINAN UPA Inoa (10045), SINAN Sta Rita (10046), LAUDOS HMCML (10047).

**3 unidades** principais (HMCML = Hospital Maternal Conde Modesto Leal, UPA Inoã, Posto Sta Rita).

---

## 3. Modelo clínico — entidades "duras"

Apesar do eDoc dominar, há tabelas tradicionais que carregam o "esqueleto" do atendimento:

### 3.1 Identificação e atendimento

| Tabela | Linhas | Função |
|---|---:|---|
| `PACIENTE` | 366.832 | 137 colunas — identificação completa, endereço, contatos, filiação, prontuários SGH/CEM |
| `FIA` | (muitas) | 128 colunas — **Ficha de Internação Ambulatorial** (internação completa) |
| `BAA` | (muitas) | 138 colunas — **Boletim de Atendimento Ambulatorial** (urgência/eletivo) |
| `ATENDIMENTO_PACIENTE` | 855 | 7 colunas — texto livre LONG (`TX_ATENDIMENTO`) — possivelmente histórico antigo |
| `FICHA_PACIENTE` | — | 13 colunas — metadado de ficha, FK pra FIA e/ou BAA |

**Padrão de chave**: FIA e BAA usam chave composta `(CD_HOSPITAL, DT_ANO_<FIA|BAA>, NR_<FIA|BAA>)`. NR é sequencial por ano por hospital.

**Vínculo de paciente unificado**: `CD_PACIENTE_UNIFICADO` em FIA/BAA — paciente pode ser unificado (fusão de duplicidades). Sempre usar `NVL(cd_paciente_unificado, cd_paciente)`.

### 3.2 Evolução e prescrição

| Tabela | Linhas | Função |
|---|---:|---|
| `EVOLUCAO_PACIENTE_FIA` | 1.374.954 | Evolução clínica (texto LONG + BLOB) em internação |
| `EVOLUCAO_PACIENTE_BAA` | 530.236 | Evolução em atendimento ambulatorial |
| `PLANO_TERAPEUTICO_MEDICO` | — | Hipótese diagnóstica + investigação/conduta (VARCHAR2 4000 cada) |
| `PRESCRICAO_FIA` | 752.140 | Cabeçalhos de prescrição em internação (39 colunas) |
| `PRESCRICAO_FIA_CONDUTA` | 2.511.874 | Itens de prescrição com horário/intervalo |
| `PRESCRICAO_FIA_INTERVENCAO` | 193.723 | Intervenções de enfermagem |
| `PRESCRICAO_FIA_REQUISICAO` | 426.560 | Requisições geradas pela prescrição |
| `PRESCRICAO_BAA` | 1.800.309 | Prescrição ambulatorial |
| `PRESCRICAO_BAA_CONDUTA` | 242.327 | Condutas BAA |
| `PRESCRICAO_BAA_REQUISICAO` | 971.429 | Requisições BAA |
| `MODELO_PRESCRICAO` | 2.562 | Templates de prescrição |
| `MODELO_PRESCRICAO_PROCED` | 19.687 | Procedimentos em modelos |

**Padrão de chave da prescrição**: `(CD_HOSPITAL, DT_ANO_FIA, NR_FIA, NR_PRESCRICAO)`. Tipo da prescrição via `ID_TIPO_PRESCRICAO` (`L`=livre/multiprofissional? `M`=médica? `E`=enfermagem? — confirmar via domínio).

### 3.3 Sinais vitais

`SINAIS_VITAIS` (115 colunas) — uma linha por aferição:
- PK: `(ID_TIPO, CD_HOSPITAL, DT_ANO_FIA_BAA, NR_FIA_BAA, DTHR_VISITA, CD_FUNCIONARIO)`
- `ID_TIPO` = `F` (FIA/internação) ou `B` (BAA/ambulatório)
- Antropometria: PESO, ALTURA, PERIMETRO_CEFALICO, CIRCUNF_ABDOMINAL
- Vitais: PA_ALTA, PA_BAIXA, PA_MEDIA, FREQ_CARDIO, RESPIRACAO, TEMP_AUX, TEMP_INC
- Saturação/oxigenação: SATURACAO_OXIGENIO, PRESSAO_VENO_CENTRAL, PRESSAO_EXPIRATORIA, PRESSAO_INSPIRATORIA, FIO2, PSV, VC
- Glicemia: GLICOSE
- Balanço hídrico: VIA_ORAL, SONDA, SORO, MED_SORO, PUSH, HEMODERIVADOS, NUTRI_PARETERAL, URINA, FEZES, RESIDUO_GASTRICO, DRENO, VOMITO

### 3.4 Dimensões (lookup)

| Tabela | Linhas | Função |
|---|---:|---|
| `MEDICO` | — | médicos (PK `CD_MEDICO`) |
| `FUNCIONARIO` | — | funcionários (PK `CD_FUNCIONARIO` VARCHAR2 8) |
| `CLINICA` | — | clínicas / unidades funcionais |
| `ESPECIALIDADE` | — | especialidades médicas |
| `CID` (presumido) | — | catálogo CID-10 |
| `PLANO_SAUDE` + `TIPO_PLANO` | — | convênios |
| `CIDADE` (11 col) | — | UF + CIDADE (PK composto) — `DS_CIDADE` é o nome |
| `SETOR_FLUXO` | 73 | setores/áreas (a "lista por setor") |
| `SETOR_FLUXO_X_FUNCIONARIO` | 36.777 | atribuição |
| `SETOR_FLUXO_X_HOSPITAL` | 165 | setores por hospital |
| `TIPO_DOCUMENTO` | 11 | CPF, RG, CN, etc — documentos de identificação do paciente |
| `MOTIVO_ATENDIMENTO` / `TIPO_ATENDIMENTO` | 20 / 11 | classificações |

---

## 4. Suporte clínico: exames, laudos, requisições, triagem

| Tabela | Linhas | Função |
|---|---:|---|
| `SOL_EXAME` | 1.105.204 | solicitações de exame |
| `SOL_EXAME_ITEM` | 3.845.942 | itens das solicitações |
| `SOL_EXAME_MESTRE` | 112 | catálogo de tipos |
| `SOL_EXAME_ITEM_OBS` | 302.517 | observações nos itens |
| `PRESCRICAO_ORIGINAL_EXAME` | 1.291.573 | vínculo prescrição → exame (FIA) |
| `PRESCRICAO_ORIGINAL_EXAME_BAA` | 2.334.874 | (BAA) |
| `LAUDO` | 43.204 | laudos médicos |
| `COMPOSICAO_LAUDO` | 40.787 | composição/itens do laudo |
| `LAUDO_CONSOLIDADO` | 6 | laudos consolidados |
| `LAUDO_AUT_SUS` | 2 | autorização SUS |
| `REQUISICAO` | 3.464.060 | requisições gerais |
| `REQUISICAO_ITEM` | 10.205.154 | itens |
| `TRIAGEM` | 3.023.874 | classificação de risco / triagem |
| `CHAMADA_PACIENTE` | 4.153.822 | chamadas em painel |

---

## 5. Rastreabilidade / auditoria

| Tabela | Linhas | Função |
|---|---:|---|
| `RASTREABILIDADE_ATENDIMENTO` | 21.910.490 | log de atendimento |
| `RAST_ASSISTENCIAL` | 12.979.369 | log assistencial |
| `RASTREABILIDADE_EXAME` | 8.297.061 | log de exame |
| `RASTREABILIDADE_PAC_BEN` | 8.010.907 | log paciente↔benefício |
| `LOG_BAA` | 3.316.564 | log BAA |
| `EDOC_MOVIMENTO_LOG` | 16.417.763 | log de movimento eDoc |
| `FUNCIONARIO_PROCESSO` | 4.723.643 | log do que cada funcionário fez |

Hospital tem rastreabilidade granular — qualquer ação clínica gera log. Boa fonte pra reconstituir "quem fez o quê".

---

## 6. Faturamento SUS

| Tabela | Linhas | Função |
|---|---:|---|
| `TU_BPA_UNIFICADA` | 5.174.677 | Boletim Produção Ambulatorial unificado |
| `TU_PREVIA_BPA` | 4.027.578 | prévia BPA |
| `MATMED_LOTE_SAIDA` | 5.694.958 | saída material/medicamento |
| `MATMED_MOV_CC` | 1.943.712 | movimento centro cirúrgico |

---

## 7. Identidades Oracle conhecidas

| Conta | Privilégios | Uso |
|---|---|---|
| `SYBSA` | Owner do schema lógico Salux | Conta da aplicação (`portal.exe`, `suprim.exe`, etc) |
| `SUPERVISOR` | Custom roles (`ROLE_CRIARUSUARIOS`, `ROLE_INFOSAUDE`) | Admin Salux humano; **lê V$SESSION mas NÃO V$SQL** |
| `INFOSAUDE` | Owner real das tabelas | Schema-owner físico (acesso via synonyms públicos) |
| `salux_obs` | `CREATE SESSION` + `SELECT_CATALOG_ROLE` | **Criada por nós** — leitura de V$/DBA_ views |
| `SYS` | SYSDBA | Via SSH root → `su - oracle` |

---

## 8. Padrões observados

### 8.1 Chave temporal composta
FIA e BAA usam `(CD_HOSPITAL, DT_ANO_<X>, NR_<X>)`. Não há ID surrogate global — número é sequencial **por ano** e **por hospital**. Implica que duas FIAs com mesmo NR mas anos diferentes são entidades distintas.

### 8.2 Paciente unificado
`CD_PACIENTE_UNIFICADO` em FIA/BAA — quando paciente tem múltiplos cadastros, é unificado. Sempre usar `NVL(cd_paciente_unificado, cd_paciente)` em joins.

### 8.3 Internação vs Ambulatório
Modelo é dual: praticamente toda tabela clínica tem versão FIA e BAA (`EVOLUCAO_PACIENTE_FIA` / `EVOLUCAO_PACIENTE_BAA`, `PRESCRICAO_FIA` / `PRESCRICAO_BAA`, etc). Razão: tipos de atendimento diferentes têm contextos diferentes (leito, alta, motivo cobrança SUS).

### 8.4 Tipos categorizados via `ID_TIPO`
SINAIS_VITAIS, EDOC_MOVIMENTO, etc usam `ID_TIPO` CHAR(1): `F` (FIA/internação), `B` (BAA/ambulatório). Discriminador inline.

### 8.5 Códigos curtos
`CD_HOSPITAL` NUMBER, `CD_FUNCIONARIO` VARCHAR2(8) (alfanumérico tipo `INFO`, `SUPER01`), `CD_MEDICO` NUMBER. CRM/CRF presumível em coluna separada.

### 8.6 EAV é dominante via eDoc
Qualquer "formulário" (anamnese, evolução, sumário de alta, laudo) provavelmente foi modelado como eDoc — entidade em EDOC_MOVIMENTO + respostas em EDOC_MOVIMENTO_ITEM. Para reproduzir uma tela, **prefirir consultar via eDoc + JOIN ao item** do que esperar uma tabela tradicional.

---

## 9. Recomendações para o front React+Vite

1. **Não tente espelhar 333 modelos eDoc 1:1.** Identifique os 20 mais usados (acima) e priorize. Os outros podem ficar atrás de um "render genérico" baseado em `EDOC_DOCUMENTO_ITEM` + `EDOC_TIPO_ITEM`.
2. **Backend deve mediar.** A complexidade do eDoc (EAV + scripts + regras + cabeçalho/rodapé) não dá pra cuspir cru pro front. Criar API que retorne `documento_renderizavel` com schema declarativo (campo, tipo, opções, validação) + payload de respostas.
3. **Read-only primeiro.** Tela 1 do React = ver prontuário completo do paciente (histórico de FIAs/BAAs + eDocs preenchidos). Cadastro/edição depois.
4. **PowerBuilder usa RichText.** Os campos `EDOC_TIPO_ITEM=7` (Texto 1 linha) na verdade renderizam RichText — seguro assumir UTF-8 + tags HTML/RTF nas respostas. Pode precisar parser.
5. **Identificação tem CNS, CPF, RG, CN, MCN.** Suportar pelo menos CPF + CNS (cartão SUS) + RG. Paciente unificado é regra (não exceção).
6. **Triagem (3M linhas) é entrada do fluxo de urgência** — provavelmente alimenta BA. Mapear depois.

---

## 10. Queries-chave reveladas por snapshot de V$SQL (1h, 134 SELECTs)

Captura direta das queries que sessões SYBSA estavam executando. SQL completos em `capturas/snapshot_sql_1h.jsonl`.

### 10.1 Lista de pacientes por setor (a tela inicial) — `SQL_ID a7kfhkn3kg1ht`

`UNION` de 3 partes (BAA normal + BAA SADT + FIA internação). Tabelas:

```
BAA b
  ⋈ PACIENTE p (via NVL(cd_paciente_unificado, cd_paciente))
  ⋈ PLANO_SAUDE ps
  ⋈ MOTIVO_ATENDIMENTO ma
  ⋈ SALA_AMBULATORIO sa
  ⋈ UNIDADE_HOSPITALAR uh
  ⋈ CENTRO_CUSTO cc
  ⊳ CLASSIFICACAO_RISCO cr (cor da triagem)
  ⋈ PARAMETRO_LISTAPAC pl (configuração do funcionário)
  ⊳ ALTA_MEDICA a
  ⊳ ACOLHIMENTO

FIA f
  ⋈ FIA_MEDICO fm (último médico responsável — MAX dt_inicio_atend)
  ⋈ FIA_LEITO fl (último leito — MAX dt_transferencia)
  ⋈ FIA_TIPO_PLANO ftp (plano vigente — MAX dt_inicio_plano)
  ⋈ PACIENTE p
  ⋈ PLANO_SAUDE ps
  ⋈ PARAMETRO_LISTAPAC pl
  ⊳ ALTA_MEDICA
```

**Binds**: `:pl_hospital`, `:pl_unidade`, `:pl_cd_quarto`, `:ps_funcionario`, `:pdt_ini`/`:pdf_fim`, `:ps_atendidos` (S/T/E/X), `:ps_sel_med/pla/setor`, `:ps_id_tipo_prescricao`, `:ps_cd_conselho`.

**Funções PL/SQL usadas**:
- `f_existe_prescricao_vigente('B'|'F', cd_hosp, nr, ano, tp_presc, conselho, 'A'|'P')` — checa prescrição vigente atual/próxima
- `F_BUSCA_IDADE(dt_nasc, SYSDATE, 2)` — calcula idade
- `f_busca_hospital_internacao(...)` — hospital onde internou

**Colunas-chave retornadas**: `nr_fia_baa`, `dt_atendimento`, `nm_paciente`, `cd_quarto/leito`, `cd_cor_risco`, `qt_minutos_espera`, `qt_minutos_triagem`, `ds_setor`, `in_prioritario`, `cd_acolhimento`, `nro_senha`, `c_idade`.

### 10.2 Evolução do paciente FIA — `SQL_ID c6c989utrdnm2`

`UNION ALL` de 5 ramos (A/B/C/D/E) com diferentes filtros. Tabelas:

```
EVOLUCAO_PACIENTE_FIA epf
  ⋈ FIA f
  ⋈ PACIENTE p
  ⋈ HOSPITAL h
  ⋈ FUNCIONARIO fn
  ⋈ FIA_TIPO_PLANO ftp
  ⋈ PLANO_SAUDE ps
  ⊳ TIPO_PLANO tp
  ⊳ MEDICO m
  ⋈ FIA_LEITO fl
  ⋈ UNIDADE_HOSPITALAR uh
```

**Campos retornados**: `epf.tx_evolucao` (texto LONG), `nm_funcionario`, `dt_evolucao`, `cd_quarto || ' - ' || cd_leito`, `cd_conselho`, `nr_crm`, `sc_unidade`, `f.dt_previsao_alta`, `epf.cd_assinatura_digital`.

### 10.3 Prescrição (medicação) — `SQL_ID 7j426akyda6u6`

4 UNION ALL: dois pra FIA (corrente + vigentes) e dois pra BAA. Tabelas:

```
PRESC_FIA_OPC_PROD opc | PRESC_BAA_OPC_PROD opc
  ⋈ MATMED m            (catálogo de material/medicamento)
  ⋈ PRESCRICAO_FIA pf  | PRESCRICAO_BAA pb
```

**Campos por item de prescrição**:
- `cd_material`, `ds_material`, `qt_material_prescrita`, `qt_material_solicitada`
- `cd_via` (via administração), `cd_diluicao`, `cd_intervalo`, `cd_horario`, `cd_unidade_medida`
- `hora_inicial`, `hora_final`, `qt_vezes`, `qt_solicitada_horario`
- `dt_inicio_apraz`, `ds_abrev_horario_imp`, `ds_horario`, `ds_freq_alt_medico`
- `in_urgencia`, `observacao`, `in_criterio_medico`, `in_opcional`
- `cd_procedimento`, `cd_intervencao`
- `in_frac_producao`, `in_fracionado`, `in_controlado` (do MATMED)
- `in_alto_risco`, `in_sub_inicio_imediato_freq`
- `tempo_prev_uso`, `tempo_decorrido_prev_uso`

### 10.4 Solicitação de exames — `SQL_ID 37fvr344d9ukk`

```
SOL_EXAME ⋈ SOL_EXAME_ITEM
  ⋈ PACIENTE
  ⋈ PROCEDIMENTO
  ⊳ COMPROMISSO_ATENDIDO ⊳ COMPROMISSO (agendamento)
  ⋈ PLANO_SAUDE
  → BAA ⋈ SALA_AMBULATORIO ⋈ UNIDADE_HOSPITALAR  (origem ambulatório)
  → FIA_LEITO ⋈ UNIDADE_HOSPITALAR                (origem internação)
```

Campos: `dt_compromisso`, `cd_procedimento`, `ds_procedimento`, `id_situacao_exame` (SO/CO/AE/IE), `dt_agendamento`, `dt_realizado`, `qt_solicitada`, `ds_hipotese_diagnostica`, `in_urgencia`, `in_exame_atendido`/`in_exame_cancelado`, `cd_quarto/leito`.

### 10.5 Laudo de exame — `SQL_ID bn4dffyqvd1xb`

```
SOL_EXAME ⋈ SOL_EXAME_ITEM ⋈ PROCEDIMENTO ⋈ LAUDO
  ⊳ LAUDO_CONSOLIDADO (URL PDF)
```

Campos: `cd_procedimento`, `ds_procedimento`, `in_laudo_sigiloso`, `id_situacao`, `id_tipo_laudo`, `laudo_url`.

### 10.6 Alergias do paciente — `SQL_ID 592cjjpjtkp3j`

```
PACIENTE_X_ALERGIA ⋈ PACIENTE ⋈ ALERGIA
  ⋈ ALERGIA_X_MEDICAMENTO ⋈ MATMED
```

Campos: `cd_alergia`, `ds_alergia`, `cd_medicamento`, `ds_material`, `ds_descritivo`, `ativo_alergia`, `ativo_paciente`.

### 10.7 Parâmetros do prontuário — `PARAMETRO_PRONTINTAMB`

Lookup por `cd_hospital` — 1 linha por hospital com **dezenas de flags** que controlam comportamento da UI:

- `in_valida_prescricao_dia`, `in_esconde_aba_equi`, `in_exibe_uni_presc_frac`, `in_usa_vinc_prod_via`
- `in_exibir_desc_alt_susp`, `in_tp_pesq_medic`, `in_mostra_dt_agenda_ex`
- `in_tipo_ordena_itens_presc`, `in_ordena_itens_presc`, `cd_via_administracao`, `in_opcionais_aut`
- `in_alerta_inter_medic`, `in_alerta_medic_injetavel`
- variantes por categoria de prescrição: `_enf` (enfermagem), `_mprof` (multiprofissional)
- `in_inicio_imediato_freq`, `in_acm_nao_gerar_req_aut`, `in_controla_dia_antibiotico`
- `in_exige_interv_dose_acm`, `in_cad_modelo_prescricao`
- evolução: `in_usa_rtf_evolucao`, `in_permissao_evol`, `in_usa_mod_evolucao`, `qt_caracter_evolucao`, `in_utiliza_plano_terap`, `in_usa_logo_evolucao`, `in_imp_prev_evolucao`

### 10.8 Top queries por volume na 1h

| SQL_ID | Execs | Função |
|---|---:|---|
| `4dmvu5fq6w8u0` | **121.438.442** | `SELECT tipo_evento FROM setor_fluxo WHERE cd_setor=:1` — polling para detecção de mudança de setor (UI live) |
| `ga8yuc3tc72s7` | 11.806.980 | `SELECT SYSDATE FROM DUAL` — keep-alive |
| `1669ykqfts7py` | 9.698.808 | `SELECT DateDiff(DD, :1, :2) FROM dual` — cálculo de dias |
| `f0wzs9nc663bn` | 3.645.099 | `SELECT sysdate FROM dual` |
| `6467njuzzttmu` | 2.042.466 | Permissões eDoc (parcial/definitivo/inativar/visualizar/imprimir/excluir) |
| `3xh980fmzaf07` | 1.308.094 | `SELECT nm_paciente FROM paciente WHERE cd_paciente=:1` |
| `3h924pd5jtn9q` | 1.122.805 | `SELECT nm_funcionario FROM funcionario WHERE cd_funcionario=:1` |
| `f06wvkg37ptu8` | 119.119 | `MAX(id_movimento)` em EDOC_MOVIMENTO (geração de próximo ID) |

---

## 11. Setores ativos no hospital — `SETOR_FLUXO` (73 setores)

Padrão `<UNIDADE> <ÁREA>`. `TIPO_EVENTO` ∈ {TOTEM, RECEPCAO, TRIAGEM, INTERNACAO, AGENDA, SADT, EXAME, ENTRADA, COLETA, CONSULTORIO, ACOLHIMENTO}.

Distribuição por unidade:
- **HMCML** (Hospital Maternal Conde Modesto Leal): 38 setores (incluindo PED = Pediatria) — classificação adulta/obstétrica/pediátrica, consultórios clínica/ortopedia/bucomaxilo/covid, medicações, SADT (lab, ECG, RX, TC, USG), trauma, imobilização, curativo
- **UPA** (Inoã): 16 setores — acolhimento, classificação, salas vermelhas/amarelas/isolamento, consultórios clínica/pediatria, RX, trauma/sutura, serviço social, odontologia
- **STA RITA** (Posto Sta Rita): 13 setores — acolhimento, classificação, consultórios clínico/pediatria, sala de sutura, trauma, reavaliação, medicação
- **Genéricos** (1-9): TOTEM, AMBULATORIO, STA RITA CLAS DE RISCO, INTERNACAO, AGENDAMENTO, SADT, EXAMES, ENTRADA, COLETA

Coluna `IN_ATIVO` (S/N) controla se aparece na UI hoje.

---

## 12. Funções PL/SQL identificadas (não-Oracle padrão)

- `F_BUSCA_IDADE(dt_nasc, dt_ref, tipo)` — idade do paciente
- `f_existe_prescricao_vigente(tipo, cd_hosp, nr, ano, tp_presc, conselho, atual_proxima)` — boolean
- `f_busca_hospital_internacao(cd_hosp, ano_fia, nr_fia, dt_evolucao, retorno)` — hospital de internação
- `tab_union` — provavelmente view/synonym auxiliar pra retornar valores nulos em UNION

São presumíveis funções no schema INFOSAUDE. Próximo passo seria inspecionar via `DBA_SOURCE WHERE name LIKE 'F_%'`.

---

## 13. Investigações da seção 13 — concluídas

### 13.1 EDOC_REGRA / EDOC_REGRA_CONDICAO / EDOC_REGRA_FORMULA — DSL declarativa

**`EDOC_REGRA`** (324 regras) liga 1 regra a 1 item (`CD_ITEM`). Campos-chave: `IN_TIPO` (P=pontuação, T=text, etc), `IN_VINCULADO`, `IN_SITUACAO`, ciclo de aprovação (`DT_LIBERACAO`/`CD_FUNCIONARIO_LIB`).

**`EDOC_REGRA_CONDICAO`** (837 condições). Estrutura **se-então em SQL declarativo**:
- `DS_OPERACAO_INI` (ex: `IGUAL A`, `MAIOR QUE`)
- `VALOR_INI` (valor a comparar, ex: "Movimento despropositados frequentes" ou "0")
- `DS_OPERADOR` (E / OU)
- `DS_OPERACAO_FIM` + `VALOR_FIM` (range)
- `MENSAGEM` (label/saída)
- `DS_RESULTADO` (rótulo ex: "Alerta e Calmo")
- `CD_COR_FONTE` / `CD_COR_FUNDO` (cores condicionais — escalas clínicas usam isso)
- `PONTUACAO` (pontos atribuídos — base das escalas Glasgow/RASS/etc)

**`EDOC_REGRA_FORMULA`** (10 fórmulas — só as somatórias importantes). `DS_FORMULA` é expressão matemática usando `PARAM_1 + PARAM_2 + ...`. Exemplos reais capturados:
- `PARAM_1 + PARAM_2 + PARAM_3` (soma de 3 escores)
- `PARAM_1 + PARAM_2 + PARAM_3 + PARAM_4 + PARAM_5` (soma de 5 — Braden, Glasgow expandido)
- `PARAM_1 / (PARAM_2 * PARAM_2)` (IMC = peso / altura²)
- `ROUND(((PARAM_1 - PARAM_2) / 7), 1)` (semanas gestacionais a partir de datas)

Conclusão: o sistema de regras eDoc é **declarativo via SQL** — não é PowerScript embarcado.

### 13.2 EDOC_SCRIPT — SQL armazenado para popular dropdowns/lookups dinâmicos

`EDOC_SCRIPT.DS_SCRIPT` (CLOB) contém **SELECTs puros**. Funcionalidade: cada campo do tipo `DropDown` (CD_TIPO_ITEM=6) ou `ListBox` (=9) pode referenciar um script via `CD_SCRIPT_SEL` no EDOC_ITEM.

Exemplos reais (recuperados de capturas/investig_edoc_script.txt):
- Script 73/74: dropdown de procedimentos SUS + CIDs (com filtros de instrução de registro)
- Script 51/52: dropdown CIAP-2 (Classificação Internacional Atenção Primária) / PROC_SIA
- Script 55: lookup de CNS+CBO+INE do funcionário (autor/parcial/definitivo) — para SOAP/CBO
- Script 10095: visão consolidada da **triagem** (Glasgow nivel_consciencia, pupila, PA, pulso, FR, temp, SatO2, peso, altura, idade gestacional, etc) com JOIN em ~10 tabelas
- Scripts 10096/10097/10098/10101: buscam o ÚLTIMO valor do paciente de algum item (cd_item=11132 alergias, =12231 peso, =12234 situacao, =13065 parecer) atravessando `EDOC_MOVIMENTO` por (cd_hospital, dt_ano_baa/fia, nr_baa/fia) ordenados por `id_movimento` DESC com `in_status='D'`.
- Script 10100: relatório cruzando paciente × eDoc × funcionário (uso interno do administrador)
- Script 10099: lookup de médicos com `cd_conselho='CRM' OR 'CRO'`

São ~153 scripts ativos, 7252 linhas de SQL puro armazenado.

### 13.3 EDOC_MOVIMENTO real (Escala de Glasgow) — shape EAV concretamente

Capturado: movimento `(hosp=1, ano=2022, id=112993)`, modelo 10029 (Glasgow), paciente CELMA DIAS MOREIRA (78 anos, prontuário 34948), FIA 28002/2022.

16 linhas em `EDOC_MOVIMENTO_ITEM`:

| cd_item | ds_item | tipo_item | resposta |
|---:|---|---|---|
| 111 | Data Documento | Data/Hora | `19/03/2022 19:38` |
| 6 | Nome do Paciente | Texto (1 linha) | `CELMA DIAS MOREIRA` |
| 10 | Número do Atendimento | Texto (1 linha) | `28002/2022` |
| 8 | Data de Nascimento | Data | `03/07/1943` |
| 7 | Sexo do Paciente | Texto (1 linha) | `Feminino` |
| 13 | Prontuário do Paciente | Texto (1 linha) | `34948` |
| 9 | Idade do Paciente | Texto (1 linha) | `78 ano(s) 8 mes(es) 16 dia(s)` |
| 12 | Convênio do Atendimento | Texto (1 linha) | `SISTEMA UNICO DE SAUDE` |
| 772 | Abertura Ocular | RadioButton | `Espontânea` |
| 773 | Resposta Verbal | RadioButton | `Orientada` |
| 774 | Resposta Motora | RadioButton | `À ordem` |
| 777 | Reatividade Pupilar | RadioButton | `Completa` |
| 775 | Total Geral - Glasgow 2018 | Numérico | `15` |
| 776 | Resultado - Glasgow 2018 | Texto (1 linha) | `TRAUMA LEVE / NORMAL` |
| 10034 | Nome do Usuário Inclusão | Texto (1 linha) | `THAIS STAITE LOURENCO` |
| 10018 | CRM Profissional Inclusão | Texto (1 linha) | `356780` |

Observações:
- Cabeçalho do documento (paciente, idade, prontuário, convênio) é **redundante** com PACIENTE/FIA — preenchido na geração por scripts (não é truth source).
- Os 4 RadioButtons (Glasgow) são as entradas. `Total Geral` é calculado via `EDOC_REGRA_FORMULA` (`PARAM_1 + PARAM_2 + PARAM_3 + PARAM_4`).
- `Resultado` (texto) é decidido via `EDOC_REGRA_CONDICAO` baseado no valor de `Total Geral` (15 → "TRAUMA LEVE / NORMAL").
- Autor (nome + CRM) também é embutido nos itens — duplicação de FUNCIONARIO/MEDICO.

### 13.4 Domínios decodificados

**`PRESCRICAO_FIA.ID_TIPO_PRESCRICAO`** + **`PRESCRICAO_BAA.ID_TIPO_PRESCRICAO`**:

| Code | FIA (765k) | BAA (1.81M) | Significado provável |
|:---:|---:|---:|---|
| `M` | 523.097 (68%) | 1.795.475 (99%) | **Médica** |
| `F` | 216.881 (28%) | 17.980 (1%) | **Farmacêutica/Enfermagem** (high in FIA, low in BAA) |
| `O` | 25.821 (3%) | 653 | **Odontológica** (cd_conselho=CRO) |
| `L` | 49 | 17 | **Multiprofissional/Livre** (residual) |

**`EDOC_MOVIMENTO.IN_STATUS`**:
- `D` = **Definitivo** (5.006.564 — 97.5%)
- `P` = **Parcial/Rascunho** (125.916 — 2.5%)

**`EDOC_MOVIMENTO.IN_ATIVO`**: S (5.1M) / N (15k inativados).

**`BAA.IN_BAA_ATENDIDO`**:
- `N` = Não atendido (espera) — 716k
- `S` = Atendido — 730k
- `E` = Em atendimento — 277k

**`BAA.ID_DESTINO`** (destino após atendimento):
- `C` = **Casa/alta** (1.36M — 87%)
- `X` = ? (143k)
- `E` = **Encaminhado** (72k)
- `I` = **Internação** (35k)
- `D` = **Óbito** (10.5k)
- `B` = ?, `T` = **Transferência** (5k), `O` = ?, `U` = ?

**`FIA.ID_INTERNACAO`**:
- `U` = **Urgência** (34.950 — 83%)
- `E` = **Eletiva** (6.875 — 17%)

### 13.5 LEITO / QUARTO / FIA_LEITO — controle de leitos

**46 tabelas** relacionadas a leito/quarto. Principais:

- **`LEITO`** (548 leitos no hospital) — 22 cols. PK composta: `(CD_HOSPITAL, CD_UNIDADE, CD_QUARTO, CD_LEITO)`. Tem `ID_SIT_LEITO` (situação), `ID_LEITO` (tipo), `IN_EXTRA`, `IN_PERMITIR_ALTA`, `ID_DESTINACAO_LEITO`, `ID_GERENCIA_LEITO`, `IN_RESERVA_AG_CIRURGIA`.
- **`QUARTO`** (43 quartos) — 8 cols. `CD_TP_QUARTO`, `IN_ISOLAMENTO`, `SEXO`, `ID_QUARTO`, `CD_GRUPO_QUARTO`.
- **`FIA_LEITO`** (93.078 transferências históricas) — 32 cols. **Vínculo paciente↔leito ao longo do tempo**: `DT_TRANSFERENCIA` (entrada no leito), `DT_SAIDA_LEITO`, `DT_HIGIENIZADO`, `DT_LIBERADO`, `IN_ISOLAMENTO`, `IN_PRECAUCAO`, `CD_TP_UTI_SUS` (tipo UTI SUS), `IN_TROCA`, `IN_DIARIA_DIV`, processo de higienização (`DT_INI_HIGIENIZACAO`, `CD_FUNC_HIGIEN_INICIO`, `CD_FUNC_HIGIEN`), liberação (`CD_FUNC_LIBER`). Indica que cada paciente internado tem 1+ linhas em FIA_LEITO conforme troca leito.
- **`PAC_DIA_UNID_TPQUARTO`** (204k linhas) — pacientes-dia por unidade/tipo de quarto (faturamento de diárias).
- **`LEITO_DIA`** (52k) — ocupação diária para AIH SUS.
- **`LEITO_HISTORICO`** (12k) — histórico de eventos.

### 13.6 Faturamento — múltiplos sistemas SUS + TISS

**BPA** (Boletim Produção Ambulatorial — SUS): `TU_BPA_UNIFICADA` (5.17M), `TU_PREVIA_BPA` (4.02M), `TU_BPA` (692k), `TU_BPA_UNIF_MESTRE`/`TU_BPA_GERADO`.

**AIH** (Autorização Internação Hospitalar — SUS): `AIH` (38k), `LOG_AIH` (1.4M), `AIH_ATO_PROF` (812k), `AIH_CALC_VLR` (166k), `AIH_X_CONTROLE_LEITOS_ESPEC` (43k), `AIH_X_CID_SEC`, `AIH_RECEM_NATO`.

**APAC** (Autorização Procedimentos Alta Complexidade): pequena — `PROCED_SEC_APAC`, `ESP_APAC`, `APAC_PROCED_EXCLUDENTE`. Não muito usada.

**SIA** (Sistema Informação Ambulatorial — SUS): 38 tabelas — `PROCED_SIA` (6.5k procedimentos), `PROC_SIA_X_CID` (44k), `PROC_SIA_X_HIERARQUIA`, `PROC_SIA_X_TP_PREST`, `PROCED_SIA_NEGOCIADO`, etc.

**TISS** (planos privados — Saúde Suplementar): 13 tabelas — `TISS3_01_CBOS` (1.5k), `TISS3_03_PARAMETROS`, `TISS_CBOS`, `TISS_ACOMODACAO`, `TISS_TABELA_PRECO`, `TISS_VERSAO_XML`, etc. Suporte versões TISS 3.01 e 3.03.

**SUS_** + **\_SUS** (lookups SUS): 34 tabelas pequenas (cor pele, nacionalidade, tipo UTI, motivo cobrança, dependência, faixa etária, CARATER_INTERNACAO_SUS).

**CNES** (Cadastro Nacional Estabelecimentos Saúde): **0 tabelas** — não há integração CNES direta no schema.

### 13.7 Integração externa — não há PACS DICOM nativo, mas há vários SaaS via URL

**`SOL_EXAME_IMAGEM`** existe mas é vazia (0 linhas). **Nenhuma** tabela com nome PACS/DICOM populada.

URLs configuradas via tabelas de parâmetro (40+ campos `*URL*` ou `URL_*` em 20+ tabelas):

| Integração | Tabela / Coluna |
|---|---|
| Receituário eletrônico ANS | `INTEGRA_NEXODATA_BAA.URL_PDF` + `URL_PDF_LME` (Lista Medic. Excepcionais) |
| Receituário eletrônico ANS | `INTEGRA_NEXODATA_FIA.URL_PDF` + `URL_PDF_LME` |
| Receituário eletrônico | `RECEITUARIO_ELETRONICO_DOC.URL_PDF` + `URL_PDF_LME` |
| Assinatura digital | `CONFIG_ASSINATURA_DIGITAL` (URL, AUTHORIZE, TOKEN, VERIFY, REDIRECIONAMENTO, CERTIFICADO) + `CONFIG_ASS_DIGITAL_FUNC` |
| Optix (PACS externo?) | `OPTIX_PARAMETRO.DS_URL_GERACAO_UID` |
| Telemedicina | `PARAMETRO_TELEMEDICINA.URL_BASE_TELECONSULTA` |
| Voz | `VOZ_INTEGRACAO_URI.URL_API` |
| CME (centro material esterilizado) | `CME_INTEGRACAO_URI.URL_API` |
| GED | `PARAMETRO_HOSPITAL.DS_URL_GEDWEB` |
| Crypto WS | `EVAL_PARAMETRO.DS_URL_WS_CRYPTO_SERVER` + `DS_URL_WS_KEY_MANAGER` |
| Conexa (?) | `PARAMETRO_CONEXA.DS_URL_WS_RECEPCAO` |
| Hospital | `HOSPITAL.DS_URL_LOGO` |
| Portal | `CONFIG_PORTAL.DS_URL_NOTICIAS`, `DS_URL_VIDEO_PORTAL` |
| Salux web | `GE_PARAMETRO.DS_URL_SALUX` |
| Laudo (link PDF) | `LAUDO.LAUDO_URL`, `LAUDO_CONSOLIDADO.LAUDO_URL` |

**`LAUDO_CONSOLIDADO`** tem só 6 linhas mas é o lugar onde laudos finalizados ficam consolidados (provavelmente com BLOB do PDF).

### 13.8 Funções PL/SQL (top 100, parcial)

Top relevantes pro front:
- Clínicas: `F_BUSCA_IDADE`, `F_ANOS_MESES_DIAS`, `F_BUSCA_ALERGIAS`, `F_EXISTE_PRESCRICAO_VIGENTE`, `F_BUSCA_HOSPITAL_INTERNACAO`, `F_BUSCA_CLINICA_FIA`, `F_GET_NM_FUNCIONARIO`, `F_GET_UNIDADE_HOSPITALAR`, `F_BUSCA_SITUACAO_CIRURGIA`/`_ITEM`, `F_BUSCA_SITUACAO_SALA_RESERVA`
- Conversão: `F_CONVERTE_LONG`, `F_LONG_TO_CHAR`, `F_LONG_ITEM_CTB`
- Tempo: `F_DIFERENCA_DATETIME`, `F_MINUTOS_ENTRE_DATAS`, `F_SOMA_MINUTOS`, `F_TEMPO_ENTRE_DATAS`
- Estoque: `F_GET_QT_ESTQ_MMD`, `F_GET_QT_MAT_MOV_MMD`, `F_GET_VL_CA_MMD`/`_MMH`, `F_GET_VL_CM_*`, `F_RECALCULAR_CM*` (Custo Médio Atual e Custo Médio com frações), `F_LOTES_NEGATIVOS`
- Faturamento: `F_BUSCA_GUIA_AUTORIZACAO`, `F_BUSCA_NR_NFSE`, `F_CALCULA_AVALIACAO_FORNECEDOR`, `F_CALC_VL_TOTAL_*`, `F_BUSCA_TUSS_COD_DSC`
- **Integração Grifols** (banco de sangue): `F_GRIFOLS_MSH`, `F_GRIFOLS_PID`, `F_GRIFOLS_PV1` — geração de mensagens HL7
- **Integração bancária Santander**: `F_SANTANDER_MONTA_HEADER_0/TPO_1`, `F_SANTANDER_MONTA_SEGMENTO_A/B/J/J52/N/O`, `F_SANTANDER_TRAILER_TIPO_5/9` (CNAB)

Os limit=100 sugere ter MAIS — vou aumentar se necessário.

### 13.10 Modelo relacional via FKs (`DBA_CONSTRAINTS`)

Mapeei FKs saindo de 43 tabelas core (~593 FKs totais). Tabelas mais "centrais" (mais FKs saindo):

| Tabela | FKs saindo | Função |
|---|---:|---|
| BAA | 52 | nó central do atendimento ambulatorial |
| FIA | 39 | nó central da internação |
| SOL_EXAME | 33 | hub de exames |
| REQUISICAO | 26 | hub de farmácia |
| AIH | 25 | hub de faturamento internação SUS |
| PACIENTE | 21 | hub identidade |
| MEDICO | 20 | profissional |
| EDOC_MOVIMENTO | 18 | hub eDoc preenchidos |
| CHAMADA_PACIENTE | 18 | painel de senha (poly: BAA + FIA + ACOLHIMENTO) |
| MATMED | 16 | catálogo material/medicamento |
| SOL_EXAME_ITEM | 15 | itens de exame |
| ACOLHIMENTO | 12 | acolhimento (pré-triagem) |
| TIPO_PLANO | 11 | convênios |
| PROCEDIMENTO | 11 | catálogo procedimentos |
| LAUDO | 11 | laudos |
| PRESCRICAO_FIA | 11 | prescrição internação |
| PRESCRICAO_BAA | 11 | prescrição ambulatório |
| FICHA_PACIENTE | 10 | ficha clínica |
| EDOC_DOCUMENTO | 10 | template eDoc |
| EVOLUCAO_PACIENTE_FIA | 10 | evolução internação |
| EVOLUCAO_PACIENTE_BAA | 10 | evolução ambulatório |
| FIA_LEITO | 10 | leito/transferência |

Detalhes em `capturas/investig_fks.txt`. Algumas relações importantes confirmadas:

**Modelo eDoc completo:**
```
EDOC_MODELO ──< EDOC_DOCUMENTO ──< EDOC_DOCUMENTO_ITEM
                    │                     │
                    │                     └──> EDOC_ITEM ──> EDOC_TIPO_ITEM (tipo de widget)
                    │                                  └──> EDOC_SCRIPT (lookup SQL dinâmico)
                    │                                  └──> EDOC_ESCALAS
                    │                                  └──> EDOC_GRUPO_ITEM
                    │
EDOC_MOVIMENTO <────┘
   │ (CD_MODELO, CD_DOCUMENTO)
   │
   ├──> PACIENTE
   ├──> FIA  (CD_HOSPITAL, DT_ANO_FIA, NR_FIA)
   ├──> BAA  (CD_HOSPITAL, DT_ANO_BAA, NR_BAA)
   ├──> UNIDADE_HOSPITALAR
   ├──> ESPECIALIDADE
   ├──> ASSINATURA_DIGITAL
   └──> FUNCIONARIO (×4: incluiu, inativou, bloqueou, ass.ele.paciente, verificou ass.)
        │
        └──< EDOC_MOVIMENTO_ITEM (resposta de campo)
               └──> EDOC_DOCUMENTO_ITEM (definição: ordem/seq)
```

**Fluxo de acolhimento/triagem/atendimento:**
```
LISTA_SENHA ──< ACOLHIMENTO ──< TRIAGEM ──< BAA / FIA
                    ↓               ↓          ↓
              CLASSIFICACAO_RISCO   ↓          ↓
                                  CHAMADA_PACIENTE (painel)
                                                ↓
                                          SALA_PAINEL
                                          CAD_HOST (estações + painéis físicos)
```

**Leito/quarto:**
```
HOSPITAL ──< UNIDADE_HOSPITALAR ──< QUARTO ──< LEITO
                                    │           │
                                    └──< FIA_LEITO ──> FIA
                                          (histórico transferências)
```

**Identificação de paciente:**
```
PACIENTE
  ├─ CD_PACIENTE_UNIFICADO -> PACIENTE.CD_PACIENTE  (auto-ref para unificação)
  ├─ CD_UF / CD_CIDADE -> CIDADE (até 4 endereços diferentes)
  ├─ CD_COR -> COR_PELE
  ├─ CD_NACIONALIDADE -> NACIONALIDADE_SUS
  ├─ CD_CBO / CD_CBOR -> TU_CBO / CBOR (ocupação)
  ├─ ID_INSTRUCAO -> GRAU_INSTRUCAO
  ├─ CD_RELIGIAO -> RELIGIAO
  ├─ CD_BARREIRA_COMUNICACAO -> BARREIRA_COMUNICACAO
  ├─ CD_PLANO_SAUDE/CD_TP_PLANO -> TIPO_PLANO
  └─ IN_VIP -> VIP
```

### 13.9 CHAMADA_PACIENTE — painel de senha (`p_senha.exe`)

22 colunas. PK lógica: `ID_CHAMADA_PACIENTE`. Vínculo a BAA (`DT_ANO_BAA`/`NR_BAA`) ou FIA (`DT_ANO_FIA`/`NR_FIA`).

Modelo: registro de **cada chamada de paciente no painel**. `NM_HOST_EMISSOR` (estação que chamou — ex: HMCML-208, HCML-1000), `NM_HOST_RECEPTOR` (painel — ex: PAINEL1.CML), `DS_SALA_PAINEL` (texto livre: "consultório 4", "CLASSIFICACAO 02"), `DT_HR_CHAMADA`, `DT_HR_COMPARECIMENTO`, `DT_HR_CANCELADA`, `PRIMEIRO_NM_PACIENTE` (truncado pra privacidade — só primeiro nome no painel), `SENHA`.

**`IN_STATUS`** (CHAR(2)):
- `CO` = **Compareceu** (3.4M — 81%)
- `CA` = **Cancelado** (729k — 17%)
- `CH` = **Chamando** (24k — em curso)
- `AC` = **Aceito/Aguardando** (22k)

---

## 14. App PowerBuilder — extração de SQL embarcado

PowerBuilder compila código em **PBDs** (Dynamic Libraries). As strings são UTF-16 LE; SQL fica embutido como literal. **3.4 GB** em 1.318 PBDs (84 MB EXE + 880 MB DLLs).

Extração completa (regex sobre bytes UTF-16 LE filtrando começo SELECT/INSERT/UPDATE/DELETE/MERGE/WITH):

| Módulo | Linhas SQL | Bytes |
|---|---:|---:|
| Cadastros.Gerais | **51.485** | 2.45 MB |
| Faturamento.SUS | 50.472 | 1.89 MB |
| Portal | 49.629 | 2.29 MB |
| Financeiro | 32.872 | 1.46 MB |
| Estatistica | 30.445 | 1.22 MB |
| Compras | 26.593 | 1.11 MB |
| Suprimentos | 23.967 | 1.07 MB |
| Gestao.Glosas | 22.685 | 0.93 MB |
| APAC | 19.031 | 0.77 MB |
| Ambulatorio.SUS | 18.424 | 0.81 MB |
| Agenda | 17.310 | 0.72 MB |
| Centro.Cirurgico | 7.712 | 0.32 MB |
| Atualizador.Tabelas.SUS | 4.379 | 0.16 MB |
| SADT | 877 | 47 KB |
| Painel.Gerencial | 628 | 22 KB |
| Painel.Paciente | 280 | 12 KB |
| Painel.Senha | 146 | 7 KB |
| TABELAS-SUS | 0 | 0 |
| **TOTAL** | **356.935** | **14.6 MB** |

Achados em amostra de `Cadastros.Gerais`:

**Integração HL7** identificada — funções no PL/SQL como `F_MSH('SX Sigma', '', '', '', 'ADT^A01^ADT_A01')` geram segmentos HL7 ADT (Admission/Discharge/Transfer) para integração externa. "SX Sigma" é o sending application no HL7. **Salux fala HL7 v2.x para integração com LIS/RIS/outros HIS**.

**SQL embarcado** inclui muitas operações de:
- Cadastro de paciente (insert/update PACIENTE, FIA, BAA)
- Cópia/draft de eDoc via tabelas TMP (`EDOC_DOCUMENTO_ITEM_TMP`, `EDOC_RODAPE_TMP`, `EDOC_CABECALHO_TMP`)
- Migração/install: `INSERT INTO modulo_processo`, `grupo_acesso_processo`, `funcionario_processo` para registrar permissões
- HL7: `F_MSH`/`F_PID`/`F_PV1` geram segmentos
- Custo médio MATMED via `F_RECALCULAR_CM` ↔ logs em `LOG_FECHAMENTO_MENSAL_UNITARIO`

**Aplicação prática**: arquivos `pbd_*_sql.txt` em `capturas/` (gitignored — podem ter binds/literais com PII) servem como **dicionário completo de queries do sistema** — útil pra encontrar a query exata de qualquer tela do desktop antes de reproduzir no React.

### 14.1 Catálogo cruzado: tabela ↔ módulos (`capturas/catalogo_pbd.md`)

Processei os 18 arquivos `pbd_*_sql.txt` e construí um **catálogo cruzado** (`scripts/catalogo_sql_pbd.py`):

- **2.478 tabelas distintas** mencionadas no app PowerBuilder
- Total de comandos extraídos: **41.787 SELECT, 60.571 INSERT, 7.386 UPDATE, 2.102 DELETE** (~111.8k comandos)
- Módulos mais "escritores" (INSERT-heavy): **Portal** (15.893 INSERTs, principalmente migrations/install), **Cadastros.Gerais** (11.626), **Suprimentos** (7.329), **Faturamento.SUS** (6.694), **Financeiro** (4.414).
- Módulos mais "leitores" (SELECT-heavy): Faturamento.SUS (6.981), Financeiro (5.798), Ambulatorio.SUS (5.630), Cadastros.Gerais (5.611), Estatistica (5.571).

### 14.2 Tabelas core (mencionadas em 11+ módulos)

Tabelas que aparecem em quase todos os módulos = **core do sistema**:

| Tabela | # módulos | Função |
|---|---:|---|
| `HOSPITAL` | 17 | Hospital atual (multi-tenant) |
| `INFOSAUDE` | 17 | Schema owner (refs via `INFOSAUDE.X`) |
| `FUNCIONARIO_PROCESSO` | 16 | **RBAC** — quem pode fazer o quê em cada módulo |
| `MODULO` | 16 | Lista de módulos do sistema |
| `FUNCIONARIO` | 15 | Profissionais do hospital |
| `GRUPO_ACESSO_PROCESSO` | 15 | **RBAC** — perfis × processos |
| `MODULO_MENU` | 14 | Itens de menu do módulo |
| `PROCEDIMENTO` | 14 | Catálogo procedimentos |
| `BAA` / `FIA` / `FIA_LEITO` | 13 | Atendimento ambulatório / internação / leito |
| `PACIENTE` / `MEDICO` | 13 | Pessoas |
| `PLANO_SAUDE` | 13 | Convênios |
| `PRESCRICAO_FIA` / `SOL_EXAME` | 13 | Prescrição / exames |
| `UNIDADE_HOSPITALAR` | 13 | Unidades funcionais |
| `EDOC_MOVIMENTO_ITEM_LISTBOX` | 13 | Respostas de listbox em eDocs |
| `COMPOSICAO_ADTO_PACIENTE` | 13 | Adiantamentos do paciente |
| `VALIDA_ERRO` | 13 | Tabela de validação (helper) |
| `MODULO_PROCESSO` | 13 | **RBAC** — processos por módulo |
| `MODULO_GRUPO_ACESSO` | 11 | **RBAC** — perfis por módulo |
| `LAUDO` / `CABECALHO_LAUDO` | 12/11 | Laudos |
| `MATMED` (+`_HOSPITAL`, `_DIA`, `_MOV`, `_MOV_CC`) | 11-12 | Catálogo material/medicamento + estoque |
| `AIH` / `LOTE_FATURAMENTO` / `CREDENCIADO` | 11 | Faturamento SUS |
| `FORNECEDOR` | 11 | Suprimentos |
| `PARAMETRO_HOSPITAL` / `PARAMETRO_PRONTINTAMB` | 11 | Parâmetros (per-hospital) |
| `PROCESSO_AUDITORIA` / `PROCESSO_LOG` | 11 | Auditoria |
| `TISS_VERSAO_XML` | 11 | TISS (planos privados) |
| `CID` | 11 | CID-10 |
| `CENTRO_CUSTO` / `CIRURGIA` / `CONSELHO_REGIONAL_CATEGORIA` | 11 | Estrutura organizacional |
| `RECURSO` / `STATUS_DEM_PAGAMENTO` / `TIPO_QUARTO` / `UNIDADE_MEDIDA` | 11 | Faturamento/Estoque |
| `H_PACIENTE` / `H_PLANO_SAUDE` | 11-12 | Histórico (h_ prefix) — auditoria de mudanças em PACIENTE/PLANO_SAUDE |
| `MEDICO_ASSINATURA` / `MEDICO_ESPECIALIDADE` | 11 | Vínculos de médicos |
| `SALUX_UTIL_EXCEL` | 14 | Helper interno pra exportar para Excel |
| `ALL_TAB_COLUMNS` / `ALL_TAB_COLS` | 14/12 | **Meta-queries** — Salux introspecta schema em runtime |

### 14.3 Modelo RBAC do Salux

A combinação `MODULO` + `MODULO_PROCESSO` + `GRUPO_ACESSO_PROCESSO` + `FUNCIONARIO_PROCESSO` + `MODULO_GRUPO_ACESSO` + `MODULO_MENU` aparecendo em quase todos os módulos confirma o RBAC do Salux:

```
MODULO (lista de módulos do sistema, ex: 'Cadastros Gerais', 'Ambulatorio SUS')
  ├──< MODULO_MENU (entradas de menu)
  ├──< MODULO_PROCESSO (processos = ações: CGVI079_T, etc — com `operacao_vinculada`, `in_logar`)
  │       ├──< GRUPO_ACESSO_PROCESSO (perfil pode executar)
  │       └──< FUNCIONARIO_PROCESSO (funcionário pode executar — com `id_inclusao`, `id_alteracao`, `id_exclusao` flags)
  └──< MODULO_GRUPO_ACESSO (perfis por módulo)
```

Cada **processo** tem flags `id_inclusao`, `id_alteracao`, `id_exclusao` (S/N) — granular: pode incluir / alterar / excluir cada entidade.

Migrations em PBDs incluem inserts pra esse RBAC (registrar processos novos ao instalar/atualizar módulos).

### 14.4 Padrão `H_*` — histórico (audit log via shadow tables)

Tabelas `H_PACIENTE`, `H_PLANO_SAUDE`, `H_LEITO_CENTRAL`, `H_CEM_DOC_HOSPITAL` aparecem espalhadas — **shadow tables que guardam histórico de alterações** das tabelas-mães. Padrão clássico de audit log via trigger.

### 14.5 Padrão `TU_*` — Tabelas Únicas (SUS oficial)

`TU_BPA_UNIFICADA`, `TU_PROCED_SIA`, `TU_CBO`, `TU_PROC_SIA_X_*`, `TU_PROCED_SIA_X_INSTR_REGISTRO`, `TU_SUBGRUPO_PROCEDIMENTO_SUS` — **`TU_*` é prefixo para Tabelas Únicas oficiais do SUS** (importadas via DATASUS). Compartilhadas entre instâncias.

---

## 15. Pendências (ainda)

- [ ] FKs reais via `dba_constraints` — estavam em retry no momento do snapshot deste doc
- [ ] Outros módulos PBD ainda não extraídos (Agenda, APAC, Faturamento.SUS, Financeiro, Gestao.Glosas, Portal, Suprimentos)
- [ ] Mapeamento de cd_modulo ↔ binário ↔ nome da tela
- [ ] HL7 export — explorar onde mensagens são enfileiradas (procurar tabela HL7_OUTBOX/HL7_QUEUE)
- [ ] Estudar `MODULO_PROCESSO` + `GRUPO_ACESSO_PROCESSO` + `FUNCIONARIO_PROCESSO` — modelo de RBAC do Salux

---

## 14. Arquivos gerados nesta investigação

| Arquivo | Conteúdo |
|---|---|
| `capturas/edoc_modelos.txt` | 333 modelos eDoc listados |
| `capturas/estatisticas.txt` | Top tabelas, top modelos, sessões por module |
| `capturas/setor_fluxo.txt` | 73 setores completos |
| `capturas/snapshot_sql_1h.jsonl` | 134 SELECTs reais SYBSA da última 1h (JSONL) |
| `capturas/analise_snapshot.txt` | Tabelas mais usadas + co-ocorrências (JOINs) + top execs |
| `capturas/query_lista_setor.txt` | Query completa da lista de pacientes por setor |
| `capturas/queries_edoc_movimento.txt` | Queries que mexem em EDOC_MOVIMENTO |
| `capturas/queries_evolucao.txt` | Queries de EVOLUCAO_PACIENTE_FIA + parâmetros |
| `capturas/queries_alergia.txt` | Queries de alergias |
| `capturas/queries_exame.txt` | Queries de SOL_EXAME (solicitação) + LAUDO |
| `capturas/queries_prescricao_fia.txt` | Query completa de itens de prescrição |
| `capturas/queries_triagem.txt` | (vazio) |

---

_Compilado por leitura read-only de DBA_TABLES, DBA_TAB_COLUMNS, V$SESSION, V$SQL, V$SQLTEXT e algumas SELECTs de teste. Nenhum write em PROD._
