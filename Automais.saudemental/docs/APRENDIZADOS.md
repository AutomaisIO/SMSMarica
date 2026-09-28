# Prime Saúde Mental — recon "como usuário" (laboratório `Automais.saudemental/`)

Laboratório irmão de `Automais.prime/` e `Automais.klinikos/` (Eco Sistemas; mesmo método:
sondas `probe_*.py`, httpx com Referer, trava de somente-leitura, sessão persistida). Alvo:
`https://marica.ecosistemas.com.br/SaudeMental/`. Tudo abaixo foi **medido em 16/09/2026** com a
mesma conta do Prime (autorizado pelo operador; a usuária dona da conta foi avisada).

> **Somente leitura.** `saudemental/client.py` é cópia do cliente do Prime, com a mesma trava.
> Os relatórios são disparados por ImageButton (`imb<Nome>.x/.y`) e nenhum nome deles tem verbo
> de escrita.

## 0. Resposta curta

- **É outro produto**, não o Prime: "Prime Saúde Mental", build `2025.08.0.18` (o Prime é
  `2024.03.1.63`), marca própria mais **Prime TEA**. Reaproveita a mesma base de código (a home
  também fica em `AtencaoBasica/Default.aspx`), mas a aplicação é separada.
- **É o sistema dos CAPS de Maricá, e está em uso diário:**

| CAPS (gate) | 1º atendimento | 01/01/2018–31/08/2025 | 09–15/09/2026 |
|---|---|---:|---:|
| C.A Psicossocial Gilberto Silva dos Santos (CNES 7321716) | **27/05/2025** | 3.194 | 568 |
| CAPS AD Gláucia Pereira de Oliveira | **16/06/2025** | 1.450 | 166 |
| CAPS Infanto-Juvenil de Maricá | **01/07/2025** | 489 | 174 |

  O Gilberto soma 26.090 atendimentos de 27/05/2025 a 16/09/2026, entre 1,2 e 2,5 mil por mês. O
  Painel da Unidade também mostra as refeições servidas na semana (café, almoço, lanche e jantar).
- A tela de relatórios tem relatórios do **CEDTEA** (TEA), mas essa conta só enxerga os 3 CAPS.
  Se existe unidade TEA no sistema, ela está fora do escopo desta conta.
- Nenhum conector do SMSMais lê este sistema. É fonte nova.

## 1. Login e gate

| | Prime | Prime Saúde Mental |
|---|---|---|
| Form | `formLogin`, `LoginView1$lgAcesso$…` | igual |
| Cookie de auth | `.Eco.SL.AtencaoBasica_ASPXAUTH_niteroi` | `.Eco.SL.AtencaoBasica_ASPXAUTH` |
| Gate | `ddlPerfil` (oculto) + `ddlUnidade` (35) | **só `ddlUnidade`** (3 CAPS) |
| Home | `AtencaoBasica/Default.aspx` | igual |

- O login desta vez **não** pediu confirmação de outra estação (a conta estava livre).
- **Trocar de CAPS sem relogar:** um `GET login.aspx` com a sessão autenticada devolve o gate, e o
  POST de `ddlUnidade` troca a unidade. É o que `sondar_atividade.py` faz. Diferente do Prime, os
  relatórios daqui **não** recebem a unidade por parâmetro: usam a unidade da sessão
  (`hidUnidadeId`).

## 2. Relatórios: tela única + Telerik Reporting 7.2 (≠ Prime, ≠ Klinikos)

`Relatorios/RelatoriosSaudeMental.aspx` concentra **47 relatórios**. Cada um é um ImageButton com
os próprios filtros: `imbAtendimentosRealizados`, `imbAtendimentosCancelados`, `imbAgendamentos`,
`imbUsuariosCadastrados`, `imbObito`, `imbAbandono`, `imbInternacao`, `imbOcupacaoLeito`,
`imbPermanenciaLeito`, `imbRaas`, `imbProducaoProfissional`, `imbProducaoProcedimento`,
`imbProcedimentosPaciente`, `imbDiarioConvivencia`, `imbListaFrequencia`, `imbVisitas`,
`imbMatriciamentoAcaoInstitucional`, `imbUsuariosPorDroga`, `imbSolicitacaoExames`,
`imbBuscaAtiva`, `imbCoberturaEsf`, `imbRefeicao`, `imageButtonDiagnostico` (CID-10/CIAP-2),
`imgRelatorioCedteaAndamento`/`EncerradosTea` e outros. A tela capturada está em `capturas/mapa/`.

