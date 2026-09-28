# Sincronismo cirúrgico Conde/UPA/Santa Rita pela web — o que é lote, o que é um-a-um, e o custo medido

> Medido em 16/09/2026 contra o Conde (`klinikosconde`, unid 0005). O objetivo é o do operador:
> **ser cirúrgico nas requisições** (não onerar o Klinikos) e trocar o sincronismo hoje feito por
> SQL (rowversion) por leitura "como usuário". Nada aqui foi escrito no hub.

## 1. O que sai em LOTE (1 requisição) × o que é UM-A-UM

| Dado do conector FHIR | Em lote pela web? | Fonte |
|---|---|---|
| Boletim / chegada / prontuário | **Sim** | 407 (registrados/dia), 667 (nominal por risco) |
| Atendido + hora + classificação + clínica | **Sim** | 526 (atendidos por profissional/dia) |
| CID por boletim | **Sim** | 815 (Atendimento Nominal por CID) |
| Desfecho / evasão / óbito / remoção | **Sim** (nominal ou totais) | 65, 834, 520, 527 |
| Em observação + leito | **Sim** | 630 |
| Cadastro novo (prontuário, nasc, sexo, CNS) | **Sim** | 21 (Prontuários Abertos), 488 (Origem) |
| **Narrativa médica** (anamnese, exame físico, hipótese, conduta) | **NÃO** | tela `AtendimentoMedico.aspx` (por boletim); 791 PDF por paciente/competência (mês inteiro dá 504) |
| **Prescrição** (medicamento, dose, via) | **NÃO** | tela `PrescricaoReceita.aspx` (por boletim) |
| **Sinais vitais** | **NÃO** | tela `Enfermagem.aspx` / `RegistrosEnfermagem.aspx` (por boletim) |

**Confirmado varrendo o catálogo inteiro (268 relatórios):** prescrição, sinais vitais e a
narrativa completa **não têm relatório nominal em lote**. Só existem por boletim (tela) ou, para a
narrativa, o PDF 791 por paciente. Resposta direta à pergunta: **sim, essa camada profunda é
um-a-um** (ou, na melhor das hipóteses, um-a-um por paciente via 791, não por boletim).

## 2. Custo medido (Conde, 1 dia, ~1.000 boletins/dia)

Relatórios de lote, por chamada (Crystal no servidor deles):

| Relatório | Tamanho | Tempo |
|---|---:|---:|
| 630 em observação (sem data) | 162 KB | **10–13 s** |
| 407 registrados no dia | 532 KB | **23–30 s** |
| 526 atendidos no dia (nominal + classificação) | 1,1 MB | **~60 s** (no limite do 504 do nginx) |

Telas por boletim: GET da tela = ~1–1,5 s, **mas** ela abre a fila/busca (RadGrid) e **ignora o
boletim na query string** — abrir um boletim específico exige **postback de busca + seleção na
grade** (2+ requisições), e a narrativa/prescrição/vitais estão em **abas separadas** (mais
postbacks). Estimativa realista: **3–6 requisições por boletim**. As telas de atendimento são de
**escrita** (charting) — abrir um boletim nelas em produção é arriscado; o caminho de leitura é a
tela "Histórico do Paciente" (`ResumoProntuario.aspx`) ou o PDF 791.

**Conta do "um-a-um para todos":** Conde ~1.000 boletins/dia × 3–6 req = **3.000–6.000
requisições/dia só para o Conde**, mais UPA e Santa Rita. É exatamente o volume a evitar.

## 3. Desenho do sincronismo cirúrgico (proposto — nada implementado)

Substitui o CDC por rowversion (que não existe na web) por um **delta de fechados**:

1. **Marca incremental = último boletim fechado visto.** Não há rowversion; a chave é o
   `spa_codigo` (Nº Boletim) + data.
2. **1 requisição de lote por unidade por ciclo** para listar os boletins do período recente
   (ex.: 526 ou 407 do dia; para "agora", 630). Diff contra o que o hub já tem (`identifier`
   `urn:klinikos:boletim`). Custo: ~1 relatório/unidade/ciclo (25–60 s), fora de pico.
