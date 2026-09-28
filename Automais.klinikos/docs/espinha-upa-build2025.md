# Espinha da UPA / Santa Rita — build 2025 (`U.2025.06.1.26`)

**Medido em 16–17/09/2026, SOMENTE LEITURA.** Este documento levanta a UPA e a Santa Rita ao
nível do Conde: quais relatórios dão a **espinha** (por boletim: chegada, cor da classificação de
risco, clínica, CID e desfecho) e os **parâmetros corretos** da build 2025 — que **não** são os do
Conde. Serve de base para o conector `KlinikosWeb` ser *version-aware* (perfil por build).

Companheiro de [`dados-crystal.md`](./dados-crystal.md) (espinha do Conde 2024) e da §7 de
[`APRENDIZADOS.md`](./APRENDIZADOS.md) (as três camadas que divergem).

---

## 1. Onde vive a espinha na build 2025: a tela 526 com seletor de modo

No Conde (2024) a espinha se espalhava por relatórios distintos: 407 (cadastro), 667 (chegada+cor),
526 (CID por boletim). Na build 2025 ela foi **consolidada numa única tela** — o relatório
**526** (`/UPA24H/UPA/Relatorios/Relatorio.aspx?parrel=526&origem=5`, rótulo "Atendimentos por
Profissional") — que ganhou um **rádio de modo** `rblPadrao` com quatro opções:

| `rblPadrao` (par8) | Modo | O que entrega |
|---|---|---|
| 0 | **Padrao** | totais por profissional (não é nominal) |
| 1 | **Nominal** | **lista por boletim**: chegada, cor, clínica, cadastro básico |
| 2 | **Por CID** | **CID por boletim** (equivalente do 526 do Conde) |
| 3 | **Por Status BAM** | **desfecho / tipo de saída por boletim** (status do Boletim de Atendimento Médico) |

Trocar o modo é um **AutoPostBack** que reconfigura a tela; ao imprimir, a mesma tela 526 gera um
**relatório subjacente diferente** para cada modo. Ou seja: um `parrel` de tela → três `parRel` de
saída.

## 2. As três URLs da espinha (parâmetros CONFIRMADos da build 2025)

Capturadas dirigindo a tela num navegador real (a app monta o `window.open` do lado servidor — ver
§4). Endpoint: `/UPA24H/Relatorios/rptviewXls.aspx` (XLS BIFF) — ou `rptview.aspx` (PDF), mesmos
params.

```
# Nominal (chegada + cor + clínica + cadastro) — o "667+407" da build 2025
rptviewXls.aspx?parNomeMaquina=&parRel=751&parNum=8&par1=<unid>&par2=<dd/mm/aaaa>&par3=<dd/mm/aaaa>&par4=5&par5=<esp|vazio>&par6=0023&par7=<prof|vazio>&par8=1

# Por CID (CID por boletim) — o "526" da build 2025
rptviewXls.aspx?parNomeMaquina=&parRel=752&parNum=8&par1=<unid>&par2=<ini>&par3=<fim>&par4=5&par5=<esp|vazio>&par6=0023&par7=<prof|vazio>&par8=2

# Por Status BAM (desfecho / tipo de saída por boletim)
rptviewXls.aspx?parNomeMaquina=&parRel=802&parNum=9&par1=<unid>&par2=<ini>&par3=<fim>&par4=5&par5=<esp|vazio>&par6=0023&par7=<prof|vazio>&par8=3&par9=0
```

Significado dos parâmetros posicionais (medido):

| par | valor | significado |
|---|---|---|
| par1 | `0006` UPA / `0007` Santa Rita | **unidade** |
| par2 / par3 | `dd/mm/aaaa` | período (início / fim) — pode ser o mesmo dia |
| par4 | `5` | origem = Urgência (o `origem=5` da tela) |
| par5 | vazio = **todas** | especialidade/clínica (código, ex. `1428` Clínica Médica) |
| par6 | `0023` | turno = hora início `00` + hora fim `23` (dia inteiro) |
| par7 | vazio = **todos** | profissional (código) |
| par8 | `1`/`2`/`3` | **modo** `rblPadrao` (Nominal / Por CID / Por Status BAM) |
| par9 | `0` (só no 802) | flag do Status BAM |
| parRel | `751`/`752`/`802` | relatório subjacente por modo |

**Deixar `par5` e `par7` vazios = TODAS as especialidades e TODOS os profissionais** → é o nominal
do dia inteiro, sem filtro obrigatório de valor único. É exatamente o que a espinha precisa.

## 3. Cobertura da espinha (o que cada relatório dá vs. o que a espinha exige)

| Necessidade da espinha | Conde 2024 | UPA/Santa Rita build 2025 |
|---|---|---|
| chegada (Data/Hora Entrada) | 667 | **751** (Nominal) |
| cor (classificação de risco) | 667 (group header) | **751** (Nominal) |
| clínica | 667 / 407 | **751** (Nominal) |
| cadastro básico (boletim, paciente, nasc.) | 407 | **751** (Nominal) |
| **CID por boletim** | 526 (subrows) | **752** (Por CID) |
| **desfecho / tipo de saída** | (a mapear no Conde) | **802** (Por Status BAM) |

As **colunas exatas** de 751/752/802 ficaram **pendentes** — ver §5 (Crystal esgotou a memória
durante o recon). Pela paridade com o 526 do Conde, esperam-se em 751/752: `Nº Boletim`, `Código`,
`Hora Atendimento`, `Nome`, `Classificação`, `Idade`, `Sexo`, `Clínica` (+ `CID` no 752); e no 802
o `Status`/tipo de saída por boletim. **Confirmar quando o servidor de relatório recuperar** e
fixar o `de-para` de colunas → FHIR Encounter (chegada=`period.start`, cor=`priority`,
clínica=`type`, saída=`period.end`/`hospitalization.dischargeDisposition`, CID=`Condition`).

## 4. Por que o caminho do Conde não funciona aqui (mecanismo)

- **A tela usa `Sys.WebForms.PageRequestManager`** (postbacks assíncronos parciais). O botão
  "Imprimir Excel" é um **RadAjaxManager** (`onclick=AjaxNS.ARWO(new WebForm_PostBackOptions(...))`),
  não um postback comum. Um POST cheio "na mão" só **re-renderiza** o painel: o evento `Click` do
  `ImageButton` não dispara pela emulação, então a app **não monta o `window.open`** e **não**
  aparece alerta. (O "Atenção!" que víamos era **falso positivo** — o regex batia na *definição* da
  função `SwAlerta2`, não numa chamada.)
- **O jeito robusto e à prova de versão** é deixar a **JS da própria app** montar a URL: injeta-se o
  cookie autenticado num navegador real, dirige-se a tela (modo → especialidade → datas → Imprimir)
  e **captura-se a requisição `rptviewXls`** que a app gera. Foi assim que os params acima saíram.
- **`parN` do Conde → timeout de SQL:** chamar `rptviewXls` do 667/526 com o `parN` do Conde
  devolve `SqlException` de **timeout de comando** (`... asyncWrite, Boolean inRetry, SqlDataReader`),
  porque o SP da build 2025 recebe params fora do formato e faz plano ruim. Com os params da §2 o SP
  roda certo (chega até o Crystal).
- **667 na build 2025 é OUTRO relatório:** exige **Ocorrência + Classificação de Risco**
  obrigatórios (drill-down), não é o nominal simples do Conde. Não serve de espinha.
- Relatórios **cross-tab de totais** (65 Tipo de Saída, 543/542 CID por faixa etária, 85) só têm
  datas, mas são **totais por faixa etária — não amarram a boletim**. Não são a espinha.

## 5. Obstáculo em aberto: Crystal vaza memória e satura sob volume

No início da sessão, 629/630 (PDF, snapshot) responderam normal. Após ~18 requisições de relatório
no recon, **todo** export passou a devolver
`COMException (0x80041004): Not enough memory for operation` no `ReportSourceClass.Export` — XLS e
PDF, com e sem filtro, em UPA e (esperado) Santa Rita. É **vazamento de memória do servidor Crystal
deles**, que só recicla no *app pool* (minutos/horas), não em segundos.

**Consequência operacional (casa com "não onerar o sistema"):** o caminho web tem de ser
**cirúrgico** — poucas requisições, `backoff` no OOM, e de preferência em janela de baixo uso.
Para a espinha diária isso é barato: **3 requisições por dia por unidade** (751+752+802, período =
o dia, `par5`/`par7` vazios). O *deep* por paciente entra em **fila, um a um, sem pressa**.

## 6. Santa Rita = paridade total com a UPA

Mesma build (`U.2025.06.1.26`), mesmo app (`/UPA24H`), mesmo gate (porta+local → `_ID_VINCULO`),
mesma tela 526 com os mesmos modos `rblPadrao` (Padrao/Nominal/Por CID/Por Status BAM). **Só mudam
host e unidade:** `starita24h.smsmarica.online`, `par1=0007`. As URLs da §2 valem trocando
`par1`. Login `leonardo.dantas` entra de verdade nas duas.

## 7. Credenciais e gate (resumo operacional)

- **Login:** `leonardo.dantas` / (senha no `.env`) — vale UPA **e** Santa Rita. `bruno.rocha` /
  `l.mello` / `123` valem **só no Conde**.
- **Gate (1× por sessão):** `porta de entrada` (AutoPostBack) → `local físico` → salvar. Grava
  `_ID_VINCULO` + `_LOCAL_NAME`. Sem isso os relatórios não engatam. `paridade_upa.gate_porta_local()`
  já faz.
- **Guarda de aba:** a primeira visita a uma tela de relatório pode cair em `ErroGuia.aspx`
  ("feche a guia atual"). Visitar `/UPA24H/Default.aspx` uma vez destrava.

## 8. Próximos passos

1. **Confirmar colunas** de 751/752/802 quando o Crystal recuperar (uma requisição de cada, dia
   único) e fixar o `de-para` → FHIR.
2. **Perfil de build no conector:** `KlinikosWeb` detecta a versão (Conde `K.2024` vs UPA/STA
   `U.2025`) e escolhe o mapa de relatórios/params. Auto-detecção pela string de versão da home ou
   escolha explícita no cadastro da instância (dropdown "versão").
3. **Paridade web × SQL** na UPA: com 751 lendo boletim+chegada+cor, rodar `paridade_upa.py`
   (ajustado para 751) contra o que o conector SQL já colocou no hub — critério de aceite.