**Como o dado sai (medido):**

1. O **POST** da tela com `ctl00$DefaultContent$imb<Nome>.x/.y` e os filtros devolve a mesma tela
   (1,1 MB) com um **Telerik ReportViewer** (`Telerik.ReportViewer.axd`, versão `7.2.13.1016`)
   inicializado com um **`instanceID`** de 32 hex. O relatório fica em memória na sessão.
2. O **GET** de `/SaudeMental/Telerik.ReportViewer.axd?instanceID=<id>&optype=Export&ExportFormat=CSV`
   devolve o arquivo (`CSV` text/plain, `XLS` application/vnd.ms-excel, `PDF`).
   - `optype=Report` (página HTML do visualizador) respondeu **500** fora do navegador. Não serve
     como pré-visualização barata.

**Armadilha medida: data do RadDateInput.** Mandar só `rdp…Inicio` e `rdp…Inicio$dateInput` é
**ignorado sem erro**. O relatório sai com `Período:  a <hoje>`, isto é, o **histórico inteiro**:
30 MB, 31 s, 26.090 linhas num único CAPS. Um conector com esse erro martelaria o fornecedor. O
que funciona é mandar também o hidden **`ctl00_DefaultContent_rdp…_dateInput_ClientState`** com
JSON `{"validationText":"AAAA-MM-DD-00-00-00","valueAsString":"AAAA-MM-DD-00-00-00",…}`. Com isso,
1 semana = 650 KB em 0,1 s. **Sempre conferir o "Período" impresso no arquivo exportado.**

**CSV não serve: use XLS.** O export CSV do Telerik **omite a grade de detalhe**. Traz só título,
rodapé e totais por dia/tipo (`textBox3`, `txtPeriodo`, `txtDataInicioGroup`, `ppr_numero…`)
repetidos em cada linha: são 41 colunas e **nenhuma** tem paciente, hora ou profissional. O
**`ExportFormat=XLS`** (BIFF, `xlrd`, 2 s/semana) traz a grade inteira. `PDF` também traz, mas
custa 28 s por semana; evitar.

### 2.1 O que cada relatório entrega (medido no CAPS Gilberto, XLS)

**Atendimentos Realizados** (`imbAtendimentosRealizados`), 09–15/09/2026: **568 atendimentos,
211 prontuários, 35 profissionais.** Um cabeçalho de colunas por relatório, um bloco por dia com
totais por tipo.

| Coluna | Preenchida | Conteúdo |
|---|---:|---|
| Prontuário | 100% | 19 dígitos = `330270` (IBGE Maricá) + `003` (unidade) + `AAMMDD` (abertura) + sequencial 4. Aberturas desde 05/2025 |
| Situação Usuário | 94% | Atualizado 372 · Em Avaliação 114 · Pendente 43 · Cancelado 2 · Óbito 1 |
| Nome | 100% | nome |
| Idade | 100% | anos (18–79 no CAPS adulto, mediana 44) |
| Data de Atendimento / Hora | 100% | **o mesmo serial de data Excel com hora** nas duas colunas; picos 9h e 12h/16h |
| Profissional Atendimento | 100% | nome, sem CNS/CBO |
| Tipo Atendimento | 100% | Individual 273 · Convivência 241 · Inicial ao Paciente 24 · Familiar 22 · Atenção à Crise 7 · Noturno 1 |
| Situação Atendimento | 100% | Encerrado 547 · Em Atendimento 20 · Cancelado 1 |
| Diagnóstico | 88% | `CID - descrição`, **só capítulo F** (top F99, F20, F200, F29, F31). É o diagnóstico do usuário, estável na semana (só 1 paciente com 2 diferentes) |

**Usuários Cadastrados** (`imbUsuariosCadastrados`, `rdpInicioCadastrados/Fim`), 01–15/09: 37
cadastros. Colunas: Cadastro (data), Usuário (nome), Tel. Usuário, Tel. Resp., Sexo, Idade,
**Raça/Cor**, **Gênero** (62% preenchido), **Moradia** (Própria/Alugada/**Situação de
Rua**/Cedida), **Origem do Usuário** (Demanda Espontânea, Urgência/Emergência, UBS, CAPS,
Outros), **Situação do PTS** (Em Avaliação/Concluído), Prontuário (**Ativo/Arquivado**, não o
número), Bairro.

