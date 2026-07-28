---
name: tratar-indicador-hmcml
description: Bootstrap para TRATAR e TESTAR, um a um, os indicadores contratuais do HMCML Maricá 2026 (módulo Indicadores, ADR-0022, ModuloPermissao 44). Use SEMPRE que o usuário abrir uma sessão dizendo "vamos tratar o indicador X", "item N", nomear um indicador da planilha (ex.: "tempo médio para acolhimento", "taxa de ocupação", "mortalidade institucional", "NPS", "densidade de queda"), falar em "repassar/validar/testar indicador", "motor do indicador", "apurar contra o Salux", ou pedir a evidência (boletins incluídos/rejeitados) de um indicador. Dá o contexto todo: catálogo em prod, banco de testes Oracle (Salux/HMCML), marcos do modelo de dados, decisões já tomadas e o fluxo tratar→testar→gravar. NÃO é para indicadores de UPA/Santa Rita (só HMCML, hospital 1).
---

# Tratar indicador do HMCML 2026 — um a um

O usuário está **repassando os 108 indicadores contratuais do HMCML** (planilha "Indicadores HMCML
Maricá 2026"), tratando o motor SQL de cada um e conferindo o número contra dado real. Ele trabalha
**um indicador por vez**, muitas vezes em **sessões separadas** do Claude — por isso esta skill.
Objetivo dele em cada item: **eliminar ruído de dados** e **poder confirmar o número na tela**
(evidência dos boletins que entraram e dos que foram rejeitados, por motivo).

Contexto de fundo: módulo **Indicadores** ([ADR-0022](../../docs/adr/0022-indicadores-motor-sql-cadastravel.md)),
`ModuloPermissao 44`, **EM PROD desde 2026-07-24**. O motor de cada indicador é **SQL guardado no
cadastro** (não Python), versionado a cada gravação, executado read-only. Só **HMCML (hospital 1)**;
UPA/Santa Rita ficam fora. Memória relacionada: `project_indicadores_hmcml_2026`, `secretario-pwa`.

## Regras inegociáveis

1. **PRODUÇÃO — gravar só com OK explícito por ação.** Testar é 100% leitura e pode. Gravar o
   `sql`/`ressalva`/`situacao` no catálogo é **escrita em prod** → só com "pode gravar" explícito.
2. **Nunca inventar número.** Boletim sem o carimbo necessário é **rejeitado com motivo**, não
   forçado no cálculo. O usuário QUER ver os rejeitados.
3. **Ser honesto na ressalva.** Cobertura, campo morto, divergência com a planilha — tudo à mostra.
4. **Competência fechada para testar.** Use um mês fechado (ex.: `2026-06-01` a `2026-07-01`), nunca
   o mês corrente.

## 1. O catálogo em produção (Postgres) — o que já existe

Entidade `smsmarica.indicador` (colunas **snake_case**). Ler daqui **antes** de tratar qualquer item —
mostra o SQL atual, a situação e a ressalva já escritos.

```python
import json, psycopg2
sec = r"C:/Users/berna/AppData/Roaming/Microsoft/UserSecrets/smsmarica-api-dev/secrets.json"
cs = json.load(open(sec, encoding="utf-8-sig"))["ConnectionStrings:DefaultDb"]
d = dict(p.split('=',1) for p in cs.split(';') if p)
conn = psycopg2.connect(host=d['Host'], port=d['Port'], dbname=d['Database'],
                        user=d['Username'], password=d['Password'], sslmode='require')
# aba: 1=Adulto 2=Pediatrico 3=MaternoInfantil 4=PerfilEpidemiologico 5=Institucional
# ler um indicador: numero é rótulo textual ("1","3.1","10.4")
cur = conn.cursor()
cur.execute("""select numero,nome,memoria_calculo,fonte_declarada,meta,meta_operador,meta_valor,
  meta_valor_maximo,pontuacao,tipo_resultado,unidade_medida,fator_densidade,situacao,ressalva,sql
  from smsmarica.indicador where aba=%s and numero=%s and excluido_em is null""", (1,'1'))
```

Enums (valor inteiro persistido — ver `SMSMarica.Data/Entities/Enums/IndicadorEnums.cs`):
- **situacao**: 1=Validado · 2=NaoValidado · 3=SemMotor · 4=ForaDoBanco
- **tipo_resultado**: 1=Razao · 2=Densidade(×fator) · 3=Absoluto · 4=Distribuicao · 5=Agrupador · 6=Media
- **meta_operador**: 1=≤ · 2=≥ · 3=< · 4=> · 5== · 6=Entre

Contrato do SQL por tipo: **Razao/Densidade** → 1 linha `numerador,denominador`; **Media** → 1 linha
`valor,denominador`; **Absoluto** → `numerador`; **Distribuicao** → N linhas `rotulo,quantidade`;
**Agrupador** → sem SQL (soma filhos). Parâmetros nomeados: `:hospital`, `:ini`, `:fim`.

⚠️ **Tempo médio deve ser `Razao` (numerador=`ROUND(SUM(minutos),2)`, denominador=`COUNT(*)`), NÃO `Media`.**
A planilha define Numerador = Σ do tempo (min) e Denominador = nº de pacientes; `Media` (`AVG`) esconde o
numerador e — pior — quebra o **trimestre**, que é `Σnum/Σden` e NÃO a média dos meses. Sem o numerador
guardado, não dá pra fechar o trimestre certo. (Erro cometido e corrigido em 28/07 nos indicadores 1,2,3.3-3.5.)

Estado inicial do catálogo (jul/2026): 108 indicadores — 39 Validado, 14 NaoValidado, 35 SemMotor,
20 ForaDoBanco. A planilha-fonte fica em `Salux/Planilhas Indicadores HMCML Maricá 2026 (Unidade).xlsx`
(ler com `openpyxl`, `data_only=False` p/ ver as fórmulas; 5 abas). Regras das fórmulas: resultado é
sempre `num/den`; densidade ×1.000; pontuação é tudo-ou-nada; trimestre é `Σnum/Σden` (não média dos
meses); percentual é fração (30% = 0,30).

## 2. O banco de testes (Oracle do Salux/HMCML) — como apurar

Roda pelo **proxy-sql interno** do droplet (mesmo caminho da produção, slug `salux-hcml`, **read-only**).
Alimente o SQL por **stdin/heredoc** (evita inferno de aspas) e **substitua os binds por literais**
(`:hospital`→`1`, `:ini`→`DATE '2026-06-01'`, `:fim`→`DATE '2026-07-01'`):

```bash
ssh -o BatchMode=yes -i ~/.ssh/id_ed25519_smsmarica root@smsmarica.online '/root/pq.sh salux-hcml' <<'SQL'
SELECT COUNT(*) FROM infosaude.baa b WHERE b.cd_hospital=1
  AND b.dt_chegada >= DATE '2026-06-01' AND b.dt_chegada < DATE '2026-07-01'
SQL
```

Acesso SSH: `reference_ssh_droplet_smsmarica` (chave `~/.ssh/id_ed25519_smsmarica`). Proxy: `reference_proxy_sql_interno`.

**Armadilhas do proxy/Oracle (todas já pisadas):**
- O guard **bloqueia `;` interno** ("múltiplos statements") — uma consulta por vez, sem `;` no meio.
- **`LEFT JOIN infosaude.lista_senha` estoura timeout** (ORA-01013). Se precisar da senha, use
  **INNER JOIN** (rápido) ou `NOT EXISTS`/subconsultas escalares `FROM dual`.
- Linha em branco no meio do SQL encerra o statement no sqlplus — o proxy não sofre disso, mas evite.
- `pq.sh` só manda 1 consulta; para vários números numa ida, junte com CTE + agregações condicionais.

## 3. O modelo de dados clínico do Salux (os "marcos" de um atendimento)

Tudo gira em torno do **boletim de atendimento** `INFOSAUDE.BAA` (PK `NR_BAA, DT_ANO_BAA, CD_HOSPITAL`;
**não existe `CD_ATENDIMENTO`**). A linha do tempo de um paciente da emergência, com o carimbo de cada marco:

| Marco | Coluna | Observações críticas |
|---|---|---|
| **Chegada** (totem) | `LISTA_SENHA.DT_HR_SENHA` | 100% preenchida. Chega via `BAA→ACOLHIMENTO→LISTA_SENHA` pela chave (`cd_hospital_senha, cd_sala_painel, dt_senha, cd_tipo_senha, nro_senha`). **`ACOLHIMENTO.DT_HR_SENHA` é 100% NULO** (campo morto); **`BAA.DT_SENHA` é só data** (hora 00:00). |
| **Acolhido** | `ACOLHIMENTO.DT_ATENDIMENTO` | 100% com hora real (inclusive p/ quem não tem senha). É o "foi acolhido". Sempre ≤ cadastro. |
| **Cadastrado** | `BAA.DT_CHEGADA` | 100%. Abertura do boletim. ≈ `BAA.DT_ATENDIMENTO`. Vem ~9 min depois do acolhido. |
| **Classificado** | `BAA.DT_CLASSIFICA_ATUAL` | ~89% do total; ~100% de quem tem senha. Cor em `BAA.CD_CLASSIFICACAO_RISCO` → `CLASSIFICACAO_RISCO.DS_CLASSIFICACAO_RISCO`. |
| **1º documento** | `MIN(EDOC_MOVIMENTO.DT_INCLUSAO)` | "Tudo que é documento no Salux é um eDoc" (evolução, receituário, atestado…). Liga ao boletim por `NR_BAA + DT_ANO_BAA + BAA_CD_HOSPITAL`. Também tem `DT_INCLUSAO_UTC` (timestamp c/ TZ). |

**Recorte ADULTO** (indicadores da aba Adulto): `NVL(b.cd_setor,-1) NOT IN (38,46,50,60,71,39,48,51,67)`
(exclui pediatria 38,46,50,60,71 e obstetrícia 39,48,51,67; `cd_unidade` vem nulo no BAA). Bloco
pediátrico/materno usa o inverso. Corte alternativo por especialidade diverge ~1,4% no lado adulto.

**Excluir vermelho** (indicadores 1–3 "exceto vermelho"):
`NVL(b.cd_classificacao_risco,-1) NOT IN (SELECT cr.cd_classificacao_risco FROM infosaude.classificacao_risco cr WHERE UPPER(cr.ds_classificacao_risco)='VERMELHO')`.

Campos MORTOS (não usar): `BAA.DT_INICIO/FIM_ATEND_MED` (0%), `ACOLHIMENTO.DT_HR_SENHA` (0%),
`FIA.NR_OBITO`/`NR_DIAS_INTERNACAO` (0%). Internação usa `FIA_LEITO.CD_UNIDADE`.

## 4. Decisões já tomadas — a "Cadeia de Entrada" (indicadores 1, 2, 3 · Adulto U&E)

**Modelo definitivo do usuário (pontos implícitos no pedido, é SEQUÊNCIA aninhada — #2 só existe se
houver #1; #3 só se houver #2):**

```
SENHA ──#1──► ACOLHE ──#2──► CLASSIFICA ──#3──► 1º DOCUMENTO (qualquer)
```

- **#1** (tempo p/ acolhimento/cadastro, meta ≤5 min): `ACOLHIMENTO.DT_ATENDIMENTO − LISTA_SENHA.DT_HR_SENHA`.
  População: adulto, exceto vermelho, **com senha**. Rejeita quem tem **acolhido antes da senha**
  (é 2ª senha / carimbo herdado — ~2.035 de 8.806 em jun/2026). Jun/2026: **7,83 min**, n=6.771.
- **#2** (tempo p/ classificação, meta ≤10 min): `BAA.DT_CLASSIFICA_ATUAL − ACOLHIMENTO.DT_ATENDIMENTO`,
  sobre a coorte válida do #1. Jun/2026: **14,07 min**, n=6.771 (sem perda; todo #1-válido tem classificação).
- **#3** (espera p/ atendimento médico, meta POR COR): fim = **1º Boletim Médico**
  (`EDOC_MOVIMENTO.CD_MODELO IN (10036,10014,10232)` — o mesmo marcador do Painel do Secretário; o 1º
  documento clínico É o atendimento em 81% dos casos). `MIN(DT_INCLUSAO desses modelos) − início`,
  **quebrado por cor** (3.1 Vermelho … 3.5 Azul), incluindo vermelho. Início: para laranja/amarelo/verde/azul
  = `DT_CLASSIFICA_ATUAL`; **vermelho tem relógio próprio (da chegada, meta 0)**. Metas: vermelho 0,
  laranja ≤10, amarelo ≤60, verde ≤120, azul ≤240 min. Coorte do #3 exige classificação + boletim médico
  (inclui vermelho, então NÃO é subconjunto estrito do #2). Rejeitar `sem documento` (~393 sem boletim
  médico em jun/26) e `pesquisa de satisfação`/`declaração` (não são documento de atendimento). **A TESTAR
  por cor** (túnel Oracle caiu no meio; 1º-doc geral já confirmado ~49 min após classificação).

Divergência consciente com a planilha: a planilha corta #1/#2 no **cadastro** (aí #1=12,69 falha, #2=6,94
passa); o usuário corta no **acolhido** (#1=7,83 falha, #2=14,07 falha). O total senha→classificação
(~19,6 min) é o mesmo; muda só onde a fronteira cai. **Ainda NÃO gravado em prod** (usuário quer gravar
1+2+3 juntos, só depois de validar o #3).

## 5. A evidência clicável (feature pendente, "implementar depois")

Padrão pedido para TODO indicador: ao clicar no número, abrir tela com **dois tabs** —
**(a) Incluídos**: os boletins que compõem o número, com PII (nome, nº boletim, os carimbos, os minutos
calculados) = a prova; **(b) Rejeitados por motivo**: cada boletim fora + o porquê
(`sem senha de chegada`, `acolhido antes da senha`, `sem classificação`, `sem documento`, `vermelho`…).
É um **drill-down novo** (endpoint que devolve linhas, não só o agregado). Decisão do usuário:
**anotar como padrão e implementar numa leva própria**, depois de repassar os indicadores. Para indicadores
de tempo-médio, a tela de detalhe também deve mostrar **distribuição por faixa + funil de cobertura**.

## 6. Fluxo por indicador (o loop tratar→testar→gravar)

1. **Ler o pacto**: `numero, nome, memoria_calculo, meta, fonte_declarada` da planilha/catálogo, lado a
   lado com o `sql`, `situacao`, `ressalva` atuais de prod.
2. **Tratar**: escrever/ajustar o SQL seguindo a memória de cálculo + os marcos da §3 + a régua de ruído
   (guardas de negativo, recorte de setor, exclusão de vermelho quando aplicável).
3. **Testar**: rodar via `pq.sh salux-hcml` num mês fechado. Sempre trazer, além do número:
   **denominador**, **negativos/ruído**, **funil de cobertura** (quem entra, quem fica de fora e por quê).
4. **Decidir** situação (Validado só se conferido) + ressalva honesta + `% na meta`.
5. **Gravar** (só com OK explícito): `UPDATE smsmarica.indicador SET sql=…, ressalva=…, situacao=…,
   atualizado_em=now(), atualizado_por=… WHERE id=…` — e **versionar** em `indicador_versao` antes de
   sobrescrever o SQL (a app faz isso via `AtualizarAsync`; se gravar SQL direto, replicar o versionamento).
   Conferir depois com `select count(*) from smsmarica.indicador where situacao=1`.

## 7. Registro de tratativas (atualizar a cada sessão)

| Indicador | Status | Número (jun/26) | Decisão / pendência |
|---|---|---|---|
| #1 Adulto — Tempo p/ acolhimento/cadastro | ✅ **GRAVADO EM PROD** (2026-07-28, sobrescrita direta sem versionar) | 7,83 min (n=6.771) | senha→acolhe. Corte no acolhido; rejeita 2ª senha. Falha ≤5. |
| #2 Adulto — Tempo p/ classificação | ✅ **GRAVADO EM PROD** | 14,07 min (n=6.771) | acolhe→classifica. Falha ≤10 (divergência consciente c/ planilha, que mede do cadastro → 6,94 bate). |
| #3 Adulto — Espera p/ atendimento médico (por cor) | ✅ **GRAVADO EM PROD** | Amarelo 23,3 · Verde 54,1 · Azul 54,3 | classifica→1º Boletim Médico (10036/14/232). 3.3/3.4/3.5 BATEM (≤60/120/240), Validado. **3.1 Vermelho e 3.2 Laranja → ForaDoBanco** (laranja não existe no HMCML; vermelho é imediato por protocolo, doc é lag de registro). |

### Pediátrico (aba 2) — Cadeia de Entrada — ✅ GRAVADO EM PROD (2026-07-28)
Mesma estrutura do adulto, **sem o #1** (a pediatria NÃO usa totem: 2 senhas em 4.443). Boletim médico
é o mesmo (10036/14/232). Setor pediátrico = `cd_setor IN (38,46,50,60,71)`.
| Sub | Situação | jun/26 | Meta |
|---|---|---|---|
| #1 senha→acolhe | **ForaDoBanco** (sem totem) | — | ≤5 |
| #2 acolhe→classifica (Razão, sem senha) | Validado | 16,42 min (n=3.970) | ≤10 ✗ |
| 3.1 Vermelho / 3.2 Laranja | **ForaDoBanco** | 2 casos / não existe | |
| 3.3 Amarelo · 3.4 Verde · 3.5 Azul | Validado | 25,6 · 46,4 · 34,5 | ✓✓✓ |

### Materno Infantil (aba 3) — ✅ GRAVADO EM PROD (2026-07-28)
**A classificação da maternidade NÃO é `DT_CLASSIFICA_ATUAL`** (esse ≈ acolhimento; dava o #2 falso de
1,21 min). É a tela de triagem própria, o **eDoc `cd_modelo=10043` "Classificação de Risco Maternidade"**
(91% das BAA de maternidade têm; fica ~46 min depois do DT_CLASSIFICA_ATUAL). ⚠️ Vale SÓ p/ maternidade:
adulto usa 10043 em ~0% (19/12.034) e pediatria em 0% — esses classificam no `DT_CLASSIFICA_ATUAL`.
- **O 10043 TEM cor**: o item `EDOC_ITEM.DS_ITEM='Classificação de Risco'` (resposta em
  `EDOC_MOVIMENTO_ITEM.DS_RESPOSTA`) traz as 5 cores com alvo próprio: Vermelho-Imediato, **Laranja-15min**
  (a maternidade TEM laranja, ≠ adulto/ped), Amarelo-30min, Verde-60min, Azul-120min. **A meta que pontua
  é a da planilha** (≤10/60/120/240), não o alvo interno.
- Padrão de join p/ tirar classificação+cor por BAA: `EDOC_MOVIMENTO mv (cd_modelo=10043)` → `EDOC_MOVIMENTO_ITEM mi`
  (por cd_hospital/ano_movimento/id_movimento/cd_modelo/cd_documento) → `EDOC_ITEM it (ds_item='Classificação de Risco')`;
  `ROW_NUMBER() ... rn=1` p/ o 1º 10043 do boletim. Boletim médico = mesmos 10036/10014/10232.
- **#1** senha→acolhe: ForaDoBanco (sem totem, 64/928). **#2** acolhe→10043: **46,52 min** (n=683, exceto
  vermelho), falha ≤10. **3.1 Vermelho**: ForaDoBanco (4 casos, imediato/doc-lag). **3.2 Laranja**: 21,17 (4
  casos, falha ≤10). **3.3 Amarelo 23,4 · 3.4 Verde 32,4 · 3.5 Azul 42,2**: BATEM (≤60/120/240).

### Sweep de habilitação (2026-07-28) — padronização
Regra do usuário: **o que não temos dado nem como comprovar fica DESABILITADO** (`ativo=false`) — aparece
esmaecido só com o nome (feature "Habilita"). Aplicado em todas as abas: `ativo=false` onde
`situacao=ForaDoBanco(4)` OU (`situacao=SemMotor(3)` E **não** for agrupador `tipo_resultado=5`);
`ativo=true` onde `situacao in (Validado,NaoValidado)` OU for agrupador. Estado 28/07: 41 Validado + 5
NaoValid + 4 agrupadores habilitados; 33 ForaDoBanco + 25 SemMotor desabilitados (108 total).

### Internação Adulto (aba 1, #11–#26) — ✅ REPASSADO E HONESTO (2026-07-28)
Regra aplicada: cada indicador tem de ser **honesto, validado e apurável** — onde não dá pra validar
contra dado real, **descarta-se a query** e marca-se ForaDoBanco com ressalva.
- **Validados (apuráveis):** #12/#13 permanência (`dt_alta−dt_baixa`, agora **Razão**; 5,94 / 9,36 dias),
  #14 reinternação, **#17 mortalidade** (`cd_mot_cobranca_sus IN (41,42,43)` = óbito SUS 100% preenchido,
  corroborado por `paciente.dt_obito` 55/55; institucional = >24h; ~12–15%/mês, acima da meta 10%).
- **Descartados (query removida → ForaDoBanco):** **#15 "PCR"** (não existe registro de PCR; contar óbito
  dá 98,6/1.000 vs meta 1 — enganoso), **#24/#25** (questionário de internação eDoc **10208 morto desde
  mai/2026**; #25 usava o form errado do ambulatório 10209).
- **ForaDoBanco (fonte externa):** #16 ITU (CCIH), #18 TEV, #19 UPP, #20 flebite, #21 quedas, #22 pulseiras,
  #23 checklist, #26 NPS (sem pergunta 0–10).
- **#11 Ocupação: NaoValidado — PENDENTE decisão de contrato** sobre o conjunto de leitos do denominador
  (com todo inventário 50,6% × só enfermarias 62,4%; a base não tem flag de tipo de unidade). Em qualquer
  variante não bate 85%. Motor existe e é coerente; falta a direção definir os leitos. `leito.id_condicao='A'`
  = operacional; exclusão pediátrica/materna = unidades 10,13,30,31,32,33,34,15,16,17,26.

**Pendências gerais (não fazer sem pedir):** (a) a **evidência clicável** (incluídos + rejeitados por
motivo) segue não implementada — é feature de UI/endpoint; (b) rodar a **apuração** (ApurarAsync) de
jun/2026 na app pra o painel exibir os números novos — os SQLs estão gravados, mas a execução é sob demanda;
(c) **Maternidade (aba 3)** conforme diagnóstico acima.

> Ao fechar/gravar um indicador, **atualize esta tabela** para a próxima sessão não recomeçar do zero.
