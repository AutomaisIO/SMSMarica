# Klinikos — recon "como usuário" (laboratório `Automais.klinikos/`)

Espelha o **método** dos laboratórios `Automais.SER/` e `Automais.SERNIT/` (sondas
`probe_*.py`, cliente httpx com Referer automático, trava de somente-leitura), mas o alvo e a
pergunta são outros: **quais endpoints a web do Klinikos usa** para entregar os mesmos dados que
hoje o conector lê direto do SQL Server (`KlinikosImportacaoStrategy`: unidades, profissionais,
pacientes, boletins `Pronto_Atendimento`, desfechos, `UPA_Evolucao`, `UPA_Atendimento_Medico`,
prescrição, `UPA_SinaisVitais`, `TB_CID`). A meta é substituir a leitura direta ao banco por
acesso pela aplicação, com um usuário.

Tudo abaixo foi **medido** contra `klinikosconde.smsmarica.online` (instância do **Conde**,
HOSPITAL MUNICIPAL CONDE MODESTO LEAL, `unid_codigo` **0005**) em **16/09/2026**. Regra do SER
vale aqui: *medir, nunca assumir*.

> **O Conde já roda Klinikos.** A memória do projeto (ago/2026) dizia que o Conde ainda era
> Salux e migraria "no futuro"; esta instância prova que migrou. Consequência imediata: as
> consultas Oracle do Painel do Secretário para o Conde estão mortas — é o item 1 do plano em
> `inventario-consultas-atuais.md`.

Documentos irmãos: `inventario-consultas-atuais.md` (o que lemos hoje por SQL × fonte web +
política de requisições + plano de corte), `sondagem-relatorios.md` (parâmetros e colunas de
cada relatório sondado), `catalogo-relatorios.md` (268 relatórios/271 telas dos menus),
`mapa-endpoints.md` (368 telas rastreadas).

> **Somente leitura.** A trava `guardar()` (em `klinikos/client.py`) recusa qualquer parâmetro
> de POST — e qualquer valor de `__EVENTTARGET` — com verbo de escrita. Login, gate de local e
> postbacks de pesquisa/relatório são POST e não alteram dado clínico. As únicas liberações
> pontuais são `LoginButton`, `btnConfirmarLogin` e `imbSalvar` do gate (grava só o cookie do
> local de atendimento).

## 1. Stack

| | |
|---|---|
| Servidor | nginx na frente; `X-AspNet-Version: 4.0.30319`, `X-Powered-By: ASP.NET` |
| Camada web | **ASP.NET WebForms** (`__VIEWSTATE`, `__VIEWSTATEGENERATOR`, `__EVENTVALIDATION`, `WebForm_DoPostBackWithOptions`), master page `aspnetForm` |
| Componentes | **Telerik 2011.3.1115.35** (RadMenu, RadGrid, RadComboBox, RadDatePicker), Bootstrap 5, jQuery 3.7, SweetAlert2, select2, DataTables |
| Relatórios | **Crystal Reports** no servidor (`rptview.aspx` → PDF; `rptviewXls.aspx` → XLS BIFF) |
| Build | **`K.2024.09.2.1`** (rodapé da tela de login, `lblVersao`) |
| Fornecedor | Eco Sistemas (`ecosistemas.com.br`) |
| Raiz da app | `/KlinikosNet/` |
| Sem Swagger | 34 caminhos óbvios (`/swagger`, `/api`, `/odata`, `?WSDL`…) → 404/500. **Não há API documentada.** |

## 2. Sessão, login e gate — o que precisa de ViewState e o que não precisa

**Resposta curta para "depois do levantamento, precisaremos de ViewState?": não, só no login.**
Depois de autenticado, tudo que interessa é **cookie + GET/JSON**, sem manter ViewState nenhum.

### 2.1 Cookies