**RAAS** (`imbRaas`, `rdpDataInicioRaas/Fim`), 01–15/09: **~1.000 registros**, colunas
Profissional (`código - nome`), Data, Usuário, Procedimento (categoria RAAS: Atendimento
Individual/Grupo/Familiar, Acolhimento Diurno/Noturno, Situação à Crise, Práticas
Expressivas/Corporais, Reabilitação Psicossocial, Visita Domiciliar, Contratualidade no Território,
Moradores em SRT, Terceiro Turno) + totais por categoria. **Sem código SIGTAP e sem CNS**, apesar
do nome.

### 2.2 Identificação do paciente: CPF e CNS saem pela "Documentação dos Usuários"

Os relatórios **operacionais** (Atendimentos, Cadastrados, RAAS) **não** trazem documento. Isso
levou a uma conclusão errada na primeira análise ("não há CPF/CNS"), corrigida na mesma sessão. O
cadastro guarda os documentos:

- **Tela de cadastro** `Paciente/PacienteEdit.aspx`: `rblCPF`/`rblCNS` (tem/não tem),
  `RadTextBoxCNS` + `RadGridCNS` (**mais de um CNS por paciente**) e o questionário "Documentos"
  (`UCQuestionarioContainerDocumentos`): CPF (`Q33P12`), RG com complemento/UF/emissão/órgão,
  certidão (formato antigo e novo), CTPS, título de eleitor. Também `txtCpfResponsavel` e
  `txtCpfAcompanhante`.
- **Grade de pesquisa de usuários** (`RegistroConvivenciaList`): colunas Código Mitra, Nome, Nome
  da Mãe, Sexo, Nascimento, Prontuário, **CPF, NIS, DNV, CNS, Certidão**, Situação. Só preenche
  depois de uma busca.
- **Relatório `imbDocumentacaoUsuarios`** (filtro "Todos", sem data), **escopo = CAPS da sessão**.
  Colunas: **Usuário, Nascimento, CPF, CNS**, CTPS/Série/UF/Emissão, Certidão/Livro/Folha,
  RG/UF/Emissão/Órgão, Título de Eleitor. O XLS sai em 4–11 s.

| CAPS | Usuários | CPF | CNS | CPF ou CNS | Nenhum |
|---|---:|---:|---:|---:|---:|
| Gilberto (adulto) | 2.075 | 1.792 (86%) | 1.349 (65%) | 1.993 | 82 |
| Infanto-Juvenil | 1.131 | 601 (53%) | 1.130 (99%) | 1.131 | 0 |
| AD | 984 | 768 (78%) | 655 (66%) | 900 | 84 |

**Todos os CPF e CNS preenchidos passam no dígito verificador.** O CPF vem com 11 dígitos, sem
máscara; o CNS começa com 7 ou 8 (provisório), com alguns 1 ou 2 (definitivo). O IJ tem CPF baixo e
CNS quase total, o que é esperado para crianças. Só 11 nomes do Gilberto aparecem também no IJ, o
que confirma que o cadastro é por unidade.

**Ligação atendimento → documento:** o relatório de documentos **não traz o prontuário**. A chave é
**nome normalizado** (+ nascimento/idade para desempate). Medido na semana 09–15/09 do Gilberto: 211
pacientes atendidos, **210 casam com exatamente 1 usuário, 0 homônimos, 1 não achado; 202 saem com
CPF ou CNS**. Para conector, conferir idade × nascimento e mandar homônimo para revisão, nunca
casar no chute (regra de dedup do hub).

Ainda não testado: busca JSON `ComboPacienteService.GetDataByFonema_MultiUnidade` (exige
`IdUnidade` no contexto; a chamada foi barrada pela política de permissões desta sessão). Com o
relatório de documentos, deixou de ser necessária.

## 3. Serviços `.asmx` (24)

Mesma família do Prime, com variações "multiunidade":

| Serviço | Métodos |
|---|---|
| `ComboPacienteService` | `GetDataByFonema_MultiUnidade`, `GetDataByOrigemPaciente` |
| `ConsultaPacienteService` | `GetDiference` |
| `ComboProfissionalPorUnidadeService` | 10 (inclui `GetProfissionalGeracaoAgenda_MultiUnidade`, `GetDataPorProcedimentoCBO`) |
| `ComboBeneficioService`, `ComboRaca`, `ComboOrgaoEmissor`, `ComboPaisService`, `ComboUFService` | cadastro |
| `UCComboCidService`, `UCComboCIAP2Service`, `UCComboCapituloCid10Service`, `UCComboCboService` | catálogos |

