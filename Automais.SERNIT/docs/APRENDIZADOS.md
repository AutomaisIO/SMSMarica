# SERNIT — SER de Niterói: recon (laboratório `Automais.SERNIT/`)

Espelho do laboratório `Automais.SER/` (SES-RJ), agora para o **SER de Niterói**
(`regulacao.niteroi.rj.gov.br/ser`). Mesma plataforma, **instância e build diferentes**.
Tudo abaixo foi **medido** contra o SERNIT real em **25/08/2026** — a regra do SER-RJ vale
aqui também: *medir, nunca assumir* (ver `../../docs/ser.md`).

> **Somente leitura.** Login e telas de *pesquisa* são POST mas não alteram dado. A trava
> `guardar()` (em `sernit/client.py`) recusa qualquer parâmetro com verbo de escrita, como no
> SER-RJ. Escrita (FollowUP, telefones) só depois de mapeada e autorizada por ação, e assinada
> pelo operador — igual ao SER-RJ (`../../docs/ser.md §7,§9,§10`).

## 1. Stack (idêntica ao SER-RJ, build mais antigo)

| | |
|---|---|
| Servidor | **WildFly/10** (header `Server`) |
| Camada web | JSF 1.2 (Mojarra) + RichFaces / Ajax4JSF **3.3.3.Final** (`ui.pack.js`/`framework.pack.js`) |
| Framework | JBoss Seam (`.seam`, conversas `cid`) |
| **Build** | **`2024-01-01-NITEROI`** (o SER-RJ estava em `2026-06-30#26-80.00`) |
| Nó de balanceador | sufixo `;jsessionid=….fms-sersaudev2` |

**Consequência prática do build ser outro:** os ids `j_idNN` **não batem** com os do SER-RJ.
Nada de `j_id` chumbado — resolver por rótulo, por `<select>` que oferece o valor esperado, ou
lendo o script da própria página. Já medido: Situação = `form0:j_id54` aqui (era `form0:j_id75`
no SER-RJ); Pesquisar = `form0:j_id75` aqui (era `form0:j_id96`). Os nomes colidem entre telas —
prova de que o número não significa nada.

## 2. Sessão e cookies

- **`JSESSIONID`** — a sessão de verdade (path `/ser`).
- **`UCID`** — cookie do balanceador. **É a diferença de nome vs o SER-RJ, que usa `SERVERID`.**
  O jar do `httpx` cuida sozinho; só não descartar o jar entre requisições.
- **`cid`** — conversation id do Seam, novo a cada conversa. Nunca fixar; ler do `Location`/página.

## 3. Login — o passo que difere de verdade do SER-RJ

No SER-RJ, `GET /ser/login` já devolve o form. **No SERNIT não.** `/ser/login` (e `/`, e
qualquer `.seam` protegido) devolve uma página **"AGUARDE..."** com
`<meta http-equiv="Refresh" content="0; URL=logout.jsp">`; `logout.jsp` reflete de volta para
`login` — um **loop de meta-refresh** (trava até o navegador, testado). Uma sessão nova recebe
AGUARDE por padrão, mesmo com cookies limpos.

**O que destrava:** `/ser/login` só entrega o form real quando o **`Referer` é `logout.jsp`**
(guarda anti-acesso-direto). A sequência que funciona — a mesma que o navegador faz — é:

| # | Requisição | Detalhe |
|---|---|---|
| 1 | `GET` tela protegida (`solicitar-consulta-pesquisar.seam`) | acende a conversa Seam; responde AGUARDE/404 com `?cid` |
| 2 | `GET /ser/logout.jsp` | invalida; **vira o `Referer`** do próximo passo |
| 3 | `GET /ser/login` (Referer = `logout.jsp`) | **agora vem o form real** (traz `login.css`, `logo_nit.png`) |
| 4 | `POST /ser/login` | `login`, `login:username`, `login:password`, botão *Entrar*, `javax.faces.ViewState` |

Por isso `SernitSession` manda **Referer automático** (= última URL visitada), como um navegador.

Detalhes medidos do form (passo 3/4):
- `action = /ser/login`; ViewState inicial `j_id1`.
- Botão *Entrar* = `login:j_id17` — **id volátil**, lido do `<input type=submit>`, nunca fixado.
- Login OK ⇒ `POST` redireciona para `/ser/home?cid=N` (não volta o `login:username`).