3. **A espinha (Encounter, chegada, CID, desfecho, classificação, cadastro) entra desse lote** —
   sem tocar tela. É o que devolve o Conde ao hub e ao Painel do Secretário.
4. **A camada profunda (narrativa/prescrição/vitais) é sob demanda e cirúrgica**, não a mangueira
   inteira. Regras possíveis (a decidir):
   - só para boletins com desfecho relevante (internação, óbito, remoção), não para toda alta;
   - só quando outro sistema/clínico pedir aquele paciente (pull on-demand);
   - preferir **791 por paciente/competência** (1 GET traz vários boletins do mesmo paciente)
     a abrir tela por boletim;
   - o resto: pedir **exportação ao fornecedor** (Eco Sistemas).
5. **Medir sempre:** cada ciclo registra nº de requisições, bytes e tempo por unidade; se um
   relatório passar de ~60 s (borda do 504), recuar a janela (1 dia, não semana/mês).
6. **Uma sessão por unidade, uma requisição por vez** (o ASP.NET serializa a sessão; paralelizar
   dá 504). Concorrência real = 3 sessões (uma por unidade), não mais.

## 3b. Medições de custo e vazão (Conde, 16/09/2026) — feito

**Vazão (quantos fecham por hora)** — coluna "Hora Atendimento" do relatório 526, dia 15/09:

- **831 atendimentos no dia**, média **~36/hora**, **pico ~65–71/hora** entre 08h e 15h,
  madrugada < 12/hora. Ou seja, o job horário de "fechados" processa ~36 boletins no caso médio
  e ~70 no pico — por unidade.

**Custo dos relatórios de lote** (cronometrado, 2 execuções cada):

| Relatório | Tamanho | Tempo | Uso |
|---|---:|---:|---|
| 630 em observação | 162 KB | 10–13 s | painel "agora" |
| 407 registrados/dia | 532 KB | 23–30 s | delta de registrados |
| 526 atendidos/dia | 1,1 MB | ~60 s | delta de fechados (no limite do 504) |

**Custo do "um-a-um" (camada profunda)** — não sai barato: a tela de boletim faz GET ~1–1,5 s
mas **ignora o boletim na URL** (abre a fila em grade) e a narrativa/prescrição/vitais estão em
abas → **postback de busca + seleção + abas**. O PDF por paciente (791) seria 1 GET, mas o
`par2` é o **código interno do paciente** e o combo o resolve por **callback de postback** (não é
webservice) — some GetData → 1 lookup + 1 GET por paciente/competência. Estimativa por boletim:
**2–4 requisições e ~5–10 s**. Não fechei o número exato de propósito, para não martelar a
produção deles; fica para o harness (§4), rodado pontualmente.

**Conta da estratégia proposta** (por unidade):

| Job | Cadência | Requisições | Tempo de servidor deles |
|---|---|---|---|
| Painel "agora" (630 + 631) | 10 min | ~2 relatórios | ~25–45 s |
| Delta de fechados (526 ou 407) | 1 h | 1 relatório | ~25–60 s |
| Profundo um-a-um | quando fecha | ~36/h × 2–4 = **~70–150 req/h** (pico ~140–280) | ~3–6 min/h (pico ~12 min/h) |

O peso está no **um-a-um**. Por isso ele tem de ser **cirúrgico**: não a mangueira inteira, e sim
os boletins que importam (ver §3.4). O painel e a espinha são baratos.

## 3c. Veredito sobre a sua estratégia

Faz sentido e bate com o medido:
- **Painel a cada 10 min:** ok, é leve (630 é o mais barato; 631 para "em andamento"). Uma
  sessão por unidade, sequencial.
- **Fechados de hora em hora:** ok — 1 relatório/unidade/hora pega os ~36 (pico ~70) que
  fecharam. O diff é por `spa_codigo` contra o hub.
