# Consultas das UPAs 24h (SQL Server)

Cobre as **duas** UPAs do painel, que rodam o mesmo HIS em instâncias separadas. As bases estão
no cadastro do smsmarica e são alcançadas pelo **proxy SQL interno** (loopback 5091 + token) — o
painel manda slug + SQL e o smsmarica resolve o transporte, que aqui é o agente WSS reverso
(ADR-0023). O painel **não** tem credencial nem driver de SQL Server.

| unidade | slug | `unid_codigo` | servidor | `unid_descricao` |
|---|---|---|---|---|
| UPA 24h Maricá (CNES 7164440) | `upa24h-marica-sqlserver` | `0006` | `UPA-SRV` | UPA MARICA |
| UPA 24h Santa Rita | `santarita-marica-sqlserver` | `0007` | `STARITA-SRV` | UPAM SANTA RITA |

> **As duas bases se chamam `UPA24H`.** `DB_NAME()` devolve o mesmo nome nas duas, e cada uma tem
> **uma única linha** em `Unidade` — não dá para descobrir de que unidade é o dado pelo nome do
> banco, só pelo slug que se usou para chegar lá. Foi o que quase induziu ao erro de achar que
> Santa Rita era outro `unid_codigo` dentro da base da UPA.

- **Servidor**: SQL Server 2014 SP3 Enterprise (12.0.6444.4) nas duas. `PERCENTILE_CONT`,
  `DATEFROMPARTS` e `ROW_NUMBER` estão disponíveis.
- **Relógio**: `GETDATE()` é horário de Brasília (conferido contra o droplet: 22:52:51 local =
  01:52:51 UTC). As expressões de período são as mesmas do Conde, traduzidas para T-SQL.
- **Volume** (30 dias até 24/07/2026): UPA Maricá 13.242 boletins (441/dia); Santa Rita 7.876
  (262/dia, com histórico desde 2006).
- Transcrição em código: [`back/Painel/ConsultasUpa.cs`](../back/Painel/ConsultasUpa.cs) — as
  consultas são as MESMAS, parametrizadas pelo `unid_codigo`. Validado nas duas bases em 25/07/2026.

## O caminho do dado

```
Pronto_Atendimento          boletim (spa_codigo, spa_chegada, unid_codigo)   ← o "BAA" daqui
  └─ UPA_ACOLHIMENTO        acolhimento 1:1 com o boletim (ACO_CODIGO)       ← 100% dos boletins
       └─ UPA_Classificacao_Risco   triagem (upaclaris_datahora, risaco_codigo)  ← 91,1%
            └─ risco_acolhimento    cadastro da cor (risaco_descricao, meta)
  └─ atendimento_ambulatorial       atendimento médico (atendamb_datainicio/datafinal) ← 96,2%
       └─ UPA_Atendimento_Medico    desfecho (tipsai_codigo → Tipo_Saida)
  └─ UPA_Fila                       fila viva (DATA_CANCELAMENTO marca quem evadiu)
```

## Três armadilhas que definem as consultas

### 1. `risaco_gravidade` é relativa ao protocolo, não uma escala global

O cadastro `risco_acolhimento` tem **três protocolos** (`proate_codigo` 0002, 0005 e 0006) e a
mesma gravidade significa coisas diferentes em cada um:

| protocolo | gravidade 5 | gravidade 6 | gravidade 7 |
|---|---|---|---|
| 0002 | Laranja | Amarelo/Observação | Vermelho |
| 0005 | Amarelo/Observação | Laranja/Consultório | Laranja/Observação |
| 0006 | Laranja/Consultório | Laranja/Observação | Vermelho |

Por isso **a cor sai de `risaco_descricao`**, no join por `risaco_codigo` — que é único entre os
protocolos (0005–0010, 0100–0106, 0107–0112). A UPA usou dois protocolos na mesma janela de 30
dias (0005 no grosso, 0006 no restante), então fixar um protocolo também não serviria.

### 2. Não existe "data de saída" no boletim — quem evadiu parece que ainda espera