## 4. Home e módulos

`GET /ser/home.seam`. Usuário logado: **`jose.marica` — "SECRETARIA MUNICIPAL DE SAUDE DE MARICA"**
(é uma conta de Maricá **dentro** do SER de Niterói). Menu: Home, Alterar Senha, Contato,
Suporte, Manual, Logout.

**Só um módulo é oferecido: `ambulatorial`** (o SER-RJ tinha ambulatorial/internacao/oncologia/
neonatal/ostomizados). Ativação = replicar `goModulo('ambulatorial')`:

```
POST /ser/home
  <form>            = <form>            # id do form lido da home (j_id23 em 25/08)
  <form>:goModulo   = <form>:goModulo   # similarityGroupingId, lido do A4J.AJAX.Submit
  param1            = ambulatorial
  AJAXREQUEST       = <form>
  AJAX:EVENTS_COUNT = 1
  javax.faces.ViewState = <ViewState do form>
```

Responde com `Location` (header ou `<meta name="Location">`); **seguir o `Location`** ativa o
módulo na sessão. Sem isso, a tela de pesquisa dá erro (mesma regra do SER-RJ §3).

## 5. Tela de pesquisa de Solicitação (`form0`)

`/ser/pages/consultas-exames/solicitacao/solicitar-consulta-pesquisar.seam` — `form0`.

| Campo | Id/name (25/08) | Observação |
|---|---|---|
| Tipo | `form0:comboTipoRecurso` | `Selecione…`, `CONSULTA`, `EXAME` |
| Recurso | `form0:comboRecurso` | depende do Tipo (autocomplete/combo) |
| **Situação** | `form0:j_id54` | `EM_FILA`, `PENDENTE`, `AGENDADA`, `CHEGADA_NAO_CONFIRMADA`, `CANCELADA`, `CHEGADA_CONFIRMADA`, `ALTA` — **resolver pelo `<select>` que oferece `EM_FILA`**, nunca pelo `j_id54` |
| CPF / Nome / CNS | `form0:cpf`, `form0:nome`, `form0:cns` | texto |
| Id Solicitação | `form0:idSolicitacao` | texto |
| Data da Solicitação | `form0:dt{Inicial,Final}SolicitacaoInputDate` | eixo de fatiamento |
| Data do Agendamento | `form0:dt{Inicial,Final}AgendamentoInputDate` | mutável — não usar p/ fatiar |
| **Data da Regulação** | `form0:data{Inicial,Final}RegulacaoInputDate` | **eixo que o SER-RJ não tem** |
| **Pesquisar** | `form0:j_id75` | `<input type=submit value="Pesquisar">` |

Diferenças relevantes vs SER-RJ:
- **Não há campo de Solicitante/Unidade solicitante** e **não há botão Exportar** — reflexo de a
  conta já ser **escopada a Maricá**. Toda a saga da "amarração do solicitante pelo autocomplete"
  do SER-RJ (`../../docs/ser.md §4.3`) **provavelmente não se aplica aqui** — mas confirmar por
  fatiamento antes de afirmar cobertura (é a lição que custou ~35% da base no SER-RJ).
- **A busca precisa de `AJAXREQUEST`** (medido: postback comum volta grade vazia; com
  `AJAXREQUEST=form0` a grade vem). É a armadilha §3.1 do SER-RJ, presente aqui também.

### 5.1 Grade e paginação

- Grade `form0:listagem`, **12 colunas úteis**: ID, Tipo, Recurso, Data da Solicitação, CNS,
  Paciente, Idade, CID, Agendado para, Situação, Ação. **Sem CPF / Solicitante / Município
  Solicitante** (o SER-RJ tinha 14). IDs de solicitação são **curtos** (ex.: `1742`) — espaço de
  IDs próprio, que **colide** com SER-RJ/SISREG ⇒ no porte, chavear por `(fonte, codigo_externo)`.
- Paginador `rich:datascroller` **`form0:sc1`** (mesmo nome do SER-RJ): 20/página, navegação via
  `A4J … {'page':'N'}` com `ajaxSingle=form0:sc1`.

#### Teto de 100 na grade, MAS o total real é informado (medido — melhor que o SER-RJ)