- **Profundo só quando fechou, um-a-um:** ok, mas é a parte cara. Recomendo **teto por ciclo**
  (processar no máximo N/hora e deixar fila para o próximo) e **priorizar** (internação/óbito/
  remoção primeiro; alta simples pode esperar ou nem puxar narrativa). E medir sempre, recuando
  se passar de ~60 s/relatório ou se a fila do profundo crescer.

## 3c-bis. Por que o "rápido" do painel é pesado — e por que ele NÃO deve existir

Medido no Conde em 16/09:

| Fonte | Saída | Tempo | O que é |
|---|---:|---:|---|
| 631 em andamento (relatório) | 4 KB | 3,0 s | Crystal (vazio no dia) |
| 56 registro diário por clínica (**só totais**) | 99 KB | **14,6 s** | Crystal |
| 630 em observação (relatório) | 162 KB | 13,7 s | Crystal |
| **AdmFila.aspx (tela viva da fila)** | 288 KB | **0,8 s** | página ASP.NET normal |

A lição: **o peso é o Crystal Reports, não o dado.** Um relatório de **totais** de 9 linhas
(56 = 99 KB) leva os mesmos ~14 s que um nominal de 162 KB — porque o custo é o motor de
relatório (carrega a definição `.rpt`, roda o formula engine, renderiza XLS/PDF e segura o lock
da sessão ASP.NET), não a consulta. Prova: a **tela viva** da fila (`AdmFila.aspx`), que lê o
banco deles direto e pinta uma grade, responde em **0,8 s** — 17× mais rápida que qualquer
relatório. Mas essa é a via interna *deles*, exatamente a que estamos abandonando.

**Tem caminho fora do Crystal? Sim, mas não serve para nós.** Testado em 16/09:
- A `AdmFila.aspx` (página viva) carrega em **0,8 s** — prova que o dado é barato; o Crystal é o
  overhead. **Mas** o grid vem **vazio** no HTML: as linhas entram depois por um **callback AJAX
  do RadGrid** (filtro `ddlFilter`), que é um **postback de ViewState por página**, específico
  de porta/fila, e nas tentativas voltou sem linhas. É frágil (quebra a cada build), dá só a
  **fila viva "agora"** (não histórico) e ainda é PII na tela.
- **Não existe API de dados** (a varredura das 368 telas não achou `.ashx`, WebMethod nem JSON
  próprio — só relatórios Crystal, páginas ASP.NET inteiras e os combos de lookup).

Ou seja, para **puxar dado da origem** só há dois caminhos reais: (a) **relatório Crystal**
(lento ~14–60 s, mas é um GET autocontido e estável) ou (b) **dirigir o RadGrid das telas vivas**
(rápido ~0,8 s, mas postback frágil, por página, e só "agora"). Para **ingestão** vale o (a),
com cadência gentil. Para o **painel** não vale nenhum — o dado já está no hub.

**Conclusão (princípio do operador, 16/09):** *acesso direto ao banco acabou; a origem passa a
ser lida "como usuário", com parcimônia, só para **ingestão**. Histórico, tempo médio, "agora",
dashboard — tudo isso a gente **deduz do nosso próprio hub**, porque o dado já está em casa.
A carga sai do sistema de origem e vem para o nosso, que é nosso para escalar.*

Portanto **o painel do Secretário não deve chamar o Klinikos.** Nada de poll de relatório a cada
10 min (seriam ~14 s de Crystal × 3 unidades × 144 vezes/dia — absurdo em cima do fornecedor).
O painel lê o **hub**:
- "aguardando por cor", "em atendimento", "atendidos hoje", "tempo classificação → atendimento"
  saem de `fhir.encounter` (chegada, classificação/cor, início e fim do atendimento) — sub-segundo,
  de graça, sem tocar o Klinikos.
- O "instantâneo" vira "na última sincronização" (~a cadência da via rápida). Para um painel
  executivo isso basta, e a frescura é o **único** botão a girar — não um poll novo.