Chamada JSON direta ainda não testada.

## 4. Varredura (`probe_mapa.py`)

41 páginas em 35 s: 33 respostas 200, 6 respostas 404 (links quebrados do menu) e 2 respostas 500
(`MapaLeitosRegistroSaida?Id=` e `RelatorioImpressaoSOAP?qId=`, sem id). Scripts próprios: só
webcam. Módulos no menu: Recepção/Cadastro, Acolhimento/Acompanhamento, **Projeto Terapêutico
Singular**, Convivência e Frequência, Atividade em Grupo, Visita, **Mapa de Leitos**, Ações
Institucionais, Matriciamento, Notificação Compulsória, **Serviço Residencial Terapêutico** (SRT),
Faturamento RAAS/BPA. Resumo sem PII em `mapa-endpoints.md`.

> A varredura abriu (GET) `Srt/MoradorEdit.aspx?id=…`. GET de WebForms só renderiza o form, nada
> foi gravado. Mesmo assim, `edit` entrou na lista de caminhos que `probe_mapa.py` pula.

## 5. Ferramentas

| Script | O que faz |
|---|---|
| `saudemental/client.py` | sessão, login, gate só de unidade, cookies persistidos, trava de leitura |
| `probe_login.py` | força login e retrata o gate |
| `probe_tela.py <url>…` | descreve uma tela |
| `probe_mapa.py` / `gerar_mapa_md.py` | varredura GET → `capturas/mapa.json` → `docs/mapa-endpoints.md` |
| `probe_relatorio.py <imb> <rdpIni> <rdpFim> DD/MM/AAAA DD/MM/AAAA [CSV\|XLS\|PDF]` | clica um relatório (datas com o `_dateInput_ClientState`), pega o `instanceID` e exporta. **Use XLS**; para relatório sem data, passe `"" ""` nos campos |
| `sondar_desfechos.py DD/MM/AAAA DD/MM/AAAA [guid]` | perfil sem PII dos relatórios de desfecho (Destinos, Abandono, Óbito, Internação, Permanência, Cancelados, Exames, Procedimentos) |
| `sondar_atividade.py DD/MM/AAAA DD/MM/AAAA` | *Atendimentos Realizados* por CAPS (troca de unidade pelo gate): contagem + 1ª/última data. Usa CSV, que basta para **contar** (1 linha por atendimento), mas não para ler o detalhe |

## 6. Integração com o SUS

Não há. O cadastro do Saúde Mental só valida o dígito de CPF e CNS no navegador. O "Consultar
Cadweb" existe só no **Prime** (`Automais.prime/docs/APRENDIZADOS.md` §4b) e foi **descartado**
pelo operador, porque o SMSMais já tem caminho próprio para o CADSUS.

## 7. Cuidados

- **Mesma conta do Prime** (`11777653738`), com OK do operador e aviso à dona da conta. Sessão
  única: conector de verdade = usuário dedicado.
- **Datas sem `_dateInput_ClientState` = histórico inteiro** (30 MB por CAPS). Nunca rodar
  relatório com período sem conferir o "Período" do arquivo.
- **PDF custa ~28 s por semana**; XLS ~2 s. Uma requisição por vez, e troca de CAPS pelo gate.
- **`capturas/` tem PII sensível** (saúde mental: CID F, situação de rua, documentos). É
  gitignored; relatório para humano só com contagens e formas.
- **Busca de paciente por nome** (`ComboPacienteService`) foi barrada pela política de permissões;
  não contornar. `edit` está na lista de caminhos que a varredura pula.

## 8. Estado em 16/09/2026 e próximos passos

**Fechado:** produto (Prime Saúde Mental, 3 CAPS, uso desde mai–jul/2025) · login/gate/troca de
CAPS · relatórios via Telerik (POST → `instanceID` → export XLS) · armadilha das datas · colunas de
Atendimentos, Cadastrados e RAAS · **CPF/CNS pela Documentação dos Usuários** (~96%, DV válido) e
ligação por nome (210/211) · 24 `.asmx` mapeados · integração SUS (não há). Nada commitado, nada em
produção mudou.