A grade lê no máximo **100 registros** (5 páginas × 20). Quando a janela tem mais que isso, o
box `form0:msgErro` traz **o total verdadeiro**:

> *"Consulta muito ampla, retorno limitado em 100 resultados. É recomendado restringir a consulta.
> **Total de resultados encontrados: 415**"*

Medido (EM_FILA, eixo **Data da Solicitação**):

| Janela | Total real (msgErro) | Grade capada? |
|---|---|---|
| sem filtro | **415** | sim (só 100 legíveis) |
| 2024 | (sem mensagem) → ≤100, **completa** | não |
| 2025 | **107** | sim |
| 2026 | **243** | sim |

Regras que saem daí (âncoras da varredura):
- **O filtro de Data da Solicitação FUNCIONA** (recorta de verdade) — medido.
- Se aparece `limitado em 100` ⇒ a grade está capada; use **`Total de resultados encontrados: N`**
  para saber o total e **subdivida a janela** até `N ≤ 100`, então pagine as ≤5 páginas.
- Se **não** aparece a mensagem ⇒ a janela cabe; a contagem real = soma das linhas das páginas.
- **O total só é confiável quando lido de `Total de resultados encontrados:`** — o texto
  "100 resultados" é o *aviso de corte*, não o total. Deduplicar por `ID Solicitação` entre janelas.
- Estratégia = **janela adaptativa sobre a Data da Solicitação**, encolhendo enquanto vier o
  `limitado em 100` (igual ao SER-RJ §4.1, mas aqui a leitura é por **paginação**, não export).

### 5.2 Menu *Ação* (Opções) — por situação (medido, todas as 7)

| Situação | Itens do menu Ação |
|---|---|
| EM_FILA | Visualizar, **Editar**, Historico da Solicitação, Cancelar, Registrar FollowUP |
| PENDENTE | Visualizar, **Editar**, Historico da Solicitação, Cancelar, Registrar FollowUP |
| AGENDADA | Visualizar, *Emitir Autorização Completa*, *Emitir Chave de Autorização - 3ª via*, Historico, Registrar FollowUP |
| CHEGADA_NAO_CONFIRMADA | Visualizar, *Emitir Autorização Completa*, *Emitir Chave de Autorização - 4ª via*, Historico, Registrar FollowUP |
| CHEGADA_CONFIRMADA | Visualizar, *Emitir Autorização Completa*, *Emitir Chave de Autorização - 3ª via*, Historico, Registrar FollowUP |
| CANCELADA | Visualizar, Historico da Solicitação, Registrar FollowUP  — **sem Editar/Cancelar** |
| ALTA | **nenhuma ação — sem Histórico** |

Confirma o SER-RJ §4.2: **Editar** só em EM_FILA/PENDENTE; **Cancelada** perde Editar/Cancelar;
**Alta** não tem ação nem Histórico. Novidade vs SER-RJ: as situações agendadas/chegada trazem
**Emitir Autorização Completa** e **Emitir Chave de Autorização - Nª via** (a "Nª via" conta
reemissões). Ids `form0:listagem:<i>:j_idNN` posicionais na página corrente (não são o ID da
solicitação) — **localizar sempre pelo texto**.

> Observação de contagem: as 7 situações reportam grade capada em 100, mas o total real de cada
> uma é lido no `Total de resultados encontrados:` (§5.1) — não confundir com o "100 resultados"
> do aviso de corte.

Abertura do item (A4J): POST no `action` do `form0` com `AJAXREQUEST=_viewRoot`,
`ajaxSingle=<link>`, `<link>=<link>` + hidden do form + ViewState; a resposta traz
`<meta name="Location">` para a tela de destino, que se segue com `GET`.

### 5.3 Histórico da Solicitação (tela rica) — medido

Aberto pelo item *Historico da Solicitação* (id posicional `form0:listagem:0:j_id133` em 25/08).
Dois blocos, como no SER-RJ (§5), mas **parsear pelo `<label>`/`<td>` do mesmo par**, porque os
ids são readonly e voláteis:

- **Dados do paciente:** Paciente, CNS, Nome Mãe, Sexo, Data Nascimento, CEP, UF, Município,
  Tipo Logradouro, Logradouro, Número, e **três telefones**:
  - **Telefone Residencial** — `form0:j_id63` (id posicional; medido com valor)
  - **Telefone SMS** — `form0:j_id65` (id posicional; **é o campo de notificação**, equivale ao
    "WhatsApp" do SER-RJ)
  - **Telefone** (contato) — terceiro rótulo
  > Resolver **sempre pelo rótulo**, nunca pelos `j_id63`/`j_id65` — a próxima recompilação da
  > plataforma renumera (mesma armadilha do SER-RJ §10, onde os telefones eram `j_id173`/`j_id178`).
- **Trilha de eventos** — tabela com colunas **ID, Data, Evento, Estado Anterior, Estado Atual,
  Central regulação, Unidade Executora, Usuário, Lotacao Evento, IP, Observação** (praticamente
  igual ao SER-RJ §5, com uma coluna `ID` a mais). Uma solicitação de exemplo trouxe **17 eventos**.
  Eventos esperados: Solicitar / FollowUP / Pendenciar / Cancelar.

### 5.4 Aba EDITAR — escrita de telefone (mapeada e EXERCITADA reversivelmente 25/08)

Abre pelo item *Editar* do menu (só EM_FILA/PENDENTE). **A aba Editar tem `action` PRÓPRIO:**
`/ser/pages/consultas-exames/solicitacao/solicitar-consulta-**editar**.seam` — e é aí que o
Gravar deve postar. **Postar no `pesquisar.seam` (caminho constante) DERRUBA A SESSÃO** (volta o
login com HTTP 200, sem mensagem) — foi assim que a §3.3 do SER-RJ se confirmou aqui, na prática.

- **Telefones (por rótulo):** *Telefone Residencial* (`form0:j_id157`) e *Telefone Celular*
  (`form0:j_id159`) — ids posicionais/voláteis (e DIFERENTES dos da tela de Histórico!); resolver
  **sempre pelo rótulo**.
- **Campos `disabled` (o navegador não envia):** `numeroCNS`, `nome`, `dataNascimento`, `nomeMae`,
  `especialidadeMedico` — e o **CPF** entra nessa lista **quando o paciente TEM CPF**.
- **CPF é obrigatório para gravar.** Paciente **sem CPF**: `form0:cpf` vem **habilitado e vazio** e
  o Gravar falha com *"CPF é obrigatório"* (partial A4J em `form0:msgErro`). Paciente **com CPF**:
  `form0:cpf` vem **disabled** (como as demais identidades) e o Gravar passa. (Coerente com o
  ADR-0041: há muitos pacientes sem CPF na fila — 1742, 10062, 43303 medidos sem CPF.)
- **Gravar** = `<input value="Gravar">` (id volátil, ex. `form0:j_id283`; resolver por valor),
  A4J em `form0` (`AJAXREQUEST=form0` + `<gravar>=<gravar>` + ViewState do form0).
- **Sucesso:** *"Solicitação de nº N salva com sucesso."* — mas, como no SER-RJ, **a prova é
  reler e comparar só os dígitos** (o SER aplica máscara ao gravar). Medido na solic **9891**
  (com CPF): Residencial `...929` → `...921`, releitura confirmou, **restaurado** e conferido em
  sessão independente; *Telefone Celular* intacto; situação preservada (seguiu EM_FILA).
- **Trava invertida:** o POST parte do renderizado (`comoNavegador`, sem disabled) e só o
  telefone-alvo muda; recusa se qualquer outro campo divergir. Escrita sai pela porta separada
  `SernitSession.post_escrita()` (loga a operação), nunca pela `post()` travada.

### 5.5 Registrar FollowUP — MAPEADO, não enviado (ensaio)

Igual ao SER-RJ §9: **não mora no `form0`**. O item do menu abre um **modal com form próprio**.
Medido (solic 1742), tudo posicional/volátil — localizar o form pelo par *textarea + Gravar*:

| Peça | Id (25/08) |
|---|---|
| form do modal | `j_id142` |
| textarea (observação) | `j_id142:j_id150` |
| Gravar (POST comum) | `j_id142:j_id152` |
| Cancelar | `j_id142:j_id153` |
| ViewState **de dentro do modal** | `j_id5` |