> **A confirmar (banco de prod estava fora agora):** que o `fhir.encounter` da UPA já carrega
> **cor da classificação** e os **carimbos de início/fim do atendimento**. Se algum não estiver,
> não é bloqueio: a via rápida ingere pelos relatórios nominais que já os trazem (667 = boletim +
> Data/Hora Entrada + risco; 526 = boletim + hora + classificação). Ou seja, o painel continua
> derivado do hub; só garantimos que a via rápida grava esses campos.

## 3d. Arquitetura acordada (16/09/2026): via rápida barata + fila lenta do profundo

Duas trilhas independentes por unidade (Conde, UPA, Santa Rita), desacoplando **chegada** de
**processamento** — o fornecedor nunca leva rajada:

### Via rápida (barata, janela curta, alta frequência)
- **Painel "agora"** a cada **10 min**: `630` (observação) + `631` (em andamento). ~25–45 s/ciclo.
- **Espinha / delta de fechados** a cada **~15 min**: `407` (~25 s; **não** o `526`, que raspa o
  504). Janela **curta e rolante** (o dia corrente, re-lido). Diff por `spa_codigo`
  (`urn:klinikos:boletim`) contra o hub → só o que é novo entra. Como roda toda hora curta, cada
  ciclo traz **poucas linhas novas** (~9/15min no médio, ~18 no pico) — nada acumula.
- Tudo que sai do lote (Encounter, chegada, desfecho, classificação/cor, **CID**, cadastro) é
  gravado **aqui**, idempotente por `spa_codigo`. É o que devolve o Conde ao hub e ao painel.
- **Conjunto mínimo da via rápida** (medido 16/09):
  - **667** (~14 s): Nº Boletim + **Data/Hora Entrada (chegada)** + **cor** + clínica → "esperando
    por cor" e tempo de espera.
  - **631** (~3–14 s): Nº Boletim + **Início Atendimento** + tempo + clínica + profissional → "em
    atendimento" e tempo de atendimento.
  - **526** (~60 s, **1×/hora**): traz **CID + procedimentos + hora do atendimento** por boletim
    em lote (agrupado por profissional) — é o que resgata o CID sem abrir tela (o 815 dedicado
    está quebrado). **A cor vem do 667**, não do 526 (a coluna Classificação do 526 veio vazia).
    Pesado, então **horário**, não a cada 10 min.
- Assim a espinha inteira (jornada + cor + CID) sai de **relatório**, e o **deep** fica só com o
  que não existe em relatório nenhum: **narrativa, prescrição e sinais vitais**.

### Fila lenta (o profundo, um-a-um, sem pressa)
- Camada profunda = **narrativa médica + prescrição + sinais vitais** (o CID saiu daqui: vem em
  lote pelo 526 — ver §3d espinha). Só o que não existe em relatório nenhum fica na fila.
- Ao ver um boletim **fechado** sem camada profunda no hub, **enfileira** `spa_codigo` (+ unidade
  + chave do paciente + prioridade) numa **tabela durável** em `smsmarica` (não em memória).
- Um **drenador** de fundo puxa a fila a **taxa fixa e gentil** (ex.: 1 boletim a cada N s,
  teto de M/hora por unidade), **um por vez**, respeitando 1 sessão/unidade e 1 requisição/vez.
  "Sem pressa" = rate-limit + backoff; nunca esvazia em rajada.
- **Prioridade:** internação / óbito / remoção antes de alta simples. Alta simples pode nem puxar
  narrativa (decisão de produto).
- **Idempotente e resiliente:** cada item faz upsert por `spa_codigo`; falha volta pra fila com
  backoff; a fila pode acumular sem dor — ela existe justamente para absorver o pico e drenar no
  vale da madrugada.
- **Guarda-custo:** cada ciclo registra nº de requisições, bytes e segundos; alarme se um
  relatório passar de ~55 s (borda do 504) ou se a profundidade da fila crescer sem drenar.