**Aberto:**
1. Sondar óbito, abandono, internação, ocupação de leito e o relatório de diagnóstico CID/CIAP.
2. Perguntar ao operador se o CEDTEA/TEA roda neste sistema com outra conta.
3. Decidir se os CAPS entram no hub FHIR (âncora CPF/CNS via Documentação + nome). Isso exige ADR,
   OK e cuidado LGPD redobrado (dado de saúde mental).

## 9. Modelo clínico × urgência e emergência (UPA, Conde, Santa Rita no Klinikos)

Medido em 16/09/2026 pelos relatórios; as telas de atendimento clínico (PEP/SOAP) **não** foram
abertas, porque abrem a partir da agenda com o id de um atendimento real.

| Conceito | Urgência (Klinikos) | Prime (especializada) | Prime Saúde Mental (CAPS) |
|---|---|---|---|
| Unidade do registro | **Boletim** (`spa_codigo`): um episódio por chegada | **Atendimento** agendado ou espontâneo (sem boletim) | **Atendimento** dentro de um acompanhamento longitudinal (prontuário + PTS) |
| Chegada/fila | chegada, classificação de risco (cor), fila, tempos | agenda (programada 9%, espontânea 91% na amostra), `DataRegistro` com hora | agenda, acolhimento, convivência |
| Duração | chegada → atendimento → saída | `DataInicio`/`DataFim` (só data) + `Duracao` | data e hora do atendimento |
| Diagnóstico | CID primário/secundário por evolução | CID por atendimento (66%), lista | CID do **usuário** (capítulo F, 88%), estável; relatório CID/CIAP |
| Procedimento | (produção) | **SIGTAP + quantidade** por atendimento | categoria RAAS (sem código SIGTAP no relatório) |
| Prescrição | estruturada (item, via, frequência, duração) | só texto livre e raro (`MedicamentosPrescritos` 0,3%) + programa **Remédio em Casa** (entregas/receitas) | **não há** tela nem relatório de prescrição |
| Exames | coleta/requisição | texto livre (`ExamesSolicitados` 6,7%) | relatório *Solicitação de Exames* (usuário, data, exame, profissional) |
| Sinais vitais | registros de enfermagem | não encontrado | não encontrado |
| Narrativa | anamnese, exame físico, conduta (por boletim) | módulo PEP (`Pep/…`), sem relatório nominal | **SOAP** (`RelatorioImpressaoSOAP.aspx?qId=`), só impressão por atendimento |
| "Alta"/desfecho | **tipo de saída do boletim** (alta, óbito, evasão, remoção, internação) | **não existe** alta: o atendimento só termina (`DataFim`) | **desfecho do acompanhamento**: *Destinos* (Alta, Alta a Pedido, Alta Após Insucesso da Busca Ativa, Alta por Falta, Continuidade Atenção Básica, Continuidade Outro CAPS, Óbito), *Motivo da Saída*, *Óbito* (data/hora/fonte), situação do usuário, prontuário Ativo/Arquivado |
| Internação/leito | observação, leito, internação | não | acolhimento em leito (CAPS III): *Internação* (tipo, local, início/fim, motivo), *Permanência em Leito* |
| Identidade | cadastro com CPF/CNS | **CNS** no próprio relatório (sem CPF) | CPF/CNS só no relatório *Documentação dos Usuários*; liga por nome |

**Leitura para o hub FHIR:** urgência = `Encounter` por boletim (class EMER) com `hospitalization.dischargeDisposition`.
Especializada = `Encounter` ambulatorial (AMB) por atendimento + `Procedure` SIGTAP + `Condition`, sem
desfecho de alta. CAPS = dois níveis: `EpisodeOfCare` (acompanhamento: início pelo acolhimento, fim pelo
*Destino*/alta) contendo `Encounter`s (atendimentos, tipo RAAS) + `Condition` do usuário. Prescrição
estruturada só existe na urgência.

**Cuidado de sessão (medido nesta análise):** Prime e Saúde Mental **compartilham a sessão única**
da conta. Logar num derruba a sessão do outro (e de quem estiver usando a conta). Duas sondas
seguidas, uma em cada sistema, derrubaram-se mutuamente. Nunca alternar os dois labs na mesma conta;
conector = usuário dedicado por sistema.