Campos do POST (quando for enviar, com autorização): `j_id142=j_id142`, `j_id142:j_id150=<texto>`,
`j_id142:j_id152=<gravar>`, `autoScroll=`, `javax.faces.ViewState=j_id5`, no `action` do form do
modal. **O Gravar do FollowUP NÃO foi postado** — sonda `probe_followup.py` é ensaio por design.
Como no SER-RJ, o evento carregará o **usuário logado** (aqui `jose.marica`/GESTOR de Maricá):
quem realmente pediu tem de ir no TEXTO.

### 5.6 Nova solicitação (aba Editar) + campos dinâmicos + CID — medido 25/08

A aba de **criação** é um `rich:tab`, aberta por `form0:editar_server_submit` (POST comum no
`action` do form0; a resposta redireciona por `<meta Location>` para `solicitar-consulta-editar.seam`).

**Bloco fixo** (nomes JSF medidos):
- Tipo: `form0:comboTipoRecurso` (CONSULTA/EXAME) · Recurso: `form0:comboRecurso`
- Médico responsável: `form0:medicoResp` (234 opções) · Classificação de risco:
  `form0:classificacao_risco` (`EMERGENCIA`, `URGENCIA`, `PRIORIDADE_NAO_URGENTE`,
  `CONSULTA_BAIXA_COMPLEXIDADE`, `NAO_CLASSIFICADO`)
- CNS/paciente: `form0:numeroCNS` · Hipótese/CID: `form0:procedimento` (suggestionbox)
- Radios (Sim/Não): `form0:naturezaSolicitacaoMandato_radio`,
  `form0:booleanMedicoSolicitanteIdentificado_radio`, `form0:unidadeDeOrigemIdentificada_radio`
- **Não há radio "É ambulatório estadual?"** (o SER-RJ tem; aqui "AMBULATÓRIO…" é só um *valor* de
  recurso). O bloco de campos base é sempre **Queixa Principal / Resultado de Exames / Observações**.

**A pegadinha do combo dependente:** trocar Tipo/Recurso é A4J com **`AJAXREQUEST=_viewRoot`**
(NÃO `form0`, apesar de o `onchange` dizer `A4J.AJAX.Submit('form0',…)`) + `ajaxSingle=<combo>` + o
id do evento lido do `similarityGroupingId` do `onchange` (volátil). Com `form0` o `comboRecurso`
volta **`disabled` e vazio**; com `_viewRoot`, popula. (Mesma família do §4.3 do SER-RJ.)

**Estrutura do `campoDinamicoBox` (verificada 25/08, recurso 431):** o box é **plano** — o rótulo
do campo é o `<label sem for>` que o **precede**; o obrigatório é um `<label>*</label>` (ou
`span.required`) entre o rótulo e o campo; radio/checkbox têm **um `<input>` por opção** (mesmo
`name` `form0:dinamico_id_N`, ids `…:0`/`…:1`) com o texto em `<label for="…:0">SIM</label>`. Não há
o wrapper `container_dinamico_id_N` do SER-RJ. (O parser do backend faz um walk em ordem de
documento acumulando rótulo/obrigatório.)

**Pesquisa de paciente na aba Nova (verificada 25/08):** campo **`form0:numeroCNS`** + botão
**`<input value="Pesquisar">`** (id volátil `form0:j_id60`, NÃO `<a title>`) → A4J `_viewRoot`;
popula o painel **`form0:painelDadosDoPaciente`**. Identidade travada (`disabled`): nome, CPF (quando
tem), nascimento, mãe, sexo, raça; editáveis: endereço + telefones (Residencial `form0:j_id152`,
Celular `form0:j_id157`, Comercial `form0:j_id159` — ids posicionais).

**Campos dinâmicos:** vivem em `<span id="form0:campoDinamicoBox">` (vazio até escolher o Recurso).
Catálogo completo (`capturas/campos_dinamicos_sernit.json`): **CONSULTA = 43 recursos em 3
formulários; EXAME = 35 recursos em 5 formulários** — bem mais simples que o SER-RJ (21). As
assinaturas: (a) base (Queixa/Resultado/Observações); (b) "internação" (Peso/Altura/IMC gramas +
Condições que Justificam a Internação + Principais Sinais e Sintomas + Principais Resultados);
(c) cirúrgico (Classificação do Risco Cirúrgico + 7 radios + Outros); e uns poucos especiais
(Histeroscopia, Videolaringoscopia com Grau Histopatológico, Eletrocardiograma com
`OBS_CONSULTA_EXAME_REDE`).