### Marca incremental (não há rowversion na web)
- Via rápida: `spa_codigo` já visto (o diff é a marca).
- Fila: estado por item (enfileirado / em processo / concluído / falho+tentativas).

### Painel do Secretário: 0 chamadas ao Klinikos
Lê o **hub**, não a origem. O painel é a **frente do hospital ao vivo**: quantos esperando e há
quanto tempo, quantos por cor (vermelho/amarelo/verde…), quantos em atendimento e o tempo em
curso. Tudo isso são **encounters recentes** no hub:
- **esperando por cor** = boletim do dia com cor (do 667) e **sem** Início Atendimento; tempo =
  agora − chegada.
- **em atendimento** = com Início Atendimento (do 631) e sem fim; tempo = agora − início.
- **atendidos / desfecho** = com fim (do 526/desfecho).

Ou seja, o painel **não faz requisição própria ao Klinikos** — ele consome o que a via rápida já
ingeriu (667 + 631, e 526 de hora em hora). A **mesma** ingestão serve o painel, a análise do
passado e o backfill do hub. A "frescura" do painel = cadência da via rápida (~10 min para
667/631). Isso apaga o poll de 10 min ao Klinikos do plano antigo — era Crystal caro para um
número que o hub já tem.

## 3e. Desonerar ao máximo a origem — checklist

O objetivo é **transferir a carga do sistema do fornecedor para o nosso**. Regras:

1. **Painel: zero requisições à origem** — derivado do hub.
2. **Nunca usar relatório de "estatística/totais" para o que o hub calcula** — Crystal cobra
   ~14 s até para 9 linhas. Se precisar de agregado, agrega **no nosso banco**.
3. **Via rápida: 1 relatório leve por unidade por ciclo** (407 ~25 s / 667 ~14 s). O 526 (~50 s)
   fica fora da via rápida — não por 504 (o nginx é nosso, timeout ajustável), mas por **carga**:
   é CPU de Crystal no app deles, então roda raro e na madrugada. Janela curta e rolante → poucas
   linhas novas por ciclo.
4. **Preferir relatório NOMINAL (linhas cruas que a gente ingere) a relatório que faz a origem
   computar** — a conta é nossa, não deles.
5. **Deep na fila lenta**, taxa gentil, teto/hora, drenar na madrugada (vale de ~<12/h).
6. **Uma sessão por unidade, uma requisição por vez** (a sessão ASP.NET serializa; paralelismo
   só gera 504).
7. **Cache do último resultado por (unidade, relatório, janela)**; só re-puxa na cadência.
8. **Recuo automático:** se um relatório passar de ~55 s, encurtar a janela; se a fila do deep
   crescer, baixar a taxa de chegada (janela ainda mais curta), não subir a de saída.
9. **Tudo medido por ciclo** (req, bytes, s) — a régua de "estamos onerando?" é objetiva.

O norte: a origem é lida o **mínimo** necessário para **trazer o dado uma vez**; tudo o que é
consulta, histórico, indicador e dashboard roda **em casa**, sobre o hub.

## 3f. Crystal onera o SERVIDOR deles; "tela" é a própria aplicação (leve)

A preocupação do operador está certa e casa com o medido:

- **Relatório Crystal = carga no servidor do fornecedor.** Os ~14 s não são rede: é CPU/memória/IO
  deles — carrega a definição `.rpt`, roda o formula/grouping engine, sobe uma sessão de
  relatório, renderiza XLS/PDF e **segura o lock da sessão ASP.NET o tempo todo**. Um totalzinho
  de 9 linhas (rel. 56) custou os mesmos 14 s: o custo é o **motor**, não o dado. Bater nisso a
  cada 10 min × 3 unidades é peso real no servidor deles.
- **"Tela" = a aplicação normal servindo uma página** (um SELECT + render de grade). Medido:
  `AdmFila` 0,8 s contra ~14 s do Crystal — **~17× menos tempo de servidor** por request, e solta
  o lock da sessão em ~1 s em vez de segurar 14–60 s (muito mais gentil com concorrência).