O Salux tem `BAA.dt_saida`; aqui não há equivalente. Sem filtro, em 24/07 havia 18 boletins das
últimas 12h sem atendimento médico — e os 13 com mais de 3h de espera estavam **todos cancelados
na `UPA_Fila`**. O painel mostraria uma fila 5× maior que a real.

O filtro certo é `EXISTS (… UPA_Fila WHERE DATA_CANCELAMENTO IS NULL)`. Com ele, o retrato bate
com a operação:

| chegada | chegaram | já viram médico | aguardando na fila | fora da fila (evadiu) |
|---|---|---|---|---|
| última hora | 12 | 8 | **3** | 1 |
| 1h atrás | 9 | 9 | 0 | 0 |
| 2h atrás | 16 | 16 | 0 | 0 |
| 3h atrás | 23 | 21 | 0 | 2 |

`EXISTS` e não `JOIN`: a `UPA_Fila` tem reentradas do mesmo boletim (514 linhas para 474
boletins) e um join duplicaria pacientes na contagem.

### 3. As UPAs não internam — e não têm maternidade

`Internacao` **parou de ser alimentada em 25/01/2026**: 972 linhas no total, zero nos últimos 30
dias. `UPA_CONSOLIDACAO_PACIENTE_OBSERVACAO` (16 linhas) referencia internações dessa mesma safra
morta e não tem coluna de data — não serve como "em observação agora".

Por isso a unidade manda `internacoes` e `maternidade` **nulas** no contrato, e o front some com
as seções. Zero seria mentira: diria que a UPA não internou ninguém, quando ela não interna.

Também não serve `Atendimento_Emergencia`, apesar do nome: o join por `emer_codigo` a partir de
`Pronto_Atendimento` devolve **zero linha** — nas duas bases. Quem carrega o relógio do
atendimento médico é `atendimento_ambulatorial`.

### Cobertura das ligações (30 dias até 24/07/2026)

| | UPA Maricá | Santa Rita |
|---|---|---|
| boletins | 13.242 | 7.876 |
| com acolhimento | 100% | 100% |
| com classificação de risco | 91,1% | 99,3% |
| com atendimento médico | 96,2% | 98,4% |
| com `Atendimento_Emergencia` | **0** | **0** |

## U1 — Agora (tick rápido, 60 s)

Janela de 12h, a mesma do Conde.

**U1a — aguardando médico por cor**: boletim sem `atendamb_datainicio` e ainda na fila não
cancelada, agrupado pela primeira classificação de risco. Devolve os **dois tempos** da
jornada, iguais aos do Conde: `min_medio_ate_classificacao` (T1, chegada → classificação,
intervalo fechado) e `min_medio_desde_classificacao` (T2, classificação → agora, relógio
correndo). Três detalhes que mandam no SQL:

- **`CASE`, não `GREATEST`** — a função só existe no SQL Server 2022 e estas bases são
  **2014**. O `CASE` faz o mesmo clamp de classificação anterior à chegada.
- **`CAST(... AS float)` antes do `AVG`** — `DATEDIFF` devolve `int` e `AVG` de `int` no
  SQL Server é **divisão inteira**: sem o cast, uma média de 28,8 min vira 28.
- **Quem não tem classificação nenhuma fica com T1/T2 nulos**, de propósito. Não é caso
  raro aqui: em 25/07 às 16h a UPA Maricá tinha **20 pessoas sem nenhuma linha de
  classificação** — essas esperam a TRIAGEM, não o médico, e o chip do front cai no texto
  "na fila há X · sem classificação" em vez de fingir um tempo que não existe.

O marcador de atendimento **não** precisou ser ampliado como no Conde: aqui
`atendimento_ambulatorial` já cobre 96–98% dos boletins e não há segunda fonte equivalente
mapeada nesta base.

**U1b — em atendimento + atendimentos de hoje**: `dt_med` preenchido e `dt_fim` nulo; o total do
dia vem na mesma ida ao agente, para não gastar dois round-trips.

## U2/U3/U4 — Movimento

`COUNT(*)` de `Pronto_Atendimento` por `spa_chegada` (o equivalente de `BAA.dt_atendimento`),
série de 35 dias e distribuição por hora do dia corrente. Medido em 24/07/2026:

| período | total | por dia |
|---|---|---|
| junho/2026 | 12.993 | 433,1 |
| julho/2026 (23 dias completos) | 9.821 | 427,0 |
| 24/07 até 22h52 | 384 | — |

## U6 — Espera por cor

Tempo da **classificação de risco** ao **primeiro atendimento médico**, por cor — o mesmo
indicador da Q6 do Conde. `fimDoc` = fim + 3 dias, porque o atendimento pode ser lavrado depois
do fim do período.

### A meta saiu do cadastro (25/07) — e a máquina de "variante predominante" saiu junto

O cadastro (`risco_acolhimento.risaco_Tempo_Espera`) é por **protocolo** e por **variante**
(Consultório tem meta, Observação não), e as unidades divergem entre si e de si mesmas:

| protocolo | | Azul | Verde | Amarelo/Consultório | Laranja/Consultório | Vermelho |
|---|---|---|---|---|---|---|
| 0005 | UPA Maricá | 240 | 120 | 60 | 10 | — |
| 0005 | Santa Rita | 240 | **60** | **30** | (não existe) | — |
| 0006 | ambas | 240 | 120 | 60 | 10 | — |

Santa Rita usa **os dois protocolos ao mesmo tempo** — 30 dias: 4.858 classificações pelo 0005
contra 1.006 pelo 0006 — então lá o mesmo Verde era medido contra 60 ou 120 min conforme a tela
que a enfermagem abriu. E o Conde tinha ainda uma terceira régua (Vermelho 15 · Amarelo 30 ·
Verde 60 · Azul 1440).

Por decisão do usuário, a régua agora é **fixa e única nas três unidades** (protocolo de
Manchester, no back em `MetasTriagem`): **Vermelho 0 (imediato) · Laranja 10 · Amarelo 60 ·
Verde 120 · Azul 240**. Com isso:

- caiu a CTE `modo` e o `espera_com_meta` — existiam só para escolher entre metas que brigavam;
- a **aba Geral voltou a mostrar meta e "% na meta"**, que antes vinham nulos justamente porque
  as réguas divergiam.

### O vermelho tem regra própria

Igual à do Conde: a espera é da **chegada até a primeira interação** (a classificação já conta),
porque no vermelho o médico assiste antes de registrar. Meta zero ⇒ `pct_na_meta` nulo.
Validado em 24/07: UPA Maricá 5 vermelhos, média **4,6 min**.

### Resultado validado (25/07/2026)

UPA Maricá, mês atual:

| cor | pacientes | com atendimento | até triagem | média | mediana | p90 | meta | na meta |
|---|---|---|---|---|---|---|---|---|
| Verde | 7.568 | 7.431 | 18,8 | 37,1 | 28 | 80 | 120 | 97,8% |
| Amarelo | 1.624 | 1.581 | 38,3 | 14,9 | 11 | 29 | 60 | 99,0% |
| Sem classificação | 904 | 0 | — | — | — | — | — | — |
| Vermelho | 69 | 57 | 131,3 | 25,2 | 19 | 59,2 | — | — |
| Azul | 37 | 25 | 36,7 | 31,7 | 16 | 72,2 | 240 | 100% |
| Laranja | 1 | 1 | 8 | 3 | 3 | 3 | 10 | 100% |

Santa Rita, mês atual:

| cor | pacientes | com atendimento | até triagem | média | mediana | p90 | meta | na meta |
|---|---|---|---|---|---|---|---|---|
| Verde | 4.666 | 4.617 | 7,6 | 32,4 | 23 | 70 | 60 | 86,4% |
| Amarelo | 1.189 | 1.166 | 11,9 | 17,5 | 13 | 35,5 | 30 | 85,0% |
| Vermelho | 179 | 174 | 5,3 | 9,7 | 6 | 19 | — | — |
| Sem classificação | 41 | 0 | — | — | — | — | — | — |
| Azul | 2 | 2 | 355 | 11 | 11 | 12,6 | 240 | 100% |

Custo por consulta: 0,7 s (hoje), 1,0 s (mês atual), 1,8 s (mês anterior).

### Ressalvas de qualidade do dado