**CID (Hipótese) — suggestionbox `form0:procedimento`, box volátil `form0:j_id203`:** o fetch de
sugestões manda `inputvalue=<termo>` + `<box>=<box>` + `ajaxSingle=<box>` com `AJAXREQUEST=_viewRoot`;
a resposta traz a tabela `<box>:suggest`, cada linha `[textoDeSelecao, código, descrição]` — o
`textoDeSelecao` é `"(E10 ) Descrição"` (código com espaço até 4 chars, entre parênteses), e é ele
que o SERNIT espera de volta em `form0:procedimento` ao gravar (igual ao SER-RJ). Medido: "diabetes"
→ 70 CIDs. O onselect grava o índice no hidden `form0:j_id203_selection`.

## 6. O que já funciona no lab

- `sernit/client.py` — `SernitSession` (login + Referer automático + trava de leitura),
  `ativar_modulo()`, parsers (`sopa`, `viewstate`, `hidden_do_form`, `campos_todos`,
  `seguir_redirect_a4j`) e `login_e_modulo()`.
- `probe_login.py` — login + build + módulos + abre a pesquisa. **OK.**
- `probe_pesquisa.py` — mapa da tela `form0` (selects, campos, botões, colunas). **OK.**
- `probe_grade.py` — busca por situação, conta registros, lê colunas, menu de Ação, avisos.
  **OK** (EM_FILA → 20/página, "100 resultados", AJAXREQUEST necessário).
- `probe_historico.py` — abre o Histórico da 1ª linha (ou de um ID) e mapeia os dois blocos.
  **OK** (dados do paciente + 3 telefones + trilha de 17 eventos).
- `probe_menu_acao.py` — menu de Ação das 7 situações. **OK** (tabela do §5.2).
- `probe_fatiar.py` — prova o teto de 100 e lê o total real por janela de Data da Solicitação.
  **OK** (EM_FILA = 415 reais; estratégia de varredura em §5.1).
- `probe_nova.py` — abre a aba de nova solicitação e mapeia o bloco fixo. **OK** (§5.6).
- `probe_campos_dinamicos.py` — cataloga os campos dinâmicos por recurso. **OK** (43/35 recursos).
- `probe_cid.py` — sonda o autocomplete de CID (Hipótese). **OK** (fetch por `inputvalue`).
- `probe_editar.py` — mapa da aba Editar; com `--testar-telefone`, escrita **reversível** de
  telefone (grava → relê → restaura). **OK** (solic 9891, com CPF).
- `probe_followup.py` — abre e mapeia o modal de FollowUP. **Ensaio: nunca posta o Gravar.**

`.env`, `credenciais_sernit.txt` e `capturas/` são **gitignored** (credencial e PII).

## 7. Próximos passos (para chegar a "TUDO igual no SER")

Já mapeado e medido: login, home/módulo, tela de pesquisa, **grade/paginação com teto de 100 +
total real**, **menu Ação das 7 situações**, **Histórico**, **Editar (escrita de telefone provada
e reversível)** e **FollowUP (mapeado, não enviado)**. Falta:

1. **Refinar o parser do Histórico** para casar rótulo→valor por par de `<td>` (hoje o alinhamento
   é aproximado) e ler a trilha de eventos linha a linha.
2. **Ligar o FollowUP** (enviar de verdade) — só com **autorização por ação**; a escrita é
   assinada pelo operador, não pela credencial de sincronismo (`../../docs/ser.md §7,§9`).
3. **Sincronismo / motor / janela de notificação** — portar para o backend
   (`SMSMais.Core/Integracoes/`), decidindo espelho (`sernit_*`) vs. reuso do arcabouço `ser_*`.
   Ver ADR-0042 e `../../docs/ser.md §11`. A varredura é por **janela adaptativa de Data da
   Solicitação + paginação** (§5.1); o campo de notificação é o **Telefone SMS** (§5.3); chavear
   por `(fonte, codigo_externo)` porque os IDs colidem com SER-RJ/SISREG. **Atenção:** editar
   telefone exige CPF no cadastro (§5.4) — pacientes sem CPF não podem ter contato corrigido pela
   tela até que o CPF entre.