**Consequência de projeto:**
1. **Deep pela fila: preferir a TELA de leitura (`ResumoProntuario`) ao 791 (Crystal).** Mesmo
   sendo vários requests por boletim, cada um é leve (~1 s de app) e não acende o motor de
   relatório. O 791 (PDF) é o oposto: 1 request, mas Crystal — evitar no volume.
2. **Crystal só onde não há alternativa barata:** a **lista diária de fechados** (espinha) —
   aí é **1 relatório leve por unidade por ciclo** (407), não um poll. Uma batida de Crystal por
   ciclo é aceitável; dezenas não.
3. **"Leve por request" ≠ "leve no total":** o deep é milhares de postbacks/dia se for a
   mangueira inteira. Por isso continua **fila com teto e prioridade** — o que torna o
   slow-drain viável é justamente cada request ser leve e espalhado no tempo, sem rajada.
4. **ViewState tem custo:** cada postback de tela carrega ~280 KB de ViewState (banda + desserial.
   deles). Baratíssimo perto do Crystal, mas não zero — mais um motivo para o deep ser cirúrgico.

Regra prática: **para o servidor deles, uma tela ≪ um relatório.** Onde der para trocar Crystal
por tela (o deep), troca. Onde o Crystal for inevitável (a lista de fechados), use **uma vez por
ciclo, janela curta**, nunca em poll curto.

Isso satisfaz o pedido: **janela curta** (nada acumula na via rápida), **traz o barato sempre**,
e o **profundo vira fila drenada um-a-um sem pressa**, com o custo por boletim (§3b: ~2–4 req,
~5–10 s) diluído no tempo em vez de concentrado.

## 3g. Não dá para janela menor que "um dia" — então o botão é a CADÊNCIA

Testado em 16/09: o `526` **ignora a hora** nos parâmetros — passar `15/09/2026 08:00–10:00`
devolveu os **mesmos 831 boletins em ~50 s**, idêntico ao dia inteiro. Os parâmetros são
**data (dia), não datetime**; qualquer hora é descartada. O mesmo vale para os outros relatórios
de período (a tela só oferece RadDatePicker de data). O custo do Crystal é fixo por render
(~14–60 s) independente de quantas linhas — encurtar a janela **não** baixaria o custo mesmo que
desse.

**Consequência:** como não dá para estreitar a **janela**, o que a gente regula é a **cadência**
e o **lado de cá**:

1. **Diff por `spa_codigo` (nosso lado):** puxa o dia inteiro no ciclo, mas só **ingere os novos**.
   O relatório custa o mesmo; nossa gravação é que fica incremental.
2. **Cadência por peso do relatório, não por janela:**
   - **667 + 631** (leves, ~14 s): frequentes (~10–15 min) — é a frente do hospital.
   - **526 (CID, ~50 s de CPU Crystal):** **raro** — não mais por 504 (o nginx é nosso, timeout
     ajustável; ver §3g), e sim por **carga** no app deles. CID/procedimento é **enriquecimento**, não é
     ao vivo — então roda **1×/dia de madrugada** (dia anterior fechado), quando é mais rápido e
     não disputa com o pico. Se precisar de CID mais fresco, no máximo a cada poucas horas, com
     retry/backoff no 504.
3. **"Agora" usa relatório de estado, não de período:** 629/630/631 são retratos do instante
   (fila/observação/andamento), naturalmente pequenos — não crescem como o 526 do dia.

Ou seja: janela mínima é o dia; a economia vem de **puxar o pesado poucas vezes** (e no vale) e
**derivar o resto em casa**.

### E o medo dos 60s do 526? Não dá para trocar por relatórios leves somados

Verificado em 16/09:
- **526 não fatia:** o único filtro é **profissional (1.308 opções)** — não há por clínica nem
  especialidade. Fatiar seria 1.308 chamadas, pior.
- **Não há relatório de CID por boletim mais leve:** o terso (815) está **quebrado**; os demais
  de CID são só **totais**. Para CID de *todos* os boletins, a origem só oferece o 526 inteiro.