- **Sem classificação (8,9%)**: boletim sem nenhuma linha de `UPA_Classificacao_Risco`. Sem
  triagem não há de onde contar o tempo, então os tempos vêm nulos e `comAtendimento` é 0 — o
  front escreve "sem triagem registrada / tempo não medido" em vez de "0 de N atendidos", que
  daria a entender que ninguém foi visto. O paciente **nunca** é descartado da contagem.
- **`mediaAteTriagem` do Vermelho** sai alta e sem sentido clínico (131 min no mês atual, 648 em
  junho): o paciente grave entra direto e a classificação é digitada depois. É artefato de
  registro, não espera real.
- **Laranja é raríssimo**: 8 casos em 13.242 na UPA Maricá e 2 em 7.876 em Santa Rita, nos 30
  dias até 24/07. A cor existe no protocolo e aparece no painel, mas a maior parte dos dias vem
  zerada. No Conde ela não existe e some da tela (`coresUsadas`).

## L4/L5 — Leitos de observação

As UPAs não internam, mas têm leito de **observação** — e é isso que a aba "Leitos" mostra
nelas, com esse nome.

- **L4 — ocupação**: `Leito` (inventário) com `lei_status` L/livre e O/ocupado. Não há status
  de bloqueio nestas bases. O cadastro é raso: um único setor (`URGÊNCIA/OBSERVAÇÃO`), sem
  nome de enfermaria (`Enfermaria.enf_descricao` vazio) e sem tipo de leito preenchido —
  quebrar pelos `locatend_codigo` (0004..0007) daria quatro linhas numeradas sem significado.
- **L5 — fluxo**: quantos foram encaminhados à observação no período, pela SUBDESCRIÇÃO da
  classificação de risco (`Consultório` × `Observação`). É medida de fluxo, não de ocupação.

### Santa Rita não tem cadastro de leitos utilizável

| | leitos cadastrados | ocupados | `rowversion` da tabela `Leito` |
|---|---|---|---|
| UPA Maricá | 23 | 4 | ~376 mil modificações atrás — **viva** |
| Santa Rita | **2** | 0 | ~13,6 **milhões** atrás — **parada** |

Dois leitos para 262 atendimentos/dia não descrevem a unidade, descrevem o abandono do
cadastro. O painel aplica um limiar (`ConsultasUpa.MinimoLeitosCadastrados = 5`): abaixo
dele, manda `ocupacao: null` + `indisponivel` com o motivo, e a tela diz o que falta em vez
de publicar "0 de 2" e sugerir unidade vazia. **É limiar, não lista fixa** — no dia em que a
unidade cadastrar os leitos, o painel volta a mostrar ocupação sozinho.

`UPA_CONSOLIDACAO_PACIENTE_OBSERVACAO` não serve como alternativa: 16 linhas, sem coluna de
data, apontando para internações da safra morta — e com `rowversion` **idêntico** (28.899.986)
nos dois bancos, ou seja, populada uma vez na implantação e nunca mais tocada.

Retrato de 25/07 — UPA Maricá 4/23 (17,4%), 77 encaminhados à observação em julho de 9.308
classificados (0,8%); Santa Rita 27 encaminhados de 6.039 (0,4%).

## Desfechos (não usado no painel — mapeado para referência)

`UPA_Atendimento_Medico.tipsai_codigo` → `Tipo_Saida`. Distribuição na UPA Maricá nos 30 dias até
24/07/2026:

| desfecho | qtd |
|---|---|
| A.1 — Atendimento em consultório concluído | 10.997 |
| (sem saída registrada) | 1.597 |
| D — Alta por evasão | 418 |
| E — Evasão sem atendimento médico | 90 |
| A — Alta por decisão médica | 90 |
| I — Transferência | 32 |
| F — Óbito | 11 |
| C — Alta a pedido | 7 |

## Como rodar uma consulta à mão

Do próprio droplet (a porta do proxy é loopback):

```bash
/root/pq.sh upa24h-marica-sqlserver   <<< "SELECT TOP 10 …"
/root/pq.sh santarita-marica-sqlserver <<< "SELECT TOP 10 …"
```

Se der "agente não está conectado", esperar ~1 min: os agentes reconectam sozinhos após um
restart do `smsmarica-server`, mas nem sempre na hora.