| Cookie | Quem põe | Papel |
|---|---|---|
| `ASP.NET_SessionId` | primeiro GET | sessão (HttpOnly, SameSite=Lax) |
| `eco-web-auth` | POST de login | **Forms Authentication** — é a credencial de verdade |
| `_codigo_unidade` | gate do local | unidade corrente (`0005` = Conde nesta instância) |
| `_LOCAL_NAME` | gate do local | nome do local de atendimento |

Sem cookie válido: `.aspx` protegido → 302 `Share/Erros/sessaoexpirada.aspx`; raiz → 302
`Login.aspx?ReturnUrl=…`.

### 2.2 Login (o ÚNICO passo que exige ler ViewState — e só o da própria tela)

`GET /KlinikosNet/Login.aspx` → `POST` do `form1` com `__VIEWSTATE`, `__VIEWSTATEGENERATOR`,
`__EVENTVALIDATION` (lidos da tela), `LoginView1$lgAcesso$UserName`,
`LoginView1$lgAcesso$Password`, `LoginView1$lgAcesso$LoginButton=ENTRAR`.

**Sessão única por usuário (confirmado):** se o mesmo usuário já está logado em outra
estação, o POST devolve a própria `Login.aspx` com *"O seu login está autenticado em outra
estação. Caso efetue o login nesta estação, a autenticação na outra estação será desfeita.
Confirma o login nesta estação?"* e dois submits: `btnConfirmarLogin=CONFIRMA` /
`btnCancelarLogin=CANCELA`. Confirmar **derruba a outra sessão**. Consequências:

- o laboratório persiste os cookies em `capturas/sessao.json` e só reloga se expirou
  (`KlinikosSession.entrar()`), para não derrubar o operador a cada sonda;
- **em produção o conector precisa de um usuário próprio** (não pode dividir com uma pessoa).

Login OK → 302 para `UPA/GravaCookie.aspx`.

### 2.3 Gate do local de atendimento (2 postbacks, uma vez por sessão)

Toda tela protegida redireciona para `UPA/GravaCookie.aspx` até escolher o local. Form:
`ddlPortadeEntrada` (GUIDs: SUTURA, URGÊNCIA E EMERGÊNCIA, URGÊNCIA E EMERGÊNCIA 2,
MATERNIDADE, PEDIATRIA, INTERNAÇÃO…), `ddlLocalFisico` (cascata), `ckbAcessoAdministrativo`
e o ImageButton `imbSalvar`. Checkbox e porta têm **AutoPostBack** → o navegador faz 2 POSTs:
(1) `__EVENTTARGET=…ckbAcessoAdministrativo` com o checkbox marcado; (2) `imbSalvar.x/.y`.
Grava `_codigo_unidade` + `_LOCAL_NAME`.

Armadilhas medidas:
- `<select>` **sem opções não vai no POST** (o navegador também não manda). Mandar `""` dispara
  *"Invalid postback or callback argument"* (event validation). `campos_todos()` já pula.
- ImageButton posta como `nome.x`/`nome.y`.
- No Git Bash, argumento começando com `/KlinikosNet/...` vira caminho Windows —
  `export MSYS_NO_PATHCONV=1` antes de chamar as sondas.

### 2.4 ASP.NET serializa requisições da mesma sessão

Duas sondas na mesma sessão (varredura + relatório) enfileiram no lock de sessão do ASP.NET;
com Crystal na frente, o nginx devolve **504** em ~60 s. Não é o endpoint que falhou — é fila.
Rodar uma coisa por vez, ou aceitar latência.

## 3. Os endpoints que entregam dado — sem ViewState

### 3.1 Relatórios: `Relatorios/rptview.aspx` e `Relatorios/rptviewXls.aspx` (GET)