Como o 526 é inevitável e pesado, o medo do 504 se resolve pelo **quando**, não pelo **qual**:
1. **526 na madrugada, 1×/dia, do dia anterior fechado.** CID não é "ao vivo" (o painel usa
   667/631, sem CID). Às 3h o servidor deles está ocioso → os ~50s tendem a ~20–30s e o 504 quase
   some. Se der 504, retry com backoff; atrasar enriquecimento não machuca.
2. **CID de carona no deep** para o subconjunto cirúrgico: o CID está na **mesma tela** do
   atendimento (narrativa/prescrição/vitais), então todo boletim que a fila lenta já abrir (
   internação, óbito, priorizados) **traz o CID junto, de graça**. O 526 noturno cobre o resto
   (a massa de altas simples), garantindo Condition em todo Encounter.

> **Correção (operador, 16/09): o nginx é NOSSO.** O timeout de 60s que gerava o 504 é ajustável
> por nós (`proxy_read_timeout`/`proxy_send_timeout` no reverse proxy). Ou seja, **o 504 deixa de
> ser parede** — o 526 completa os ~50s sem cortar. **Atenção:** subir o timeout **não acelera** o
> relatório; os ~50s são o **Crystal na CPU do servidor de aplicação** atrás do nginx. Então o
> 526 continua pesado *para aquele servidor* e ainda vale rodá-lo **raro e na madrugada** — mas
> agora por **carga**, não por medo de falhar. Sem o 504, cai a urgência de fatiar/substituir:
> podemos rodar o 526 inteiro quando precisarmos, com retry só por precaução.

## 4. Custo do deep — medido em 16/09 (`medir_custo_boletim.py`)

O que **ficou medido** (sobre a tela de leitura `ResumoProntuario`, 5 prontuários reais):
- **Cada request é leve e não-Crystal:** shell ~0,5 s + um postback ~0,5 s; ~4 requisições e
  **~1,1 s** por prontuário só para abrir a tela e tentar carregar. Confirma "tela ≪ relatório".

O que **resistiu à medição** (e é em si a descoberta): **carregar o prontuário não é um postback
simples.** O campo do prontuário é `readonly` e a carga só acontece pelo **popup de busca**
(`rwinConsultaOrigem` → consultar paciente → selecionar), que devolve o prontuário ao pai e só
então monta a **árvore** de atendimentos (cada nó expande por outro postback). Setar o campo e
disparar `imbPesquisar`/árvore **não** carregou (nome/CID vieram vazios). Além disso, os
prontuários de teste (do relatório 21 = "abertos") são recém-cadastrados, sem atendimento.

**Estimativa realista do deep por paciente** (não um número cravado): shell + popup de busca +
seleção + expansão de N nós = **~4–8 requisições, ~3–6 s**, dominado por round-trips (não Crystal),
**mais a fragilidade** de dirigir popup + árvore, que quebra a cada mudança de build deles.

**Conclusão que isso reforça:** raspar o deep por tela é **caro em passos e frágil**. Portanto:
1. A **fila lenta** do deep tem de ser **pequena/cirúrgica** (só desfecho relevante), não a massa.
2. Para cobertura **completa** de narrativa/prescrição/vitais, o caminho robusto é **exportação do
   fornecedor** (Eco Sistemas), não o scraping de tela. Vale pedir.
3. O que **não** depende disso já está resolvido barato: jornada+cor+CID pelos relatórios
   (667/631/526), e o painel pelo hub.

> **Nota de método:** parei de forçar o popup para não martelar a produção do fornecedor
> (guardrail). O número exato do deep sai quando (a) tivermos um prontuário com atendimento
> confirmado e (b) decidirmos dirigir o popup — ou, melhor, quando o fornecedor expuser a
> exportação e o scraping deixar de ser necessário.

## 4a. Validação de paridade SQL × Web (o teste de aceite antes de substituir)

Antes de o conector web **substituir** o SQL, provar que ele entrega **os mesmos dados no mesmo
formato**. O método (ideia do operador):

1. **Escolher a bancada nas UPAs, não no Conde.** As UPAs já estão no hub via SQL — então dá para
   comparar. O Conde não tem SQL no hub (é a lacuna), então serve só de validação estrutural depois.
2. **Amostra:** N boletins (`spa_codigo`) de um dia de uma UPA que o hub já tem via
   `meta.source=…/klinikos/upa24h-…`.
3. **Rodar a estratégia web** para os MESMOS boletins/dia e montar os recursos FHIR.
4. **Diff campo a campo** contra o que está no hub (vindo do SQL): Encounter (period, class,
   status, discharge), Condition (CID), Observation (vitais), Patient (identidade). Relatório de
   paridade: igual / diferente / ausente, por campo.

**O critério é o RESULTADO FINAL CONVERGIDO, não o meio do caminho.** É esperado (e ok) que os
campos cheguem em tempos diferentes — a espinha primeiro, o deep depois pela fila. O aceite é:
**depois que a espinha E o deep já rodaram para o boletim, o recurso FHIR do web = o do SQL.** A
diferença legítima é *quando* cada campo aparece, nunca o *conteúdo* final. Então o diff se faz
sobre o **estado convergido**: rodar, para os N boletins da amostra, espinha + deep até o fim, e
só então comparar. Tem de bater no final: Encounter (period, class, status, discharge), Condition
(CID), Observation (vitais), DocumentReference (narrativa), MedicationRequest (prescrição), Patient.

**Deltas de formato a normalizar até o final bater (o teste revela, a gente reconcilia):**
- **CID código × texto — o de-para.** O SQL traz **código+nome** (de `TB_CID`); o 526 (e as telas)
  trazem o **texto** do diagnóstico, e às vezes **sem o código**. Onde não vier o código, é
  obrigatório um **de-para texto→CID** para preencher o `Condition.code` no mesmo formato do SQL
  (senão o diff acusa "diferente" só por formato). O de-para: normaliza o texto (maiúsculas, sem
  acento, sem o sufixo entre colchetes) e casa contra `TB_CID` (que o hub/o conector já conhece);
  o que não casar entra numa lista de exceções para tratamento manual — nunca inventar código.
- **Precisão de hora:** SQL tem timestamp exato; o relatório web dá `HH:MM`. Definir tolerância
  (casar por minuto) ou buscar o segundo na tela do deep. Onde a fonte web genuinamente não tem o
  segundo, é limite da fonte, não erro — fica **documentado** como tolerância aceita.
- **Vitais/narrativa:** vêm pelo deep; no estado convergido têm de existir e casar valor a valor.

Ou seja, o alvo é **equivalência eventual**: mesmo conteúdo final, ordem de chegada diferente. O
relatório de paridade é o **critério de aceite** para ligar a substituição das UPAs e define o
contrato de formato (o que casa exato, o que casa com tolerância) antes de confiar no web para o
Conde, onde não há SQL para comparar.

## 4b. Harness de custo por boletim (pronto para rodar pontualmente)

Falta o número que decide o item 4: **quantas requisições e quantos segundos custa puxar a camada
profunda de UM boletim** pelo caminho de leitura (`ResumoProntuario.aspx` por prontuário, ou 791
por paciente). Isso exige abrir a tela de um paciente real (PII → fica em `capturas/`, gitignored)
na produção do fornecedor. Proposta: `medir_custo_boletim.py` que roda numa amostra pequena
(3–5 boletins reais tirados do 407), cronometra o ciclo completo e grava **só os agregados**
(req/boletim, s/boletim) — sem dado de paciente no doc. **Não rodo sem seu OK**, porque bate em
tela de paciente na produção deles e você pediu para não onerar.

> Popular o hub e mudar o conector são ações de produção + carga em massa — só com OK por passo
> ([[feedback_producao_confirmar_antes]], [[feedback_carga_massa_verificar_pela_api]]).