Os botões *Imprimir*/*Excel* da tela de parâmetros (`Relatorios/ParametroRelatorio.aspx?parrel=N&Modulo=X`)
**não devolvem o relatório**: devolvem a tela com um `window.open` para

```
Relatorios/rptview.aspx?parRel=631&parNum=5&par1=01/09/2026&par2=30/09/2026&par3=0005&par4=0109&par5=-1&Modulo=UPA
Relatorios/rptviewXls.aspx?parRel=631&parNum=5&par1=…&par5=…&Modulo=UPA
```

Ou seja, **o relatório é um GET com parâmetros posicionais** (`parNum` = quantos; `par1..parN`).
Para o 631: par1/par2 = período, **par3 = unidade** (`_codigo_unidade`), par4 = especialidade,
par5 = profissional (`-1` = todos). Relatórios sem parâmetro usam `parNum=1&par1=unidDef`.

| Endpoint | Devolve | Medido |
|---|---|---|
| `rptview.aspx?parRel=629&parNum=1&par1=unidDef` (Fila de Espera) | **`application/pdf`** 1,8 MB, 37 páginas | colunas: Nº Boletim, Nome, Início Atendimento, Tempo, Clínica |
| `rptviewXls.aspx?parRel=629&…` | **`application/vnd.ms-excel`** 375 KB, **XLS binário (BIFF)**, `filename=Relatorio.xls.xls` | lê com `xlrd` |
| `rptview.aspx?parRel=631&…` (1 mês, Clínica Médica) | página `RptView` com **`System.OutOfMemoryException`** do Crystal | intervalo grande estoura o servidor |
| `rptviewXls.aspx?parRel=631&…` (1 mês) | **504** (nginx) | ver §2.4 — fila + Crystal |

Ordem de preferência para scraping: **XLS (estruturado) > PDF (texto posicional)**. O PDF do 629
já mostrou lixo de teste (paciente "TESTE" com 10.968 h de espera desde 16/06/2025) — o
relatório espelha o banco, inclusive o que ninguém limpa.

**O layout `parN` muda de relatório para relatório** — não dá para chutar; descobrir pelo
`window.open` que a tela de parâmetros gera (é o que `probe_relatorio.py`/`sondar_relatorios.py`
fazem). Medidos:

| parrel | tela | `rptviewXls` gerado | XLS (1 dia) |
|---|---|---|---|
| 631 Atendimentos em Andamento | ParametroRelatorio (datas, especialidade, profissional) | `parNum=5&par1=ini&par2=fim&par3=0005&par4=<esp\|-1>&par5=<prof\|-1>` | 15/09 só: vazio; 15–16/09 todas esp.: **425 linhas** — Nº Boletim, NOME, Início Atendimento, TEMPO, CLINICA, PROFISSIONAL |
| 407 Pacientes Registrados no Dia | ParametroRelatorio (datas) | `parNum=4&par1=0005&par2=ini&par3=fim&par4=1` | 545 KB, ~2.000 linhas (cabeçalho repetido por registro) — Nº Boletim, Prontuario, Paciente, Dt. Nascimento/Idade, Clinica |
| 56 / 57 Registro Diário/Mensal por Clínica | idem | `parNum=4&par1=0005&par2&par3&par4=3\|1` | tabela de **totais** (9–11 linhas) — barato para contagem |
| 66 Tempo de Permanência | idem | `parNum=4&…&par4=4` | totais (16 linhas) |
| 484 / 518 / 519 | idem | `parNum=3` (ordem unidade×datas varia!) | totais |
| 100 Internações Diárias | ParametroRelatorio (tipo, datas, clínica, sexo) | `parNum=9&par1&par2&par3=&par4=0005&par5=N&par6=N&par7=&par8=01=000000&par9=1` | Prontuário-Nome, D.Nasc, Idade, Sexo, Hora In, Município/Bairro, Local Internação |
| 667 Nominal por Classificação de Risco | `UPA/Relatorios/Relatorio.aspx?parrel=667&origem=5` | `rptviewXls.aspx?parNomeMaquina=&parRel=667&parNum=6&par1&par2&par3=&par4=0005&par5=PAR&par6=` | 629 linhas — Número do Boletim, Paciente, Idade, Data/Hora Entrada, Clínica, Origem |
| 763 Histórico Eventos da Fila | Relatorio.aspx | `parNum=4&par1&par2&par3=0005&par4=1` | 101 linhas (cabeçalho a identificar) |
| 818 Tempo Médio da Classificação | Relatorio.aspx | `parNum=2&par1=ini&par2=ini+7d` | **504** (Crystal pesado mesmo com 1 semana) |
| 791 Boletim Atendimento Médico PDF | ParametroRelatorio (competência + paciente + evento) | `RptViewCompetencia.aspx?parRel=489&parNum=2&par1=MM/AAAA&par2=<paciente>` | 504 com paciente vazio — é **por paciente** |
| **526** Atendimentos por Profissional | Relatorio.aspx `?parrel=526&origem=5` | `parNum=5&par1=0005&par2&par3&par4=5&par5=` | **937 linhas / 1,1 MB** — No Boletim, Código, Hora Atendimento, Nome Paciente, **Classificação**, Idade, Sexo, Clínica |
| **630** Pacientes em Observação | link direto | `rptviewXls.aspx?parRel=630&parNum=1&par1=unidDef` | 210 linhas — Nº Boletim, Nome, Início Atendimento, Tempo, Especialidade, Profissional, Espec. Obs., Profissional Obs., **Leito** |
| **21** Prontuários Abertos (Cadastro) | `Cadastro/Relatorios/ProntuariosAbertos.aspx` (só PDF na tela; XLS aceita os mesmos parâmetros) | `parNum=4&par1=ini&par2=fim&par3=0000&par4=0005` | 37 linhas — Prontuário, Paciente, Clínica, Nascimento, Idade, Sexo, **CNS** |
| **488** Origem Criação Prontuário (Cadastro) | idem | `parNum=5&par1=0005&par2=&par3=&par4=ini&par5=fim` | 7 linhas — Data Abertura, Código, Paciente, Prontuário, Setor de Origem |
| 65 / 711 / 66 / 56 / 57 | estatísticos | ver `sondagem-relatorios.md` | tabelas de totais (6–19 linhas): faixa etária × tipo de saída / classificação / permanência / clínica |
| 818 / 831 / 835 | Relatorio.aspx | — | **quebrados no vendor**: 504, `COMException` OLE DB, `FormulaException` do Crystal |

As telas do **Cadastro** usam `UCPesquisaData1$rdtpDataInicio/Fim` (RadDateTimePicker) e
validam no cliente com SweetAlert ("Datas inicial e Final Obrigatórias", "Selecione uma
clínica!", "Informe pelo menos, ou o intervalo de datas ou a faixa de Prontuário") — a sonda
precisa preencher o campo certo, senão o POST volta a própria tela sem `window.open`.

Os botões da tela de parâmetros: `ImageButton1` = **Voltar** (não é "visualizar"),
`btnImprimirExcel`, `btnImprimir`. Na `Relatorio.aspx` do módulo: `imbVoltar`,
`imbImprimirExcel`, `imbImprimir`, e os RadDatePickers ficam direto no conteúdo
(`…contentCenterChild$rdpDataInicial`), sem o `ctlParam$`.

Catálogo: `catalogo_relatorios.py` extraiu dos menus **268 relatórios** (84 via
`ParametroRelatorio`, 61 via `Relatorios.aspx`, 120 com tela própria, 3 `rptView` diretos) —
UPA 75, Internação 61, Ambulatório 53, Laboratório 25, Cadastro 15, Radiologia 14, Acesso 10,
Centro Cirúrgico 10, Administração 3, eProntuário 2. Lista completa em
`capturas/catalogo_menu.csv` (sem PII; pode virar doc).

### 3.2 WebServices `.asmx` — JSON com cookie, sem ViewState (confirmado)

`WebServices/ComboExamesService.asmx` e `WebServices/ComboGrupoExameService.asmx` são fontes
de RadComboBox (ASP.NET AJAX). `GET …/js` devolve o proxy com os **nomes dos métodos**
(`GetData(context)`, `DeletarVinculo()`) — é o mais perto de um "swagger" que existe aqui.
A página de ajuda (`.asmx` sem `/js`) e `?WSDL` respondem 500 (`RadComboBoxContext` não
serializa) — não é bloqueio, é bug do vendor.

Chamada direta que **funcionou** só com cookie:

```
POST /KlinikosNet/WebServices/ComboExamesService.asmx/GetData
Content-Type: application/json; X-Requested-With: XMLHttpRequest
{"context":{"Text":"","NumberOfItems":0,"FilterString":"","ClientState":""}}
→ 200 {"d":{"__type":…,"Items":[…],"NumberOfItems":…,"EndOfItems":…}}
```

São **lookups** (catálogo de exames/grupos), não dado clínico — mas provam o modelo: tudo
que for `.asmx`/`.ashx` é consumível como API.

### 3.3 Telas (`.aspx`) — só para o que não sai por relatório

Rastreadas por `probe_mapa.py` (GET puro, pula URL com verbo de escrita/Logout/GravaCookie).
Resultado da varredura completa (16/09, 23 min, uma sessão): **368 páginas** (260× 200, 6× 500,
2× 504, 1× 404), **3 serviços `.asmx`** (`ComboExamesService.GetData`,
`ComboGrupoExameService.GetData/DeletarVinculo`, `ComboProcedimentoService.GetProcedimento`),
16 scripts próprios (só webcam). **Nenhum `.ashx`, nenhum WebMethod `.aspx/Metodo`, nenhum
`$.ajax` para endpoint próprio** — a aplicação é postback puro; o que não é relatório é
ViewState. Tudo em `mapa-endpoints.md` (sem PII) e `capturas/mapa.json`.

Os 500 são bugs do vendor que um GET já dispara: `Radiologia/Relatorios/Radiologicos.aspx?parrel=751`
("Input string was not in a correct format"), `UPA/PesquisaSatisfacao.aspx` e
`Administracao/Tabelas/OrgRegional/*` (NullReference), `PostoEnfermagem/requisicaoexterna.aspx?Modulo=INTERNACAO`
(`ddlEnfermaria` SelectedValue inválido), `Share/CheckOut/CheckOut.aspx?origem=3` (SQL
"Incorrect syntax near 'e'").

Ler grade de tela exige o postback do RadGrid (paginação) e aí sim ViewState — **último
recurso**; preferir o relatório equivalente.

## 4. Mapa do módulo UPA (telas de dado clínico) — do menu, sem clicar

`UPA/Registro.aspx` (boletim; `?emergencia=True`), `UPA/ClassificacaoDeRisco.aspx`,
`UPA/AtendimentoMedico.aspx` (`?MultiProfissional=1`), `UPA/AtendimentoMedicoOdonto.aspx`,
`UPA/PrescricaoReceita.aspx`, `UPA/Enfermagem.aspx`, `Share/UPAShare/RegistrosEnfermagem.aspx`,
`Share/Leitos/ObservacaoLeito.aspx`, `Share/Leitos/MapaLeitos.aspx`,
`Share/Prescricao/PlanoTerapeutico.aspx`, `Share/CheckOut/CheckOut.aspx?origem=5|3` (baixa de
boletim; `origem=3` deu **500 SQL "Incorrect syntax near 'e'"**), `UPA/Remocao.aspx`,
`UPA/AdmFila.aspx`, `UPA/Retaguarda.aspx`, `UPA/RegulacaoDeLeitos.aspx`,
`UPA/AdmCompulsoria.aspx` (SINAN), `UPA/requisicaoexterna.aspx` (coleta de exames),
`UPA/PesquisaSatisfacao.aspx` (500 NullReference).

Relatórios do UPA que correspondem ao que o conector SQL lê hoje (candidatos a fonte):

| Dado no conector SQL | Relatório candidato (parrel) |
|---|---|
| Boletins do dia / chegada | 407 *Pacientes Registrados no Dia*; 56/57 *Registro Diário/Mensal por Clínica*; 691 *Registro Nominal por Ocorrência* |
| Em andamento / fila / observação | 631 *Atendimentos em Andamento*; 629 *Fila de Espera*; 630 *Pacientes em Observação* |
| Desfecho / saída | 65 *Tipo de Saída, Sexo e Faixa Etária*; 834 *Encerrados por evasão*; 520 *Remoções por destino*; 527 *Óbito/Chegou Cadáver* |
| CID | 815 *Atendimento Nominal por CID*; 484 *Diagnósticos por Clínica* |
| Classificação de risco | 667 *Nominal por Classificação de Risco*; 833 *Histórico de Classificação de Risco* |
| Evolução / multiprofissional | 830 *Evoluções de Multiprofissionais*; 791 *Eventos Clínicos* |
| Profissional | 526 *Atendimentos por Profissional*; 788 *Profissionais sem CNS* |
| Reentradas | *Reentradas.aspx* (tela própria) |

Falta medir: colunas e parâmetros de cada um (posição `parN`), custo no Crystal por intervalo
e **qual deles carrega o `spa_codigo`** (nº do boletim) — é a chave que amarra tudo no
conector.

## 5. Ferramentas deste laboratório

| Script | O que faz |
|---|---|
| `klinikos/client.py` | sessão httpx, login (+ confirmação de sessão única), gate do local, persistência de cookies, trava de leitura, `postback()` |
| `probe_login.py` | sonda 0: força login e retrata a home |
| `probe_tela.py <url>…` | descreve uma tela: form, campos, alvos, scripts |
| `probe_mapa.py [--max N] [--so-modulo X]` | rastreador GET de todas as telas → `capturas/mapa.json` + `capturas/mapa/` |
| `catalogo_relatorios.py` | catálogo dos 268 relatórios a partir dos menus → `capturas/catalogo_menu.csv` |
| `probe_relatorio.py <parrel> …` | replica a tela de parâmetros (postback de especialidade + botão) e mostra o `window.open` |
| `probe_rptview.py "<query>"` | chama `rptview`/`rptviewXls` direto e descreve PDF/XLS/HTML |
| `sondar_relatorios.py <parrel\|url>…` | lote: campos da tela + `parN` gerado + colunas/linhas do XLS → `capturas/sondagem_relatorios.json` |
| `gerar_sondagem_md.py` / `gerar_mapa_md.py` | `docs/sondagem-relatorios.md` e `docs/mapa-endpoints.md` a partir das capturas (sem PII) |
| `decodificar_viewstate.py` | strings/URLs de um `__VIEWSTATE` (LOS base64, sem cifra) |
| `analisar_har.py` | agrupa um HAR do DevTools por endpoint (para quando a extensão do Chrome voltar) |

## 6. Próximos passos

1. Terminar a varredura completa (`capturas/mapa.log`) e gerar `docs/mapa-endpoints.md` sem PII.
2. Para cada linha da tabela do §4: chamar `rptviewXls` com 1 dia, ler colunas com `xlrd`,
   anotar `parN` e se traz `spa_codigo`.
3. Medir custo: 1 dia × relatório × Crystal; definir janela segura (o 631 com 30 dias estoura).
4. Decidir a marca incremental (não há `rowversion` na web): provavelmente data do boletim +
   reconciliação diária dos últimos N dias.
5. Usuário dedicado do conector (sessão única derruba quem compartilhar).

## 7. UPA/Santa Rita são build 2025 — DIVERGEM do Conde (medido 16/09/2026)

O Conde é `K.2024.09.2.1` (app `/KlinikosNet`); UPA e Santa Rita são `U.2025.06.1.26` (app
`/UPA24H`). Não é só o caminho — o comportamento diverge em três camadas, e reusar o mapa do
Conde FALHA:

1. **Gate:** a build 2025 exige **porta → (AutoPostBack) → local físico → salvar** e grava o
   cookie **`_ID_VINCULO`** (+ `_LOCAL_NAME`). O "Acesso Administrativo" que basta no Conde
   (grava `_codigo_unidade`) passa o gate mas **não** deixa os relatórios rodarem na UPA.
2. **Parâmetros dos relatórios:** o `rptviewXls` com o `parN` do Conde devolve
   `SqlException: Procedure or function 'ksp_relatorio_Consolidado_...'` — o SP espera outros params.
3. **Identidade do relatório:** o **mesmo `parrel=667` é OUTRO relatório** na UPA — a tela
   (`/UPA24H/UPA/Relatorios/Relatorio.aspx?parrel=667&origem=5`) tem campos que o Conde não tem
   (**ddlOcorrencia, ddlClassificacaoRisco, ddlClinica, ddlProfissional, chkPPR, turno**), e
   Ocorrência/Classificação são **obrigatórios** ("Atenção!" se vazios). No Conde, 667 é o
   nominal simples por classificação.

Correção do item 2: o `parN` do Conde não dá "procedure not found" e sim **timeout de comando SQL**
(`... asyncWrite, Boolean inRetry, SqlDataReader`) — o SP da build 2025 recebe params fora do
formato. Com os params certos (abaixo) o SP roda.

### 7.1 A ESPINHA da build 2025 foi encontrada — tela 526 com seletor de modo `rblPadrao`

Recon de 16–17/09/2026 (ver [`espinha-upa-build2025.md`](./espinha-upa-build2025.md), documento
dedicado). A espinha do Conde (407+667+526) foi **consolidada** na build 2025 numa **única tela,
o relatório 526** (`/UPA24H/UPA/Relatorios/Relatorio.aspx?parrel=526&origem=5`), com um rádio de
modo `rblPadrao` (AutoPostBack) que gera **relatórios subjacentes diferentes**:

| modo (`par8`) | `parRel` de saída | espinha |
|---|---|---|
| 1 Nominal | **751** | boletim + chegada + cor + clínica + cadastro |
| 2 Por CID | **752** | CID por boletim (o "526" da build 2025) |
| 3 Por Status BAM | **802** | desfecho / tipo de saída por boletim |

URL da build 2025 (XLS BIFF; `par5`/`par7` vazios = TODAS especialidades/profissionais):
```
/UPA24H/Relatorios/rptviewXls.aspx?parRel=751&parNum=8&par1=<unid>&par2=<ini>&par3=<fim>&par4=5&par5=&par6=0023&par7=&par8=1
```
`par1`=unid (`0006` UPA, `0007` Santa Rita), `par4`=5 origem, `par6`=`0023` turno dia inteiro.

**Mecanismo (por que o POST na mão não gerava):** a tela roda em `Sys.WebForms.PageRequestManager`
e o botão Excel é **RadAjaxManager** (`AjaxNS.ARWO`); emular o POST só re-renderiza — o `Click` não
dispara e a app não monta o `window.open`. O "Atenção!" era **falso positivo** (regex batia na
definição de `SwAlerta2`, não numa chamada). O jeito à prova de versão: **navegador real com o
cookie injetado** dirige a tela e captura a URL que a app gera.

**Crystal vaza memória:** 629/630 (PDF) iam no início da sessão; após ~18 requisições **todo**
export virou `COMException (0x80041004): Not enough memory` (XLS e PDF). É saturação do servidor
Crystal deles — recicla só no app pool. **Caminho web tem de ser cirúrgico:** 3 req/dia/unidade
(751+752+802, período = o dia) + `backoff`. Por isso as **colunas exatas** de 751/752/802 ficaram
pendentes de confirmar quando o servidor recuperar.

**Santa Rita = paridade total:** mesma build, mesmo app, mesma tela 526 com os mesmos modos; só
mudam host (`starita24h`) e `par1=0007`.

**Consequência:** conector **version-aware** (perfil por build, auto-detectado pela string de
versão da home ou escolhido no cadastro). Credenciais: `bruno.rocha`/`l.mello`/`123` valem **só no
Conde**; na UPA/Santa Rita usa-se `leonardo.dantas` (loga de verdade nas duas).
