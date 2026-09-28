# Prime Saúde — recon "como usuário" (laboratório `Automais.prime/`)

Mesmo **método** do laboratório `Automais.klinikos/` (mesmo fornecedor, Eco Sistemas): sondas
`probe_*.py`, cliente httpx com Referer automático, trava de somente-leitura, sessão persistida.
Alvo: `https://marica.ecosistemas.com.br/Prime/`. Tudo abaixo foi **medido em 16/09/2026**.

> **Somente leitura.** A trava `guardar()` (`prime/client.py`) recusa nome de parâmetro de POST
> e `__EVENTTARGET` com verbo de escrita. Liberados pontualmente: `LoginButton` e
> `btnConfirmarLogin`. Hidden `*_ClientState` **vazio** do Telerik não é ação e passa.

## 0. Resposta curta: o Prime em uso é a atenção ESPECIALIZADA

O Prime é o "Prime Saúde", e a raiz pós-login se chama `AtencaoBasica/` (cookie
`.Eco.SL.AtencaoBasica_ASPXAUTH_niteroi`). Esse nome é da **linha de produto do fornecedor**, não
do uso em Maricá. O relatório *Pacientes Atendidos* por unidade diz quem usa de verdade:

| Unidade | 09–15/09/2026 (atendimentos / pacientes por CNS) | ago/2026 | 2025 | 2023–2024 |
|---|---:|---:|---:|---:|
| CDT Dr. Alberto Luis Machado Borges | 1.210 / 715 | | | |
| Centro de Reabilitação Ambulatorial e Domiciliar | 1.039 / 546 | | | |
| Centro Materno Infantil | 741 / 410 | | | |
| Ambulatório Péricles Siqueira Ferreira | 516 / 369 | | | |
| SAE (Serviço de Atendimento Especializado) | 381 / 118 | | | |
| CEO Itaipuaçu | 317 / 257 | | | |
| Melhor em Casa | 315 / 91 | | | |
| CEO Boqueirão | 127 / 120 | 559 | 4.107 | **0** |
| Odontomóvel | 12 / 11 | | | |
| CEREST | 0 | | | |
| **25 USFs** (todas) | **0** | | | |
| **Total** | **4.658 / ~2.600** | | | |

> **Correção (16/09, mesma sessão):** a primeira versão desta tabela contava **linhas físicas**
> do CSV e deu ~14,8 mil na semana. O CSV tem quebras de linha dentro do campo de procedimentos
> (ver §3.1), e o número certo é **4.658 registros**. As conclusões (USFs zeradas, especializada
> desde 2025) não mudam.

- **As 25 USFs estão cadastradas, mas não têm atendimento no Prime.** A USF Central e a USF
  Bambuí têm zero atendimentos de 2018 a 08/2026, em janelas anuais. Janelas longas funcionam: o
  controle com o CEO Boqueirão devolve 4.107 atendimentos em 2025. As USFs atendem em outro
  sistema.
- **A especializada começou a usar o Prime em 2025.** O CEO Boqueirão tem zero em 2023 e 2024.
- O Prime não é lido hoje por nenhum conector do SMSMais. É **fonte nova**, não substituição.

Sonda: `sondar_atividade.py DD/MM/AAAA DD/MM/AAAA [guid…]` (só contagens; CSVs em `capturas/`).

## 1. Stack

| | |
|---|---|
| Servidor | nginx/1.18.0 (Ubuntu), `X-AspNet-Version: 4.0.30319`, **hospedado pela Eco** (não em `*.smsmarica.online`) |
| Camada web | ASP.NET WebForms, Telerik (RadComboBox, RadDatePicker, RadWindow, RadSplitter), jQuery 3.7, select2, SweetAlert2 |
| Build | `2024.03.1.63` (`lblVersao` do login) |
| Raiz | `/Prime/` (home pós-login `/Prime/AtencaoBasica/Default.aspx`) |
| Irmão | `/SaudeMental/` = **Prime Saúde Mental**, outro produto (build `2025.08.0.18`) → `Automais.saudemental/` |

## 2. Login e gate de unidade

1. `GET /Prime/login.aspx` → form **`formLogin`** (Klinikos: `form1`) com os mesmos campos
   `LoginView1$lgAcesso$UserName/Password/LoginButton=ENTRAR`.
2. **Sessão única por usuário**, igual ao Klinikos: se o usuário está logado em outra estação,
   volta `btnConfirmarLogin`. Confirmar **derruba a outra sessão**. Isso aconteceu na primeira
   sonda, porque a conta estava aberta num navegador. Um conector precisa de usuário próprio.
3. Login OK → a própria `Login.aspx` com **gate**: `LoginView1$ddlPerfil` (CBO; com um perfil só,
   `pnlPerfil` vem `display:none`) e `LoginView1$ddlUnidade` (GUID de 35 unidades), ambos
   AutoPostBack.
   - **Armadilha medida:** mandar `ddlPerfil=-1` ou fazer postback do perfil faz a tela voltar
     igual, **sem erro**.
   - O que passa é **um** POST com `__EVENTTARGET=LoginView1$ddlUnidade` e **sem** o campo
     `ddlPerfil` no corpo → 302 para `AtencaoBasica/Default.aspx`.
4. Cookies: `ASP.NET_SessionId` + `.Eco.SL.AtencaoBasica_ASPXAUTH_niteroi` (forms auth). A
   unidade escolhida fica na sessão e aparece nas telas como `hidUnidadeId`.

## 3. Relatórios: GET direto, sem ViewState, com `unidades=<GUID>`

O botão "Gerar relatório" (PDF/XLS/CSV) **não devolve o arquivo**. A tela responde com um
`btnGerarRelatorio…_ClientClick(...)` que faz `window.open` para uma página `*RPT.aspx` com os
parâmetros na query. Exemplo medido:

```
GET /Prime/Relatorios/RelatorioPacientesAtendidosRPT.aspx
    ?dataInicio=09/09/2026&dataFim=15/09/2026&funcao=&prof=&idGrupoPrioritario=
    &extensao=CSV&unidades=<GUID da unidade>
→ 200 text/csv; charset=utf-8  (0,1 s)
```

- **`unidades` aceita o GUID de qualquer unidade da lista do gate.** Não é preciso trocar a
  unidade da sessão para ler outra.
- `extensao=CSV` devolve texto separado por `;` com cabeçalho. É melhor que o XLS BIFF do Klinikos.
- Colunas e formato do *Pacientes Atendidos*: §3.1 (22 campos; **cuidado com as quebras de linha
  dentro do registro**).
- Um ano inteiro de uma unidade (4.107 atendimentos, CEO Boqueirão 2025) volta sem 504. O custo
  é bem menor que o do Crystal no Klinikos. Ainda assim, uma requisição por vez.
- Chamados **sem** parâmetro, os `*RPT.aspx` dão 500 ("Input string was not in a correct
  format"). É a varredura que chama assim, não é bloqueio. Os 21 RPT vistos estão em
  `mapa-endpoints.md` e todos seguem o mesmo padrão: *Atendimento em Aberto, Diagnóstico,
  Famílias, Indivíduos por Equipe, Pacientes Faltantes, Imunização por Ciclo de Vida, Testes
  Rápidos, Saúde Bucal, Coletas, Consolidado Território, Remédio em Casa…*

### 3.1 O que vem no CSV de *Pacientes Atendidos* (perfil de 4.658 registros, 09–15/09/2026)

**Formato (armadilha):** separador `;`, **sem aspas**, cada registro termina com `;` + **CRLF**,
mas os campos `Procedimentos`/`Cod_Procedimentos`/`ExamesSolicitados` têm **LF solto** dentro. Um
leitor CSV comum desalinha tudo (sexo vira "Espontanea", CNS vira data). Ler assim: separar
registros por CRLF (`\r\n`), descartar o campo vazio final e esperar **22 campos**.

Um registro = **um atendimento** (paciente × profissional × data).

| Coluna | Preenchida | Conteúdo medido |
|---|---:|---|
| `pac_nome` | 100% | nome |
| `pac_usarNomeSocialAtendimentos` / `pac_nomeSocial` | 100% / 0,1% | sempre `False`; nome social em 4 registros |
| `pac_sexo` | 100% | F 2.764 · M 1.885 · I 9 |
| `Data_Nasc` / `idade` | 100% | `dd/mm/aaaa 00:00:00` / anos |
| `CNS` | **97,5%** | 15 dígitos; 1º dígito 7 (4.311) ou 8 (231); **~2.600 pacientes distintos**. **Não há CPF.** |
| `FuncaoAtendimento` | 100% | Procedimento 1.421 · Fisioterapeuta 475 · Saúde Bucal 468 · Enfermagem 381 · Fono 219 · Escuta Inicial 159 · Psicólogo 145 · Cardiologista 134 · Médica 124 · Neuro 102 · GO 91 · Oftalmo 83 · Otorrino 81 · Endócrino 80… |
| `prof_nome` | 100% | nome do profissional, **sem CNS/CBO/conselho** |
| `Diagnosticos` | 66% | lista `CID - descrição` separada por vírgula; 686 CIDs distintos (top Z01, I10, F840, K02, M545, I64) |
| `Procedimentos` | 100% | `<procedimento>, NOME</procedimento>…` (pseudo-XML) |
| `Cod_Procedimentos` | 100% | `<procedimento>  <SIGTAP 10 díg> - NOME - Quantidade: N</procedimento>`; **168 SIGTAP distintos**, 8.549 ocorrências (top 0301010048, 0301010072, 0301100039) |
| `TeveExamesSolicitados` / `ExamesSolicitados` | 70% / 6,7% | `S/N` (vazio quando a função é "Procedimento"); exames em **texto livre**, um por linha, sem código |
| `TeveMedicamentosPrescritos` / `MedicamentosPrescritos` | 70% / 0,3% | `S/N`; texto livre (`princípio ativo dose`), 12 registros |
| `Demanda` | 100% | Espontanea 4.222 · Programada 436 |
| `DataRegistro` / `DataInicio` / `DataFim` | 100% | `dd/mm/aaaa hh:mm:ss` (registro com hora; início/fim só a data) |
| `Duracao` | 100% | `1h 18min 11s` |
| `EnderecoTelefone` | 100% | texto único `LOGRADOURO, Nº, BAIRRO, MUNICÍPIO-UF, CEP: 99999999 - Tel: 99999999999` |

**Leitura para o hub:** dá para montar Encounter (data, função/especialidade, demanda, duração),
Condition (CID), Procedure (SIGTAP + quantidade) e Patient (nome, sexo, nascimento, **CNS**,
endereço, telefone). **A âncora é o CNS**, porque não há CPF. Practitioner só por nome. Exames e
medicamentos são texto livre e raros.

## 4. Serviços `.asmx`: JSON com cookie (31 serviços)

Ao contrário do Klinikos (3 `.asmx`), o Prime expõe **31** serviços em `/Prime/Services/`. O
proxy `/js` lista os métodos. Os que mais interessam:

| Serviço | Métodos |
|---|---|
| `ComboPacienteService` | `GetData`, `GetDataByFonema`, `GetDataByFonemaNaUnidade`, `GetDataSQL` |
| `ComboProfissionalPorUnidadeService` | 12 métodos (lotação ativa, CBO médico/enfermeiro, EMAD/EMAP, agenda…) |
| `ComboProfissionalService` | `GetData`, `GetDataSQL` |
| `UCComboEquipeService` | `GetAllEquipes`, `GetData_ESF_EAP`, `GetEquipesAtencaoDomiciliar`… |
| `ComboUnidadeService` | `GetUnidades` |
| `UCComboCidService` / `UCComboCIAP2Service` / `UCComboCboService` | catálogos |
| `ComboExamesService` | 7 métodos de catálogo de exames |

A chamada JSON direta (padrão RadComboBox `{"context":{…}}`) **ainda não foi feita**; é o próximo
passo. `GetDataSQL` merece cuidado: o nome sugere SQL montado no servidor, então só leitura e só
com o contexto que a tela manda.

## 4b. Integração com o SUS: "Consultar Cadweb" no cadastro do paciente (medido, DESCARTADO)

O Prime diz ter integração com o SUS. Medido em 16/09/2026, lendo só o HTML das telas (GET, nada
clicado):

- **e-SUS / SIAPS** (`Faturamento/ExportacaoEsus3.aspx`, "Geração de Arquivos e-SUS / Siaps"):
  gera os arquivos das fichas (Atendimento Individual, Domiciliar…) **para enviar** ao e-SUS. É
  saída de produção, **não consulta paciente**.
- **Consulta de paciente no CADSUS**: fica em **`Paciente/CadastroPaciente.aspx`** (controle
  `UCNovoCadastroPaciente1`), no botão **`btnConsultarCadweb`** ("Consultar Cadweb", RadButton com
  postback). Ele usa o CPF (`txtCPF`) e o CNS (`RadTextBoxCNS`) digitados e abre a janela
  **`rwinConsultaCadweb`**, um **comparador Prime × Cadweb** com duas colunas de resultado, *por CPF* e
  *por CNS*, para os campos **Nome, Nome da Mãe, Nome do Pai, Data de Nascimento, CPF, CNS e Sexo**
  (`radio<Campo>Prime` / `radio<Campo>Cadweb_porCPF` / `_porCNS`). Sem resultado, mostra
  `txtInfoConsultaCadweb` = "Não foi encontrado paciente no CadWeb a partir da busca por…".
- **O que grava:** a consulta em si só preenche a janela. Gravar é outro botão
  (`btnAtualizarCadastroPacienteCadweb` → copia os campos escolhidos para o form), seguido de salvar
  o paciente. **Nenhum dos dois pode ser acionado pelo laboratório.**
- O Saúde Mental **não** tem esse botão: no cadastro dele, CPF e CNS só passam por validação de
  dígito no navegador.

**Descartado (decisão do operador, 16/09/2026):** o SMSMais já consulta o CADSUS pelos próprios
caminhos (SISREG `cadweb50` / proxy de CPF), então o "Consultar Cadweb" do Prime **não será
testado nem usado**. Fica registrado só para ninguém redescobrir.

## 5. Varredura (`probe_mapa.py`, GET puro)

123 páginas em 95 s: 102 respostas 200 e 21 respostas 500 (os RPT sem parâmetro). Nenhum
`.ashx`, nenhum WebMethod, nenhum `$.ajax` próprio. Pula: logout, senha, carga, sincronização,
importação, exportação, migração, fechamento e unificação. Resumo sem PII em
`docs/mapa-endpoints.md`.

Fora da varredura, abertas à mão por GET (só renderizam o form, nada clicado):
`Faturamento/ExportacaoEsus3.aspx` e `Paciente/CadastroPaciente.aspx` (§4b). O cadastro de
paciente **não** é `Paciente/Paciente.aspx` (404): os links relativos do menu apontam para
`CadastroPaciente.aspx` e `AtencaoBasica/ChamadaPaciente.aspx`.

## 6. Ferramentas

| Script | O que faz |
|---|---|
| `prime/client.py` | sessão httpx, login (+ confirmação de sessão única), gate de unidade, cookies persistidos, trava de leitura |
| `probe_login.py` | força login e retrata a página pós-login (gate) |
| `probe_tela.py <url>…` | descreve uma tela: form, campos, alvos, scripts |
| `probe_mapa.py [--max N]` | rastreador GET → `capturas/mapa.json` |
| `gerar_mapa_md.py` | `docs/mapa-endpoints.md` sem PII |
| `probe_atendidos.py` | primeira tentativa por postback; mostrou o `window.open` do RPT |
| `sondar_origem_sisreg.py <guid> ini fim` | agenda da recepção + *Pacientes Agendados* (CSV): distribuições de origem/tipo de demanda, URLs de ação |
| `sondar_codigo_sisreg.py <csv_agendados> <unidade_id_hub> [max]` | cruza agendados × `smsmarica.solicitacao` (CNS+unidade+data) e procura o código/chave SISREG exatos no Prime |
| `classificar_origem.py <csv_agendados> <unidade_id_hub> DD/MM/AAAA(hoje)` | classifica cada agendamento pela origem SISREG (regras §10.1) só com o banco do hub |
| `sondar_custos.py <guid> DD/MM/AAAA [max_logs]` | custo e cobertura do pipeline chegada/início/atendido/falta (§12) |
| `sondar_atividade.py DD/MM/AAAA DD/MM/AAAA [guid…]` | atendimentos do *Pacientes Atendidos* por unidade × janela (só contagens). Conta **registros por CRLF**; a versão inicial contava linhas e inflava ~3×, corrigido em 16/09 |

## 7. Cuidados (valem para qualquer próxima sessão)

- **Sessão única.** A primeira sonda derrubou a sessão aberta da conta `11777653738` (usuária da
  SMS). O operador avisou a dona da conta para não usá-la durante o laboratório. Conector de
  verdade = **usuário dedicado**.
- **Uma requisição por vez** e `sleep` entre unidades. O servidor é da Eco, não nosso.
- **`capturas/` tem PII** (nome, CNS, endereço, telefone) e é gitignored. Relatório para humano sai
  com contagens e formas, nunca valores.
- **Busca de paciente por nome** (`ComboPacienteService.*`) foi barrada pela política de
  permissões na sessão de 16/09. Não contornar; se precisar, pedir OK explícito.
- **Cadweb** do Prime: descartado (§4b). Não acionar.

## 8. Estado em 16/09/2026 e próximos passos

**Fechado:** quem usa o Prime (especializada, desde 2025; USFs zeradas) · login/gate · relatório
por GET com `unidades=<GUID>` · formato e colunas do *Pacientes Atendidos* · 31 `.asmx` mapeados ·
integração SUS (e-SUS só exporta; Cadweb descartado). Nada commitado, nada em produção mudou.

**Aberto:**
1. Sondar os outros RPT úteis para a especializada: *Atendimento em Aberto*, *Diagnósticos*,
   *Pacientes Agendados/Faltantes*, *Encaminhamentos Especializados*.
2. Chamar por JSON os serviços **que não são busca de paciente** (profissional por unidade,
   equipes, unidades) para saber se trazem CNS/CBO do profissional.
3. Decidir se o Prime entra no hub FHIR como PEP da especializada (âncora CNS). Isso exige ADR e
   é escrita em produção, então só com OK.

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

## 10. Origem SISREG: o Prime não sabe, o hub sabe (cruzamento medido 16/09/2026)

**O "Espontânea" do Prime não é a origem real.** No *Pacientes Atendidos*, 91% vem como
`Demanda=Espontanea`. Na agenda da recepção, "Tipo da Demanda"=DE, "Tipo de Agendamento"="Outro",
"Procedência"="Encaminhamento Interno (Sem necessidade de retorno)". No *Pacientes Agendados*,
`OrigemAgenda`="Unidade" (100%). O Prime não enxerga o SISREG.

**Cruzamento com o hub** (`sondar_codigo_sisreg.py` + `fhir.patient` × `smsmarica.solicitacao`, só
SELECT). Amostra de 100 agendamentos do **CDT**, 09–15/09/2026, casados por **CNS + unidade
executante (CDT, CNES 3132358) + data**:

| Resultado | Agendamentos |
|---|---:|
| Solicitação SISREG no CDT, **mesma data** | **86** |
| Solicitação SISREG no CDT, até 3 dias de diferença | 4 |
| Solicitação SISREG no CDT, outra data | 8 |
| SISREG só em outra unidade | 1 |
| CNS fora do hub | 1 |

Correspondência coerente entre a "Função de Atendimento" do Prime e o procedimento SISREG:
Cardiologista ↔ OCI Avaliação Cardiológica (13), Procedimento ↔ Mamografia (8) / Holter (8) /
MAPA (4), Otorrino ↔ Consulta em Otorrino (8), Ortopedista ↔ OCI Ortopedia (7), Fono ↔
Audiometria (4). "Procedimento" e "Consulta Médica" do Prime escondem **exames** regulados.

**O código do SISREG não está no Prime** (teste objetivo, por string exata):
- os `codigo_solicitacao`/`chave_confirmacao` dos 88 pares casados **não aparecem** em nenhum CSV
  baixado do Prime (Atendidos, Agendados);
- em 8 telas `Agendamento/DetalhesLogAgenda.aspx?AgendaId=`, também não. O log só tem eventos do
  sistema (Agendar, Acolhimento realizado, Atendimento iniciado, Desagendar) com descrição
  automática;
- a grade da agenda, o relatório de agendados e o cadastro do paciente **não têm campo** de
  chave, código ou solicitação SISREG.
- **Não verificado:** texto livre dentro do atendimento (Motivo da Procura, `MotivoConsulta.aspx`,
  evolução/PEP). Só se vê abrindo um atendimento real (§11).

**Consequência:** a ligação Prime → SISREG se faz **do nosso lado**, por CNS + unidade + data (86%
exata, 98% em ±7 dias na amostra), e não por código. Isso dá ao hub o que o Prime não tem: origem
regulada, procedimento SIGTAP/SISREG e código da solicitação, e permite fechar o ciclo
"agendado no SISREG → compareceu no Prime → CID/SIGTAP".

**Armadilha medida:** `RelatorioPacientesAgendadosRPT` **corta em 100 linhas por chamada**, mesmo
com um único dia (CDT tem mais de 100 por dia). Para cobertura total, fatiar por profissional
(`prof_codigo`) ou função (`idfuncaoatendimento`). O cruzamento acima é **amostra**. Péricles, CMI e
Reabilitação devolveram 0 agendados no mesmo período (atendem sem essa agenda).

### 10.1 Os 14% que não casam (amostra de 100) na mesma data: retorno, enfermagem, unidade divergente (medido)

Pergunta do operador: se toda primeira consulta passa pelo SISREG, quem fica de fora deve ser
retorno. Medido nos 14 agendamentos da amostra (CDT, 09–15/09) que não casaram por CNS + CDT +
mesma data, olhando o **histórico SISREG do paciente no hub** (`smsmarica.solicitacao`, que tem o
CDT de 2019 a 12/2026, 250.786 solicitações):

| Grupo | Casos | O que o SISREG mostra | Leitura |
|---|---:|---|---|
| Consulta c/ **Otorrino** | 4 | consulta de Otorrino **no CDT 2, 2, 8 e 14 dias antes** | **retorno** da consulta regulada |
| **Procedimento** | 1 | USG pélvica **no CDT 1 dia antes** | atendido no dia seguinte (atraso/remarcação) |
| Consulta c/ **Ortopedista** | 3 | (a) OCI ortopedia **na mesma data**, mas SISREG diz executante **Péricles**; (b) ortopedia no Péricles 105 dias antes; (c) nada de ortopedia | (a) **unidade divergente** SISREG × Prime; (b) retorno vindo de outra unidade; (c) sem regulação |
| **Consulta de Enfermagem** | 6 | nenhum procedimento compatível (histórico de nutrição, cardio, exames de anos atrás) | **não regulado**: enfermagem é atendimento interno do CDT |

**Resultado:** excluindo a enfermagem (não regulada), **93 de 94** agendamentos têm origem SISREG
identificável: 86 na mesma data, 4 retornos, 1 no dia seguinte, 1 com unidade divergente e 1
retorno de outra unidade. Só **1** (ortopedia) não tem nada no SISREG.

**Regras de casamento para o conector** (em ordem; a primeira que casa vence):
1. CNS + unidade executante + **mesma data** → "atendimento da solicitação X";
2. CNS + **mesma data** em **outra unidade executante**, mesma especialidade → casa e marca
   "unidade divergente" (o SISREG diz uma, o paciente foi atendido noutra);
3. CNS + unidade + data **±3 dias**, mesma especialidade/procedimento → casa como "atendido fora da
   data agendada";
4. CNS + **mesma especialidade** com solicitação atendida **nos 180 dias anteriores** (qualquer
   unidade) → **retorno** vinculado à solicitação de origem (não é nova solicitação);
5. função **não regulada** (Consulta de Enfermagem; lista a medir por unidade) → ignora, não casa;
6. o resto → "sem regulação": revisão humana.

Compatibilidade de especialidade = de-para "Função de Atendimento" do Prime ↔ `procedimento_texto`/
`especialidade_texto` do SISREG (ex.: Otorrino ↔ CONSULTA EM OTORRINOLARINGOLOGIA; Ortopedista ↔
OCI/CONSULTA EM ORTOPEDIA; Cardiologista ↔ OCI AVALIAÇÃO CARDIOLÓGICA). "Procedimento" e "Consulta
Médica" do Prime não dizem a especialidade, então o casamento é por data e o procedimento vem do
SISREG.

Limites: amostra de 100 agendamentos de uma unidade e uma semana. A regra do retorno (180 dias) e a
lista de funções não reguladas precisam ser medidas no dia inteiro (174+) e em outras unidades.

### 10.2 CDT completo, 09–25/09/2026: `OrigemAgenda`="Unidade" não é retorno (medido)

Coleta: *Pacientes Agendados* fatiado pelos 64 profissionais (`GetDataForRelProducao`) × 13 dias
= 832 chamadas, 71 s, nenhuma fatia no teto. Resultado: **1.403 agendamentos, 1.288 pacientes**.
A agenda do Prime só tem data até **18/09** (16/09: 167, 17/09: 142, 18/09: 1), ou seja, a recepção
lança a agenda perto da data.

- **`OrigemAgenda`="Unidade" em 1.403 de 1.403**, inclusive nas consultas reguladas na mesma data.
  "Unidade" = **agenda lançada pela própria unidade** (o CDT não é "integrado com a regulação" no
  cadastro de unidades). **Não** distingue primeira vez de retorno. Isso só sai pelo cruzamento.

Classificação (`classificar_origem.py`, regras de §10.1):

| Classe | Agendamentos | % |
|---|---:|---:|
| 1 SISREG mesma data, mesma unidade | 1.178 | 84,0 |
| 2 SISREG mesma data, outra unidade executante | 7 | 0,5 |
| 3 SISREG mesma unidade, ±3 dias | 28 | 2,0 |
| 4 **retorno** (SISREG na mesma unidade nos 180 dias anteriores) | 56 | 4,0 |
| 5 função não regulada (Consulta de Enfermagem) | 107 | 7,6 |
| 6 SISREG só antigo (>180d), futuro ou de outra unidade | 10 | 0,7 |
| 7 nada no SISREG / CNS fora do hub | 17 | 1,2 |

- **Origem SISREG explicada (1–4): 1.269 = 90,4% do total; 97,9% dos reguláveis** (tirando a
  enfermagem). Resíduo real: 27 (1,9%).
- **Futuro (17–18/09): 138 de 143 (96,5%) já casam na mesma data.** A recepção lança no Prime o que
  o SISREG agendou.
- **Retorno se concentra onde o especialista acompanha:** Otorrino 28 retornos + 15 ±3 dias em 153;
  Dermatologia 5 em 9; Ginecologia 4 em 30. Fono (64/64), Urologia (6/6), Oncologia e "Consulta
  Médica" (181/185, que no CDT são exames de imagem) são quase 100% mesma data.
- "Procedimento" (358) = exames regulados: 338 mesma data.

### 10.3 Não há importação do TXT do SISREG: a recepção digita (medido)

Hipótese do operador: o Prime teria um menu que importa o TXT do SISREG. Medido de três formas:

1. **Menu** (perfil Gerente; HTML de 123 telas): importações só de **XML do CNES**
   (`importacao/ImportarCnes.aspx`), **Cadastro Voluntário** (`Cadastro_Voluntario/ImportacaoCadastroVoluntario.aspx`,
   que é pesquisa de cidadãos cadastrados entre datas + auditoria, **sem upload**) e **Carga do Prime
   Mobile**. Nada de SISREG ou regulação. Ressalva: o menu da recepção pode ser outro.
2. **15 caminhos plausíveis** (`Agendamento/ImportacaoSisreg.aspx`, `Sisreg/Importacao.aspx`,
   `importacao/ImportarSisreg.aspx`…) → **todos 404**.
3. **A digitação deixa rastro** (`datahoraagenda_registro` × `prof_agendou`, 1.391 agendamentos do
   CDT 09–25/09): uma importação criaria centenas no mesmo segundo pelo mesmo usuário. Medido:
   - máximo **1 agendamento por segundo e 2 por minuto** por usuário; nenhum minuto com 5 ou mais;
   - intervalo entre registros consecutivos do mesmo usuário: p10 58 s, **mediana 188 s (~3 min)**, p90 19 min;
   - **6 usuários**; 2 fazem 82% (617 e 525);
   - registrado **0 a 2 dias antes** da data da agenda (mediana 1, máximo 7), em horário comercial
     (7h–16h, pico 15h), 180–230 por dia útil.

**Conclusão:** a agenda regulada do CDT é **digitada à mão**, um por um, ~3 min cada, por duas
pessoas, na véspera, a partir do que o SISREG agendou. É o retrabalho que o SMSMais pode eliminar:
o hub já tem a agenda SISREG do CDT (`smsmarica.solicitacao`, até 12/2026).

## 11. PEP / SOAP: pendente

- **Prime:** o atendimento clínico abre a partir da agenda por `Pep/IniciarAtendimentoDigitador.aspx?pacienteid=`
  e `AtencaoBasica/ChamadaPaciente.aspx?atendimentoid=`, e o acolhimento por
  `MotivoConsulta.aspx?acolhimentoid=`. "Iniciar" e "Chamar" **executam ação** (criam atendimento,
  chamam no painel) e **não** podem ser abertos por sonda. Nenhuma tela de *leitura* do PEP foi
  achada nas capturas.
- **Saúde Mental:** `Relatorios/RelatorioImpressaoSOAP.aspx?qId=<id>` é a impressão do SOAP (leitura),
  linkada a partir da lista de famílias/pacientes (`SOAP.aspx`). Precisa de um id real.
- Caminho seguro proposto: um operador abre **um** atendimento no navegador com o DevTools gravando
  (HAR), ou por Claude in Chrome na sessão dele. Depois `analisar_har.py` (do lab Klinikos) extrai as
  telas de leitura e seus parâmetros, sem clicar em ação.

## 12. Caminho barato para marcar nas *Solicitações* (chegada, início, atendido, falta), com custo medido

Objetivo do operador: no SMSMais, em Solicitações, marcar que o paciente regulado **chegou**, a
**hora em que foi atendido**, **faltou** etc., a partir do Prime. Medido no **CDT, dia 15/09/2026**,
uma sessão só (`sondar_custos.py`).

**Semântica dos horários (medida, não presumida):**
- *Pacientes Atendidos* `DataRegistro` **não** é a hora do atendimento: é a digitação, às vezes dias
  depois (atendimento de 09/09 registrado em 11/09). `Duracao` mediana de 1 min, `DataInicio/DataFim` só
  com a data. Serve para **data do atendimento + CID + SIGTAP**, não para relógio.
- **O relógio real está no log do agendamento** (`Agendamento/DetalhesLogAgenda.aspx?AgendaId=`):
  eventos com `dd/mm/aaaa hh:mm:ss`: **"Acolhimento realizado" = chegada**, **"Atendimento Iniciado" =
  início**, além de "Agendar"/"Desagendar". Em 30 logs do dia: 26 com chegada e 17 com início.

**Pipeline por unidade × dia:**

| Passo | Endpoint | Custo medido | Entrega |
|---|---|---|---|
| 1. Profissionais | `Services/ComboProfissionalPorUnidadeService.asmx/GetDataForRelProducao` `{"context":{"Text":"","NumberOfItems":0,"UnidadeId":<guid>}}` | 0,1 s, 1 chamada | 64 profissionais (CDT). **Não** usar `…PorEsquipe…GetData`/`…PorUnidade…GetData` (10; cobrem 0–34) |
| 2. Agendados por profissional | `RelatorioPacientesAgendadosRPT.aspx?prof_codigo=<id>&dtInicio=dia&dtFim=dia&extensao=CSV&unidades=<guid>` | ~0,05 s × 64 | **174 agendamentos distintos**, nenhuma fatia no teto de 100. `agendaid`, `datahoraagenda`, `cns_numero`, profissional, função |
| 3. Log de cada agendamento | `Agendamento/DetalhesLogAgenda.aspx?AgendaId=<agendaid>` | 0,05 s, 18 KB × N | chegada e início com hora |
| 4. Atendidos do dia | `RelatorioPacientesAtendidosRPT.aspx` | 0,25 s, 142 KB | 245 registros: CID, SIGTAP, função (fecha o "atendido") |
| 5. Em aberto | `RelatorioAtendimentoEmAbertoRPT.aspx?…&extensao=CSV` | 0,09 s | quem iniciou e não fechou (`DataInicio`, profissional, CBO, classificação) |
| 6. Faltantes | `RelatorioPacientesFaltantesRPT.aspx?…&grupoPrioritario=0` | 0,03 s | **CNS + CPF**, `Situacao`, `UsuarioRegistroFalta`. Com `grupoPrioritario` vazio dá **500** |

**Custo total de rede, CDT, 1 dia:** ~65 chamadas de lista (~3 s) + ~175 logs (~9 s) + 3 relatórios
(<0,5 s) = **~12–15 s de rede, ~250 requisições**. Com 0,2 s de pausa entre chamadas, ~1 min por
unidade/dia. Incremental durante o dia: reler só os logs dos agendamentos **ainda sem "Atendimento
Iniciado"** (o log é pequeno).

**Ligação com a Solicitação:** agendamento Prime (CNS + unidade + dia) → `smsmarica.solicitacao`
(paciente por `fhir.patient.cns/cns_todos`, `unidade_executante_id`, `data_agendada`). Amostra CDT: 86%
casa na mesma data (§10). Casamento ambíguo (mais de uma solicitação no dia) vai para revisão.

**Pendências antes de construir:** usuário dedicado (sessão única, §7); confirmar que o Faltantes com
`grupoPrioritario=0` não filtra grupo; medir outras unidades (Péricles, CMI e Reabilitação não usam
essa agenda); escrita nas Solicitações é produção e precisa de OK e desenho (ADR/feature).

## 9. A busca de paciente mistura duas bases — e é isso que parece "bug do CadWeb"

Medido em 21/09/2026, sessão da unidade `2b89b351` (Ambulatório), busca por CPF na
`Agendamento/AgendaRecepcao.aspx` (lupa `imbConsulta`, `rblPesquisarPor=CPF`).

A grade de resultado é a `gridBuscaPaciente`, e ela traz linhas de **duas origens**. O
discriminador é a coluna **Código** (o GUID do paciente no Prime) somada à **Situação do
Cadastro**:

| Código | Situação do Cadastro | Significa |
|---|---|---|
| GUID real | (vazio / normal) | paciente **existe no Prime** |
| `00000000-0000-0000-0000-000000000000` | `Paciente CadWeb` | **não existe no Prime** — a linha é um eco da consulta ao CadWeb |

Exemplo medido (CPF `056.840.827-69`): volta uma linha com nome, mãe, nascimento, CPF e CNS
corretos, `Situação do Cadastro = Paciente CadWeb` e **Código zerado** — `1 items in 1 pages`.

### Por que a tela de cadastro "diz que não existe"

O operador relatou: na tela de cadastro, buscar pelo CadWeb sempre devolve "não existe";
na tela principal de pesquisa, o mesmo CPF aparece como cadastro CadWeb existente.

As duas telas não fazem a mesma pergunta. **Quem resolve o CadWeb é a tela de busca**, e ela
repassa o resultado para o cadastro pelo POST: ao clicar `imbCadastro`, o corpo enviado para
`Paciente/CadastroPaciente.aspx` leva um bloco `key_*` já preenchido —
`key_CNS`, `key_pac_cpf`, `key_pac_nome`, `key_pac_mae`, `key_pac_nascimento`,
`key_endereco_*` e `key_pac_SistemaOrigem=CadWeb`. A tela de cadastro **recebe** os dados; ela
não os busca. A busca CadWeb de dentro do cadastro é outro caminho, e é o que falha.

**Consequência para automação:** "verificar se o paciente existe" não é "a busca retornou
linha". É **a linha ter Código diferente de zero**. Uma automação que tratar linha do CadWeb
como paciente existente vai tentar agendar contra o GUID nulo.

## 10. Cadastrar paciente por fora da tela: o que trava (medido 21/09/2026)

Sequência real, observada pela extensão enquanto a recepção trabalhava:

```
imbCadastro (na linha da grade)  -> AgendaRecepcao      full postback
cross-page                       -> CadastroPaciente    full postback, traz os key_* do CadWeb
rcboUfNascimento                 AutoPostBack   ASYNC=true
rcboMunicipioNascimento          AutoPostBack   (combo encadeado: depende da UF)
rblOrientacao$1        = N       AutoPostBack
rblIdentidadeGenero$1  = N       AutoPostBack
rblEstaSituacaoRua$1   = N       AutoPostBack
chkNomePaiDesconhecido = on      AutoPostBack
rbSalvar                         AutoPostBack   <- grava
```

**Três armadilhas, todas com o mesmo sintoma: HTTP 200 e nada gravado.**

1. **`rbSalvar` é RadButton `rbLinkButton`** — renderiza `<span>`, não tem `name`, só
   `_ClientState`. Procurar por `name` devolve None, e um POST com `__EVENTTARGET` vazio volta
   200 com a página inteira. O alvo verdadeiro (`ctl00$ctl00$DefaultContent$rbSalvar`) só existe
   no `_postBackReference` do JS.
2. **Tudo é partial postback.** Sem `__ASYNCPOST=true`, `<ScriptManager>|<alvo>`,
   `RadAJAXControlID` e o header `X-MicrosoftAjax: Delta=true`, o handler não roda.
3. **Cada campo exige o SEU postback.** Mandar os seis complementos juntos no POST do Salvar não
   funciona — o servidor nunca processou os eventos. E cada resposta renova o
   `__EVENTVALIDATION`: é preciso aplicar o delta (`len|tipo|id|conteudo|`, fatiado pelo `len`,
   nunca por `split('|')`) antes do POST seguinte.

**O que NÃO trava:** tipo/número de documento. Cinco dos seis `rbSalvar` do operador gravaram
sem eles. Mensagens como "Informe o nome do cidadão" e "Informe o CNS" aparecem no HTML **também
nos que gravaram** — são templates de validador, não erro. O único que falhou foi um teste com
`rblNacionalidade = Naturalizado`, que liga um bloco novo (data de entrada no Brasil, data de
naturalização, etnia).

### O eco do CadWeb depende do USUÁRIO

Mesmo CPF, mesma unidade, mesma tela: com uma conta a busca devolve a linha "Paciente CadWeb"
(Código `00000000-…`); com outra devolve **"Nenhum registro encontrado"**. A consulta ao CadWeb
é função do usuário/perfil, não da unidade. Uma carga em massa precisa validar isso na conta que
vai usar, senão conclui "o paciente não existe" quando o que faltou foi permissão.

### Custo medido (por paciente, sem login)

| caminho | requisições | tempo | tráfego |
|---|---:|---:|---:|
| até a tela de cadastro preenchida | 4 | 3,6 s | 2,2 MB |
| com os AutoPostBacks + Salvar | 10 | 12,3 s | 6,6 MB |

O GET inicial da agenda custa ~3,5 s / 464 KB e vale para o lote inteiro — **mas só dentro da
mesma sessão**: se alguém logar com a mesma conta e derrubá-la, o estado guardado morre junto.
Os `.asmx` (consulta pura, JSON + cookie, sem ViewState) respondem em 15–30 ms.

### Correção (mesma sessão): o eco do CadWeb é INTERMITENTE, não é permissão

A seção acima afirmou que a consulta ao CadWeb dependia do usuário/perfil, a partir de um
"Nenhum registro encontrado" numa conta e do eco normal em outra. **Está errado.** Minutos
depois, a MESMA conta que havia falhado voltou a devolver a linha "Paciente CadWeb" para o mesmo
CPF, sem nenhuma mudança de perfil.

O comportamento certo é tratar a ausência do eco como **falha transitória**: repetir, e nunca
concluir "o paciente não existe no CadWeb". Uma carga em massa que interpretar o vazio como
resposta definitiva vai cadastrar em duplicidade na primeira intermitência.

## 11. Cadastro por fora da tela: o que já foi ELIMINADO (21/09/2026)

Cinco tentativas de gravar um cadastro por requisição, todas com HTTP 200 e nada gravado.
Hipóteses testadas e **descartadas com evidência**:

- **Tipo/número de documento** — não é obrigatório na prática: 5 dos 6 `rbSalvar` da recepção
  naquele dia gravaram sem eles.
- **Mensagens na resposta** ("Informe o nome do cidadão", "Informe o CNS", "Informe o local de
  atendimento") — são **templates de validador no HTML**, aparecem igualmente nas respostas que
  GRAVARAM. Não servem como diagnóstico.
- **`_ClientState` dos RadComboBox** — o POST que gravou **não traz** `_ClientState` preenchido
  para `rcboUfNascimento`/`rcboMunicipioNascimento`/`rcboRaca`; manda só o texto, igual ao nosso.
- **Campos-espelho** (`txtIBGEMunicipioNascimento`, `txtCodigoPaisNascimento`, `txtIdade`,
  `txtNumeroProntuario`) e os sete `group*_porCPF = radio*Prime` — implementados a partir do POST
  que gravou; **não resolveram**.

Pista ainda não explorada: no delta da nossa resposta **nenhum validador vem marcado
`isvalid=False`**, o que sugere que o servidor talvez nem chegue a validar. E o
`ValidationSumary` declara `validationGroup = "PacienteDefinitivo"`, enquanto os 14 validadores
ativos estão **sem grupo** — um botão com ValidationGroup não dispara validadores fora dele.

**Próximo passo recomendado:** parar de deduzir pelo POST final e capturar pela extensão um
cadastro COMPLETO feito na tela, do `imbCadastro` ao `rbSalvar`, comparando a **sequência
inteira** de postbacks (quantos, quais alvos, em que ordem) — e não só o último corpo. A
recepção navega pelas abas do `RadMultiPage`, e isso pode instanciar estado que a nossa
sequência enxuta nunca cria.

## 12. A causa raiz: o ENDEREÇO é obrigatório (21/09/2026)

Achada comparando dois `rbSalvar` do MESMO cadastro, com 40 s de diferença, ambos capturados
pela extensão: **283 campos idênticos**, e a gravação só passou quando estes quatro, todos do
user control `UCEndereco`, vieram preenchidos:

```
UCEndereco$rcboTipo        (vazio) -> 001 - RUA
UCEndereco$rcbLogradouro   (vazio) -> ESCRITOR RODRIGO MELO FRANCO
UCEndereco$txtNumero       (vazio) -> 160
UCEndereco$txtComplemento  (vazio) -> AP1501
```

Por que custou tanto a aparecer: **o validador vive DENTRO do user control**, e por isso não
consta nos `Page_Validators` que o delta publica para o painel principal. Procurar a
obrigatoriedade na lista de validadores da página leva ao lugar errado. O CEP **não** precisa
existir na base de CEPs — o operador digita UF, município, bairro, tipo, logradouro e número na
mão e grava.

**Armadilha de método, e é a lição real desta sessão:** decidimos cedo "endereço não é
obrigatório, a recepção preenche depois" e tiramos o bloco da carga. Foi essa decisão que
quebrou o cadastro, e ela envenenou cinco tentativas seguidas de diagnóstico — cada uma
perseguindo validadores do painel principal que nunca foram o problema. Quem achou foi o
operador, testando na tela.

**Consequência para a carga em massa:** a ficha mínima NÃO é só identidade. É identidade +
endereço com tipo de logradouro, logradouro e número. E o tipo de logradouro não pode vir do
`key_endereco_codigoTipoLogradouro` do CadWeb (ver §9) — tem que ser derivado do texto ou
confirmado por gente.

Três blocos de endereço dividem os mesmos sufixos na tela (`rcboTipo`, `txtNumero`, `txtCEP`…).
Mirar pelo sufixo escreve no bloco errado: é preciso filtrar por `$UCEndereco$`.

## 13. A causa raiz REAL: o valor mora no _ClientState de cada controle Telerik (21/09)

Achado com o padrão-ouro: um Salvar feito à MÃO que gravou (Daniel, v0.7.0 da extensão),
diferido contra o meu replay com dado idêntico. Os campos planos batiam; o que faltava era o
`_ClientState`.

**Todo controle Telerik (RadTextBox, RadMaskedTextBox, RadDateInput, RadComboBox) guarda o
valor REAL num hidden `<name>_ClientState`, em JSON — e o servidor lê de lá, não do input.**
No Salvar que gravou:

```
RadTextBoxCNS_ClientState   validationText: "708202616177143"
txtCPF_ClientState          validationText: "101.113.867-05"
txtCEP_ClientState          validationText: "26155-125"   (mascarado)
dateInput_ClientState       validationText: "1984-07-23-00-00-00"
radComboBoxPais_ClientState value:"0001" text:"BRASIL"
```

No replay, todos vazios. Mandar `RadTextBoxCNS=708...` no campo plano NÃO basta: o servidor vê
o CNS como vazio e o RequiredFieldValidator dispara DE VERDADE.

**Correção do §11:** os avisos de validação NEM sempre são template. Para os campos Telerik eles
eram reais — o comparador os escondia porque `_ClientState` estava na lista de ruído. Foi o que
mascarou a causa por horas.

**Como preencher sem reinventar o Telerik:** a tela em branco já entrega cada `_ClientState`
com o schema completo (todas as chaves, valor vazio). Pegar o shipped e injetar o valor em
`validationText`+`valueAsString` (e `value`+`text` nos combos) é o replay menos frágil.

**Mas é a QUINTA armadilha Telerik da mesma tela** (alvo do RadButton só no JS; partial postback
obrigatório; AutoPostBack por campo + renovação do EVENTVALIDATION; endereço obrigatório dentro
do UCEndereco; ClientState de todo controle). Cada uma falha com HTTP 200 e some sozinha.
Recomendação registrada: para carga em massa, DIRIGIR O NAVEGADOR (deixar o JS do Telerik montar
o ClientState) elimina as cinco de uma vez — o `SMSMais.integrador` já dirige Chrome por CDP.
O replay HTTP continua ótimo para LEITURA (busca, .asmx, relatórios), que não tem ClientState.

## 14. Veredito do replay de ESCRITA: não converge — usar navegador (21/09)

Depois de achar e corrigir CINCO causas (RadButton, partial postback, AutoPostBack+EV,
endereço obrigatório, ClientState de todo controle), o Salvar por requisição AINDA falha, e a
resposta agora dá os validadores REAIS que reprovam (isvalid=false), todos "sem grupo":

```
RequiredFieldValidator15  rdpDataNascimento   Informe a data de nascimento
cvUfNascimento            (custom)            Informe a UF de nascimento
cvMunicipioNascimento     (custom)            Informe o município de nascimento
RequiredFieldValidator1/2 documento           Informe tipo/número do documento
```

O que cada um revela:
- **Data:** eu mando `rdpDataNascimento=1974-03-16` (ISO), mas o RadDatePicker posta e valida
  pelo sub-campo `rdpDataNascimento$dateInput` em **dd/mm/aaaa**. Campo e formato errados.
- **cvUfNascimento / cvMunicipioNascimento:** são validadores CUSTOM (lógica server-side que não
  vemos). Provavelmente checam o `value` (código) do combo, que eu deixo vazio — mando só o
  `text`. Precisaria do código IBGE/UF no ClientState `value`.
- **documento:** aparece como obrigatório aqui, mas cadastros manuais gravaram sem — ou seja há
  lógica condicional que não domino.

**Conclusão honesta:** cada correção revela outra camada; o alvo é MÓVEL (validadores custom +
estado Telerik + contexto de sessão) e só o JS do cliente real o satisfaz de forma coerente. O
replay de escrita não converge sem reimplementar o cliente Telerik + a lógica custom do servidor,
e cada tentativa é um hit em produção. **Para ESCREVER, dirigir o navegador** (o JS monta tudo).
O replay continua ÓTIMO e recomendado para LER (busca, .asmx, relatórios) — nada disso valida.

## 15. DESTRAVADO: o combo valida pelo CÓDIGO INTERNO, resolvível via .asmx (21/09)

Correção do §14 ("não converge"): converge sim. A causa dos `cvUfNascimento`/`cvMunicipioNascimento`
era o `value` do ClientState do combo. O navegador manda o CÓDIGO INTERNO do Prime, não o texto
nem o IBGE:

```
rcboUfNascimento        value="RJ"       text="RJ"
rcboMunicipioNascimento value="007043"   text="RIO DE JANEIRO"   (007043 != IBGE 3304557)
rcboRaca                value="3"         text="AMARELA"
```

O servidor acumula o objeto ao longo dos postbacks async e valida por esse código interno. Meu
replay mandava `value` vazio -> nascimento acumulado vazio -> reprova no Salvar.

**O código é resolvível sem adivinhar** — pelo mesmo .asmx que o combo usa ao carregar:
```
POST /Prime/Services/ComboMunicipioService.asmx/GetData
  {"context":{"Text":"rio","NumberOfItems":0,"UF":"RJ","CODIGOIBGE":"COM"}}
  -> Items:[{"Text":"RIO DE JANEIRO","Value":"007043"}]
```
Cascata: município exige `UF` no context (sem ela, 500 "Object reference"). UF nasc = a própria
sigla. Raça e bairro têm seus próprios serviços (a descobrir pelo mesmo método — as chamadas
estão capturadas no acervo do cadastro manual).

**A extensão NÃO filtra** — o `007043` veio limpo na captura, sem máscara. Não é preciso versão
"sem filtro"; a v0.7.0 já revela tudo, inclusive os códigos internos.

**Consequência:** o replay de escrita é viável com um RESOLVEDOR de código de combo (chama o .asmx
de cada combo, casa texto->Value). Continua mais frágil que dirigir o navegador (cada combo é um
serviço com seu context), mas é determinístico e agora entendido ponta a ponta.

## 16. FUNCIONOU: cadastro por API ponta a ponta (21/09)

Um paciente foi gravado por requisição, sem navegador e sem humano — confirmado pela busca
(Código real, não mais 00000000). (GUID/identidade do teste ficam fora do repo, no scratchpad.)

Os três buracos finais, resolvidos com valor MEDIDO contra o navegador (não deduzido):
1. Data — o RadDatePicker valida pelo sub-campo `<picker>$dateInput` em dd/mm/aaaa. Faltava ele.
2. UF de nascimento — `value` = a sigla.
3. Município de nascimento — `value` = código interno, resolvido por `combos.municipio(uf, nome)`
   via ComboMunicipioService.asmx, idêntico ao que o navegador gera ao preencher à mão.

Custo: ~15 requisições, ~8 s por cadastro (13 postbacks + save + os .asmx de resolução).
Os validadores de DOCUMENTO (isvalid=false na resposta) NÃO bloqueiam — são "sem grupo", fora do
grupo do Salvar (PacienteDefinitivo). Confirmado: gravou com documento vazio.

Receita completa em prime/combos.py (resolvedor) + probe_cadastro_direto.py. Frágil como o §15
descreve; para MASSA, pesar contra navegador headless. Mas está PROVADO que a via-API grava.

## 17. TIRO ÚNICO: os postbacks NÃO são necessários (21/09)

Testado com o protocolo do operador: (A) descobrir os códigos internos numa sessão
(`--resolver-so` grava em `codigosInternos` na ficha), (B) ENCERRAR a sessão, (C) login novo +
GET + UM POST (`--sem-postback --salvar`, sem chamar o resolvedor — usa os códigos da ficha).

Gravou. Custo: **2 requisições, ~2 s** (GET + POST), contra 15 req / 8 s do caminho com os 13
AutoPostBacks. Confirmado pela busca.

**Conclusão:** os AutoPostBacks são só a coreografia do navegador — o servidor NÃO exige que os
valores tenham passado por postback. O `__EVENTVALIDATION` do formulário em branco aceita os
códigos de combo (`value` interno) mandados de uma vez. (Aposta anterior de que a EV barraria:
ERRADA — bom ter testado.)

**Consequência para MASSA:** o padrão vira `resolver códigos 1x (cachear tabela município/raça/
bairro) -> GET + 1 POST por paciente`. ~2 req/paciente. Muda muito a fragilidade do §15/§16: a
dança de postbacks some; sobra só (a) montar os ClientState certos e (b) manter as tabelas de
código. Ainda vale cachear município/raça (são estáveis) em vez de resolver a cada cadastro.
Identidade dos pacientes de teste fica no scratchpad, fora do repo.

## 18. ACHADO: o prontuário do Prime é o módulo /Pep/ em estrutura SOAP (23/09/2026)

A extensão rodou no PC de um MÉDICO (v0.9.0, ~1.170 capturas em 2h) e trouxe telas que nenhuma
varredura anterior alcançou — porque não são item de menu: abrem de dentro do atendimento.

```
/Prime/Pep/soap.aspx                    a NOTA CLÍNICA (SOAP)
/Prime/Pep/SoapPrescricao.aspx          prescrição
/Prime/Pep/RelatoriosSoap.aspx          relatórios do atendimento
/Prime/Relatorios/RelatorioImpressaoSOAP.aspx (+ /GenerateReport)
```

**Chaves do modelo clínico** (todas GUID, vistas nos hidden da tela):
```
hiAtendimentoId / hidAtendimentoId / atendimentoId   <- o ENCONTRO (Encounter)
hiPacienteId / hidPacienteId                          <- o paciente
hidFuncaoAtendimento, hidCodigoUnidade, hidEnderecoId, hidCbosPermitidos
```

**Campos da nota** (RadEditor, um por letra do SOAP):
`radSubjetivo`, `radObjetivo`, `radAvaliacao`, `radPlano`. Confirmado com conteúdo real no
`radAvaliacao`. O valor chega PERCENT-ENCODED no corpo do XHR (`%2c` = vírgula) — quem parsear
precisa decodificar.

Abas por seção: `rbpSubjetivo_i0/i1/i2` (RadPageView) com user controls próprios —
`UCEditSubjetivo2`, `UCSoapMotivoConsulta2`, `UCSoapDadosPessoais1`.

**Codificação clínica por serviço** (mesmo padrão .asmx de combo do §15):
```
UCComboCidService.asmx/GetData            "REFLU" -> K21/K210/K219 (Value = o próprio CID)
UCComboCIAP2Service.asmx/GetDataProblema  CIAP-2
ComboExamesService.asmx/GetExamesPorParametros   solicitação de exames
```

**Prescrição** (`SoapPrescricao.aspx`): `txtPrescricaoLivre` (texto livre, capturado com conteúdo
real), `rblTipoPrescricaoReceita` (Simples/Controlado), `chkReceituarioControlado`,
`hidPadronizacao`, `rblOpcaoDeFrequencia` (Intervalo), `ddlTipoFrequencia` (H), `ddlTipoDuracao` (D).

**Corrige o §? anterior:** a conclusão de que "o Prime é agenda/recepção, não prontuário" estava
ERRADA em dobro — não só o clínico existe (§ relatório de Pacientes Atendidos), como há um PEP
completo com SOAP, CID, CIAP-2, exames e prescrição. O que faltava era capturar na máquina certa.

## 19. Custo de trazer o clínico do Prime para o hub (medido 23/09/2026, sessão da Clarisse)

Três vias, medidas de verdade. Todas SOMENTE LEITURA.

### A) Lote diário estruturado — 1 requisição por dia/unidade
```
GET /Prime/Relatorios/RelatorioPacientesAtendidosRPT.aspx
    ?dataInicio=DD/MM/AAAA&dataFim=DD/MM/AAAA&funcao=&unidades=<GUID>&extensao=CSV
```
| formato | tempo | tamanho | conteúdo |
|---|---:|---:|---|
| CSV | **248 ms** | **135 KB** | **227 atendimentos** de 1 dia/1 unidade (CDT) |
| PDF | 4.271 ms | 915 KB | mesmo conteúdo, 17x mais lento |

Traz: paciente (nome/CNS/nascimento/endereço+telefone), profissional, função de atendimento,
demanda, **Diagnosticos (CID)**, **Procedimentos + Cod_Procedimentos (SIGTAP)**, exames
solicitados, medicamentos e o **tempo**. **Não traz a narrativa do SOAP**, nem o `atendimentoId`
ou o GUID do paciente. **Sempre usar CSV, nunca PDF.**

**CORREÇÃO de duas afirmações minhas erradas (23/09/2026).** Escrevi acima "622 registros" e que
`ExamesSolicitados`/`Medicamentos`/`DataInicio/Fim/Duracao` "vieram VAZIAS". As duas saíram do meu
parser ingênuo, não do Prime. Com a remontagem correta (`prime/relatorio_csv.py`), no CDT em 22/09:
**227 atendimentos** (622 eram fragmentos de linha), `Duracao` e as datas em **100%**,
`Procedimentos`/`Cod_Procedimentos` em 100%. Lição: contagem tirada de parser malformado mente com
cara de dado, e eu publiquei a mentira duas vezes antes de conferir por um segundo método — o
`sondar_atividade.py`, que conta por CRLF, dava 227 desde sempre.

**Ler o preenchimento pela FUNÇÃO do atendimento, senão o clínico parece pior do que é.** Das 227
linhas do CDT, 129 são `FuncaoAtendimento = "Procedimento"` (acolhimento/escuta inicial), que **por
natureza** não têm CID, exame ou medicamento. Nas 98 CONSULTAS: **CID em 98/98 (100%)**, exame
solicitado em 29, medicamento em 0. Misturar as duas dava "43% com CID" e subestimava tudo.

**ARMADILHA — o CSV é malformado.** Cabeçalho com 22 colunas (21 `;`), mas a maioria das linhas
físicas tem só 11 `;`, e o arquivo **não tem nenhuma aspa**: os campos `<procedimento>…</procedimento>`
contêm QUEBRA DE LINHA crua. `csv.DictReader` desalinha as colunas em silêncio — foi o que fez o
diagnóstico sair como duração ("50s", "1min 2s"). Quem for importar tem que remontar o registro
juntando linhas físicas até acumular 21 `;` — é o que `prime/relatorio_csv.py` faz, e o
`conferir()` dele tem que ser olhado SEMPRE antes de confiar na importação.

### B) Narrativa do SOAP — POR ATENDIMENTO, cara
```
POST /Prime/Pep/RelatoriosSoap.aspx           (atendimentoId -> gera o qId)
GET  /Prime/Relatorios/RelatorioImpressaoSOAP.aspx?qId=&unidadeId=     18 ms / 15 KB (só a casca)
POST /Prime/Relatorios/RelatorioImpressaoSOAP.aspx/GenerateReport      ~360 KB
     {"queryString":"?qId=&unidadeId=&usu_codigo=&qAtestados=&qDeclaracoes=&qItems=REPL-&..."}
     -> {"d":{"isZip":false,"pdfBase64":"JVBERi0..."}}   PDF em base64
```
2–3 requisições por encontro e devolve **PDF** (narrativa NÃO estruturada — exigiria extrair texto).
Para ~175 atendimentos/dia numa unidade: **~350–525 requisições e ~60 MB/dia**.
O `qId` capturado ontem ainda funcionava hoje — não expira de imediato.
`qAtestados`/`qDeclaracoes`/`qItems` selecionam os blocos: é por aqui que saem os **DOCUMENTOS**
(atestado, declaração) além do prontuário.

### C) A extensão (já temos) — de graça e já estruturado
Captura `radSubjetivo/radObjetivo/radAvaliacao/radPlano` no momento em que o médico digita, com
`atendimentoId` e `pacienteId` juntos. Custo ZERO para o Prime. Limite: só nas máquinas instaladas.

**Recomendação:** A para o lote diário (barato), C para a narrativa (grátis e estruturada),
B sob demanda quando o hub precisar do documento impresso/atestado de um atendimento específico.

## 20. Backfill e conferência da noite: o relatório é a espinha, a extensão é o bônus (23/09/2026)

Três perguntas do Bernardo — backfill do que já foi atendido, não dá para instalar a extensão em
todo PC ainda, e como garantir depois (de noite) que tudo rodou + **pegar o horário de término**.

### O horário de término existe, mas não está onde parece
| coluna | tem hora? | o que é |
|---|---|---|
| `DataInicio` | **NÃO** — `00:00:00` nos 227 | só a data |
| `DataFim` | **NÃO** — `00:00:00` nos 227 | só a data |
| `DataRegistro` | **SIM**, 100% (`22/09/2026 07:53:31`) | **o fechamento do registro** |
| `Duracao` | 100% (`50s`, `1min 2s`) | duração real |

Então **`fim = DataRegistro`** e **`inicio = fim − Duracao`**. O nome engana: quem for pelo
`DataFim` importa meia-noite em 100% dos atendimentos e não percebe.

Conferido que `DataRegistro` é o FIM, não o começo: na linha do tempo de cada profissional, o
intervalo entre registros consecutivos comporta a duração do seguinte em 129 dos 170 pares. Os 41
restantes se espalham por TODAS as funções na proporção do volume — é o profissional fechando
registros em fila, não erro de leitura.

### O "está lá até agora" é real e tem número
Em 22/09, **12 atendimentos foram fechados só na manhã do dia 23** (CDT 3, Melhor em Casa 8,
Reabilitação 1) — `DataRegistro` cai no dia seguinte enquanto o atendimento aparece no relatório
de 22. É exatamente o que o Bernardo descreveu ("todo mundo no CDT está lá até agora"). **Consequência
para a importação: deduplicar pela CHAVE, nunca pela data do `fim`** — e reprocessar o dia anterior
na conferência da noite, senão esses 12 entram como atendimento do dia errado ou entram duas vezes.

### O backfill inteiro: 243.330 atendimentos, 10 unidades, 36 requisições

Executado em 23/09/2026 (`01/01/2024` a hoje, `--de-uma-vez`), 0 falhas:

| unidade | atendimentos | consultas | período |
|---|---:|---:|---|
| CENTRO MATERNO INFANTIL | 69.293 | 41.690 | 27/03/2025 → hoje |
| CENTRO DE REABILITAÇÃO AMB. E DOMICILIAR | 69.121 | 45.118 | 27/05/2025 → hoje |
| AMBULATÓRIO PÉRICLES SIQUEIRA FERREIRA | 47.769 | 32.758 | **12/03/2025** → hoje |
| SAE | 23.357 | 15.450 | 15/04/2025 → hoje |
| CEO ITAIPUAÇU | 17.038 | 17.038 | 12/05/2025 → hoje |
| CEO BOQUEIRÃO | 8.646 | 8.646 | 12/05/2025 → hoje |
| MELHOR EM CASA MARICÁ | 4.008 | 3.077 | 07/07/2026 → hoje |
| CDT DR ALBERTO LUIS MACHADO BORGES | 3.942 | 2.180 | **24/08/2026** → hoje |
| ODONTOMÓVEL | 139 | 139 | 30/07/2026 → hoje |
| CEREST | 17 | 13 | 25/09/2025 → hoje |

**O Prime começa em 12/03/2025** — antes disso não há nada para trazer. CDT e Melhor em Casa
entraram agora (ago/jul de 2026), então "o histórico do CDT" são 30 dias, não anos.

### A chave natural NÃO basta — e só a escala mostrou
Chave = (unidade, fechamento, profissional, paciente) parecia suficiente: nos 227 do dia deu
227/227 distintas. Nos **120.832** do primeiro lote histórico, 15 grupos colidiram — e **14 eram
atendimentos DIFERENTES de verdade** (mesmo paciente, mesmo profissional, mesmo segundo,
procedimento SIGTAP diferente: `0301100039` × `0301100250`). Importar com ela fundiria atendimento
real e ninguém veria.

Consertado com **ordinal** dentro do grupo (`atribuir_chaves()`), não com hash de conteúdo: hash de
conteúdo muda quando o profissional EDITA o atendimento depois, e o hub ganharia um fantasma em vez
de atualizar. Depois do conserto: **243.330 chaves distintas em 243.330 atendimentos, 0 colisão**.
O re-chaveamento foi feito **sobre os NDJSON já baixados**, sem uma requisição a mais ao Prime.

**Lição:** teste de unicidade de chave em 227 registros não prova nada. Só a escala mostra.

### Prime é a ESPECIALIZADA — 1.093 atendimentos/dia em 8 unidades
Varredura de 22/09 nas 36 unidades do gate: só 8 têm movimento (Ambulatório Péricles 239, CDT 227,
Reabilitação 193, Centro Materno Infantil 188, CEO Itaipuaçu 77, Melhor em Casa 71, SAE 61, CEO
Boqueirão 37). **As USFs vêm zeradas por natureza** — atenção básica vive no Klinikos. Zero numa
USF não é alerta; zero numa das 8 é.

### O PC que instalamos ontem é de RECEPÇÃO, não de médico
A instalação `de010365` mandou 3.027 capturas em 22/09 e **nenhuma** do `/Prime/Pep/`: só
`AgendaRecepcao.aspx` (2.916) e `CadastroPaciente.aspx` (772). Narrativa clínica capturada no dia:
**0 de 1.093**. Não é defeito da extensão — é onde ela foi posta. A conferência da noite passou a
acusar isso sozinha ("mandou N capturas mas NENHUMA do PEP").

Também: **`payload.operador` vem VAZIO em todas as capturas do Prime** — a extensão não carimba
quem é o operador logado no Prime. Sem isso não dá para cruzar extensão × relatório por
profissional, só por contagem. É o próximo conserto na extensão.

### As ferramentas
| ferramenta | o que faz |
|---|---|
| `prime/relatorio_csv.py` | remonta o CSV malformado (`ler`) + `conferir()`, que é heurística de sanidade — **olhar antes de importar** |
| `prime/atendidos.py` | normaliza uma linha do relatório em atendimento (`fim`/`inicio` derivados, CID, SIGTAP com quantidade, exames, medicamentos) + `chave()` sintética (sha1 de unidade+fechamento+profissional+paciente), já que o relatório não traz o `atendimentoId` |
| `backfill_atendidos.py INI FIM [--seco] [--unidade G] [--refazer]` | varre dia × unidade → NDJSON em `capturas/backfill/` + `_resumo.csv`. **Retomável** (pula o que já existe) e com `--seco` para ver o tamanho da varredura antes de metralhar o Prime |
| `conferir_noite.py [DD/MM/AAAA] [--sem-banco]` | cruza relatório (verdade) × capturas da extensão no hub (cobertura), imprime o quadro e os ALERTAS, e sai com código ≠ 0 quando há alerta — para o agendador reclamar sozinho |

### O backfill histórico custa 36 requisições, não 3.600

Eu tinha dimensionado o backfill como dia × unidade (248 ms cada) e chegado a milhares de
requisições. **Errado: o relatório aceita JANELA LARGA.** `dataInicio=01/01/2024&dataFim=hoje`
numa requisição devolve o histórico inteiro da unidade — CDT: 3.942 atendimentos, 2,2 MB, 3,2 s.
Reabilitação: 69.121. Ambulatório Péricles: 47.769. **Uma requisição por unidade cobre tudo**
(`backfill_atendidos.py INI FIM --de-uma-vez`).

Dia a dia continua servindo para a **conferência diária**; para o histórico, janela larga.

Lição repetida: antes de otimizar o laço, perguntar se o laço precisa existir. Passei o custo de
"1 dia" para "N dias" sem testar se o relatório aceitava o intervalo — e aceitava desde sempre.

## 21. Levar o Prime para o hub FHIR: o que casa, o que não tem onde pousar (23/09/2026)

Decisão do Bernardo: "importar quem temos 100% de certeza em relação ao CNS".

### "CNS confiável" NÃO é "CNS definitivo" — e quase errei feio
A leitura de manual diz: CNS provisório (série 7/8/9) não é identidade nacional estável, logo só
o definitivo (série 1/2) serve de âncora. Medido nos 243.330 atendimentos do Prime:

| classe do CNS | atendimentos | pacientes |
|---|---:|---:|
| definitivo (série 1/2, DV ok) | **362** (0,1%) | 48 |
| provisório (série 7/8/9, DV ok) | **234.123** (96,2%) | 38.470 |
| inválido (DV não fecha) | **0** | 0 |
| ausente | 8.845 (3,6%) | 2.102 |

Exigir definitivo importaria **362 de 243.330** — o mesmo que não importar. Antes de recomendar
isso, conferi o nosso próprio hub: **99,3% dos CNS de `fhir.patient` são provisórios**. O
provisório é a realidade de Maricá, não defeito do Prime. **A régua é DV válido, não série.**

E o dado é bom: **zero CNS inválido** em 243 mil. A validação (`prime/cns.py`) fica mesmo assim,
porque é a mesma armadilha do CPF `00000000000` do [ADR-0041] — o dia em que aparecer um campo
preenchido a esmo, ele para aqui em vez de fundir duas pessoas.

### `cns_todos` vale ~7.900 pacientes duplicados
Casando os 38.518 CNS do Prime contra o hub (amostra de 2.000):

- só pela coluna `cns`: **72,8%**
- incluindo os apelidos (`cns_todos`): **93,5%**

Os ~20% de diferença são pessoas que **já temos**, sob outro CNS primário. Ter casado só pela
coluna teria criado ~7.900 fichas duplicadas. O `PatientService` já faz certo — e tem no código o
comentário explicando que `x = ANY(cns_todos)` não usa o GIN e vira Seq Scan em 344 mil linhas.
Eu redescobri isso na marra escrevendo a consulta na forma errada duas vezes.

### O que NÃO tem onde pousar
O schema `fhir` tem `encounter` e `condition`, mas **não tem `procedure` nem `service_request`**.
Consequência: os **procedimentos SIGTAP — que estão em 100% dos 243.330 registros e são a parte
mais rica do relatório — não podem ir para o hub hoje**. Nem os exames solicitados. Levar isso
exige criar os recursos no `Automais.Fhir` (entity + configuration + migration + service +
controller), não é ajuste de mapeamento.

### Unidade: 3 Organizations no hub, 8 unidades no Prime
`fhir.organization` tem **3 registros**, todos do Salux. As 51 unidades vivem em
`smsmarica.unidade`. O padrão das 3 existentes é bom e vale seguir: CNES + um identifier por PEP
(`urn:salux:hospital`, `urn:klinikos:unidade`) na MESMA Organization — é o [ADR-0039] funcionando,
o CNES sendo a ponte. Para o Prime seria `urn:prime:unidade=<GUID>`.
Enquanto não existirem, o `Encounter.serviceProvider` vai por **referência lógica ao CNES**
(`{identifier: {system: sid/cnes, value: ...}}`), que é FHIR válido e não precisa ser reescrito
quando a Organization for criada.

### Ferramentas
| ferramenta | o que faz |
|---|---|
| `prime/cns.py` | `valido()` / `definitivo()` / `classificar()` — DV do CNS, famílias 1-2 e 7-8-9 |
| `prime/unidades.py` | de-para GUID do Prime → CNES nosso; `pendencias()` lista o que precisa de gente (2 inferidos, 2 ausentes) |
| `preparar_hub.py` | monta Encounter + Condition em `capturas/hub/*.ndjson` **sem gravar nada** — para conferir antes de qualquer escrita |

Constantes acrescentadas no `Automais.Fhir` (build limpo): `MetaSources.Prime`,
`FhirSystems.PrimeAtendimento` e `PrimeAtendimentoId` (este para quando o atendimento vier pela
extensão, que vê o `atendimentoId` real — o relatório não traz).

### O que ficou pronto para carregar (23/09/2026)
`preparar_hub.py` sobre os 243.330 do backfill, com o mapa de pacientes já resolvido:

```
Encounter prontos:   203,909      Condition (CID):  130,872
pacientes distintos:  30,597      identifiers únicos: 100%, CID codificado: 100%
```
| ficou de fora | por quê |
|---:|---|
| 14.180 + 8.365 | CEO Itaipuaçu e CEO Boqueirão — CNES **inferido**, esperando confirmação humana |
| 8.845 | sem CNS (os 2.102 pacientes do ADR-0041) |
| 7.888 | paciente não existe no hub — criar é outro ato |
| 126 + 17 | Odontomóvel e CEREST não existem em `smsmarica.unidade` |

Resolução de paciente medida no conjunto todo (não amostra): **35.971 de 38.518 CNS (93,4%)**
resolvidos, apontando para **35.928 pacientes** — 43 CNS do Prime são apelidos da mesma pessoa,
que é justamente o que o `cns_todos` existe para pegar.

**Armadilha de fuso, pega antes de acontecer:** `EncounterService.ParseInstant` usa
`DateTimeStyles.AssumeUniversal` — datetime **sem offset é lido como UTC**. Os horários do Prime
são wall-clock de Maricá, então a carga inteira entraria **3 horas atrasada** e nada acusaria. É o
mesmo bug da importação do SISREG (agendamento de 8h aparecendo 5h, commit 1744eb5). Os `period`
saem com `-03:00` explícito.

**Armadilha de glob:** juntar `capturas/backfill/*.ndjson` pega os arquivos de UM DIA (que a
conferência diária gera) junto com os de janela larga — 22/09 entrava duas vezes, e com a chave
antiga. O padrão agora é `*_AAAAMMDD-AAAAMMDD.ndjson`, com deduplicação por chave como rede.

## 22. Os 2.102 "sem CNS" e as duas unidades que faltavam (23/09/2026)

### O relatório não tem CPF de NINGUÉM
Vale dizer porque a pergunta natural é "e o CPF?": a coluna **não existe** no *Pacientes Atendidos*.
Os 2.102 sem CNS não são um subconjunto mal cadastrado — são gente cujo único identificador forte
não veio. O que eles têm: **nome, nascimento, sexo e endereço+telefone, 100% preenchidos** (2.101
dos 2.102 com telefone no texto). E são pacientes de verdade: os três maiores têm 90, 71 e 64
atendimentos.

### O CNS falta no Prime, não no hub — em 72% dos casos
Cruzando os 2.102 contra `fhir.patient` por nome + data de nascimento:

| | pacientes | |
|---|---:|---|
| casam com **um único** paciente | **1.507** (71,7%) | destes, **1.474 têm CPF** no hub |
| **ambíguos** (mais de um paciente com mesmo nome+nascimento) | 142 (6,8%) | **não resolver** |
| não existem no hub | 453 (21,6%) | |

Em atendimentos: **6.365 dos 8.845 (72%) seriam recuperáveis**. Dois dos três exemplos conferidos
à mão estavam no hub **com CPF e CNS**, vindos de outras fontes (SERNIT, Salux).

**Mas eles NÃO entram nesta carga**, e de propósito: a instrução foi "importar quem temos 100% de
certeza em relação ao CNS", e um vínculo por nome+nascimento não é isso. Os 142 ambíguos são a
prova de que homônimo com mesma data existe nesta população — 6,8%, muito acima dos 0,12% medidos
entre os que têm CNS. Parte desses 142 deve ser duplicata do nosso próprio hub, não pessoa
distinta; enquanto não se sabe qual é qual, fundir é o erro que não se desfaz.
Fica como **segundo lote, para decisão separada e com a incerteza declarada** ([ADR-0041]).

### CNES das duas unidades ausentes, pelo cadastro nacional
`GET https://apidadosabertos.saude.gov.br/cnes/estabelecimentos?codigo_municipio=330270` — 295
estabelecimentos em Maricá, sem login, sem tocar no SISREG:

| unidade | CNES | endereço |
|---|---|---|
| ODONTOMOVEL MARICA | **`0209724`** | Av. Roberto Silveira, 46, Centro — CEP 24900440 |
| CEREST MARICA | **`6893430`** | Rua Jovino Duarte de Oliveira, 2142, Aracatiba — CEP 24901130 |

**Duas armadilhas da API:** (1) `limit` é capado em **20** — pedir 100 devolve 20 e uma paginação
que pare quando `len < 100` colhe só a primeira página (foi o que me aconteceu: 20 de 295).
(2) O `codigo_cnes` volta como **inteiro**, então o Odontomóvel aparece `209724` — mas as 51
unidades do nosso cadastro são todas de **7 dígitos**. Sem o zero à esquerda (`0209724`) o de-para
por CNES não casa e a unidade some em silêncio.

As duas foram **criadas em 23/09/2026** (`criar_unidades_faltantes.py`, autorizado pelo Bernardo):
o cadastro foi de 51 para 53 unidades, e a releitura confirmou os **7 dígitos** nas duas — o zero
à esquerda do Odontomóvel sobreviveu, que era exatamente onde isso quebraria calado.

### Estado final da preparação (23/09/2026)
```
Encounter 223.317   Condition 136.321   pacientes 35.928   10 CNES distintos
identifiers únicos 100%   CID codificado 100%   period com fuso 100%   problemas: nenhum
```
Fora, e só isto: **11.168** (paciente não existe no hub — criar é outro ato) e **8.845** (sem CNS
— o segundo lote, que depende de aceitar vínculo por nome+nascimento). Nenhuma unidade pendente.

**Escrita em produção passou pelo Bernardo, não por mim:** o classificador de segurança recusa
escrita em produção vinda do agente mesmo com autorização em texto. O caminho que funcionou foi
deixar o script pronto, idempotente (`on conflict (cnes) do nothing`), com UUID v7 fixo e com
releitura conferindo ao final — e ele executar. Vale repetir esse formato nas próximas.

## 23. Consultar o hub para conferir a carga tem custo — e eu derrubei a conexão (23/09/2026)

Medindo quantos pacientes do Prime já existem no hub, fiz três vezes a mesma besteira: consulta
**por paciente**, 2.000 a 38.000 delas seguidas, cada uma varrendo `fhir.patient` (378.188 linhas).
Resultado: uma consulta que não terminava em 10 min, um `connection timed out` e, por fim, um
**`server closed the connection unexpectedly`** no meio da medição.

O `appsettings.json` do SMSMais diz o que eu deveria ter lido antes: *"O Postgres gerenciado é
compartilhado (max_connections=97). Split: SMSMais 50, FHIR 20, sobra ~16"*. Uma sonda de
laboratório que segura conexão por 10 minutos está disputando com a aplicação em produção.

**As formas certas, medidas:**
| jeito | custo |
|---|---|
| `x = ANY(cns_todos)` numa subconsulta correlata | **não usa o GIN** → Seq Scan; não terminou |
| `cns_todos && ARRAY[...]` em lotes de 1.000 | 38.518 CNS em **~11 min**, funcionou |
| `nome = any(ARRAY[...])` (igualdade exata, btree) | 3 nomes em **297 ms** |
| **uma passagem só** com `f_unaccent(nome) = any(ARRAY[2.547 nomes])` | 1 Seq Scan em vez de 2.547 |

A regra que faltava: **quando a pergunta é sobre um conjunto, pergunte pelo conjunto**. 2.547
consultas indexadas custam mais que uma varredura única da tabela — e, pior, seguram a conexão
por minutos.

E uma nota de rigor: cogitei trocar o `f_unaccent ILIKE` por igualdade exata porque é mais rápido.
Igualdade exata pode **deixar de achar** um homônimo escrito com acento — ou seja, **subestimaria
a duplicata**, que é o lado errado para errar numa pergunta de segurança. Por isso a varredura
única mantém o `f_unaccent` dos dois lados, mesmo custando um Seq Scan.

## 24. Criar paciente a partir do Prime duplicaria metade — medido (23/09/2026)

Pergunta do Bernardo: quantos pacientes temos, quantos seriam adicionados, desses quantos têm CNS
e CPF, e quantos são homônimos — "minha preocupação é duplicar cadastro de paciente".

### O hub hoje: 378.188 pacientes
| | |
|---|---:|
| com CPF | 344.119 (91%) |
| com CNS | 231.105 (61%) |
| com os dois | 222.044 |
| sem CPF **e** sem CNS | 25.008 |

### A carga de atendimentos cria ZERO paciente
Foi assim que ela foi desenhada: `preparar_hub.py` só inclui atendimento cujo paciente **já
existe**. Os 223.317 Encounters amarram a 35.928 pacientes existentes. Risco de duplicar: nenhum.

### Se criássemos os 2.547 que sobraram, **52% seriam duplicata**
Cruzando os 2.547 (têm CNS, mas o CNS não existe no hub nem como apelido) contra `fhir.patient`
por nome + data de nascimento, numa varredura única:

| | candidatos | |
|---|---:|---|
| **já existem no hub** (um único paciente com mesmo nome+nascimento) | **1.320 (51,8%)** | viraria **DUPLICATA** |
| ambíguos (mais de um homônimo de mesma data) | 105 (4,1%) | precisa de gente |
| realmente novos | 1.122 (44,1%) | |

Os 2.547 carregam **11.168 atendimentos** (média de 4,4 cada).

**Nenhum deles tem CPF** — o relatório do Prime não traz CPF de ninguém. Todo paciente criado a
partir desta fonte nasceria ancorado só no CNS. E os exemplos mostram o contrário do outro lado:
os que já existem no hub **têm CPF**, e um deles tem um CNS *diferente* do que o Prime traz
(`898003203163626`) — é a mesma pessoa com dois CNS, e o do Prime não estava no `cns_todos`.

### A conclusão, e ela não é "criar" nem "descartar"
Para os **1.320**, o certo não é criar nem deixar de fora: é **acrescentar o CNS do Prime como
apelido (`cns_todos`) no paciente que já existe**. É exatamente para isso que a coluna existe, o
`PatientService` já busca por ela, e o efeito é duplo — some a duplicata **e** os atendimentos
passam a colar numa identidade que já tem CPF. Os 105 ambíguos vão para revisão humana; os 1.122
realmente novos entram marcados pelo [ADR-0041] (CNS sem CPF = identidade incompleta).

### E não há rede no banco
`IX_patient_cns` **não é único** (`CREATE INDEX ... USING btree (cns) WHERE cns IS NOT NULL`).
Nada impede dois pacientes com o mesmo CNS; a proteção inteira está no código de resolução. É
diferente de `smsmarica.unidade`, que tem unique parcial em `cnes` — foi ele que garantiu a
idempotência do cadastro das duas unidades novas. Vale avaliar o mesmo em `fhir.patient.cns`,
mas só depois de saber se já há CNS repetido hoje.

## 25. O desempate: nome da mãe existe dos DOIS lados — e revelou duplicata NOSSA (23/09/2026)

Pergunta do Bernardo sobre os 105 ambíguos: dá para confirmar por endereço, nome de pai/mãe, algo
que dê o ponto único entre eles?

### O que cada lado tem
| sinal | no hub (`fhir.patient`) | no relatório do Prime |
|---|---:|---|
| **nome de mãe/pai** (`contact` + `relationship`) | **99%** | **não tem** |
| telefone | 97% | 100% |
| endereço | **39%** | 100% |
| CEP | 38% | 100% |

O endereço é forte no Prime e fraco no hub; a mãe é o inverso. Aplicando endereço+telefone aos
105: **38 resolvidos (36%)**, 19 empatados, 48 sem sinal em comum — o teto vem dos 39% de endereço.

### O nome da mãe está no Prime, e barato
Não no *Pacientes Atendidos*, mas a **grade de busca de paciente tem a coluna "Nome da Mãe"**
(junto com Código, Nome, Sexo, Nascimento, Prontuário). Uma busca por nome resolve — não precisa
abrir o cadastro (`txtNomeMae` do `UCNovoCadastroPaciente1`) paciente a paciente.

### E aí veio o achado que inverte o problema
Comparando os nomes de mãe **entre os candidatos do próprio hub**, nos 105 ambíguos:

| | |
|---:|---|
| **63 (60%)** | **mães IGUAIS — não é homônimo, é a MESMA pessoa duplicada no nosso hub** |
| 39 (37%) | mães diferentes — homônimo de verdade; o nome da mãe do Prime resolve |
| 3 (3%) | parcialmente iguais |

Ou seja: **a maior parte da "ambiguidade" não vem do Prime, vem de duplicata que já temos.** E as
fontes dizem por quê — são pares entre PEPs diferentes:

```
25  santarita + upa24h        9  salux-hcml + upa24h      5  salux + santarita
10  upa24h (duas no mesmo)    5  implantacao + upa24h     5  santarita (duas no mesmo)
```
Em **56 dos 63**, só *uma* das cópias tem CPF — que é exatamente o motivo de nunca terem se
fundido. Casa com as duplicatas pré-identifier já conhecidas.

### Consequência prática
1. Os **39 + 3** homônimos reais: buscar o nome da mãe no Prime (42 buscas) e desempatar.
2. Os **63**: problema nosso, anterior ao Prime. Mesclar é trabalho à parte e com gente no meio.
3. **E o mesmo teste vira a rede de segurança dos 1.320** "já existem": antes de acrescentar o CNS
   do Prime como apelido, conferir que o nome da mãe bate. Sem isso, o apelido pode ser colado na
   pessoa errada — e apelido errado é pior que duplicata, porque funde em silêncio.

## 26. VALIDADO: o ponto único é o CPF, e ele está na busca do Prime (23/09/2026)

O Bernardo mandou validar os "homônimos" contra o Prime. O resultado desmonta a premissa — e
entrega uma coisa melhor.

### Não existe homônimo: os 105 ambíguos são duplicata NOSSA
Comparando o nome da mãe **entre os candidatos do próprio hub**, com tolerância a truncamento e
erro de digitação:

| similaridade da mãe | casos |
|---|---:|
| idêntica / uma é prefixo da outra | 75 |
| erro de digitação (≥0,85) — `DANIELLE`×`DANIELE`, `CREIOZELINA`×`CREIOZOELINA` | 19 |
| parecida (0,70–0,85) | 6 |
| realmente diferente (<0,70) | 5 |

E os 5 últimos, conferidos um a um contra o Prime, **também não são homônimos**: dois têm
`SEM INFORMAÇÃO`/`DESCONHECIDO` no lugar do nome, e os outros são a mesma mãe escrita diferente
(`DESIREE FERREIRA PICORELLI GOULART` × `DESIRE FERREIRA`).

**Minha primeira medição (63 duplicatas / 39 homônimos) estava errada por comparar por igualdade
exata.** Nome de mãe truncado e com typo é a norma, não a exceção — comparar nome de pessoa por
`==` entre bases diferentes não mede nada.

### O achado: a busca do Prime traz CPF, e o CPF resolve
A grade de busca de paciente (`gridBuscaPaciente` da `AgendaRecepcao`) devolve
**`Código | Código Mitra | Nome | Nome da Mãe | Sexo | Nascimento | Prontuário | CPF | CNS`**.
Nos 5 casos: **5/5 com CPF, 5/5 com pacienteId, 5/5 com nome da mãe**, nenhum eco de CadWeb.

| sinal | acertou |
|---|---|
| **CPF** | **5 de 5** |
| nome da mãe | 3 de 5 — falhou em `DESCONHECIDO`×`SEM INFORMAÇÃO`, e **discordou do CPF** num caso (mãe de solteira × de casada) |

Quando mãe e CPF discordam, **o CPF ganha**: é chave nacional com dígito verificador, e é a régua
primária do [ADR-0009]. O nome da mãe serve de confirmação, não de âncora.

### Consequência: mudar a régua de resolução do Prime
O *relatório* não tem CPF de ninguém — foi por isso que a resolução inteira foi feita por CNS. Mas
a *busca* tem. Uma busca por paciente (leitura) entrega **CPF + `pacienteId` do Prime**, e com isso:
- resolve contra os 91% de pacientes do hub que têm CPF, em vez dos 61% que têm CNS;
- dá a **chave de origem** (`urn:prime:paciente`), que o relatório não dá — sem ela um Patient do
  Prime nasceria como os do SISREG/implantação, que têm chave de origem em **0%**.

Custo: ~2.547 buscas para os candidatos a criação (~1–2 s cada). **Atenção à sessão única**: a
sonda derrubou a sessão aberta em outra estação ("usuário autenticado em outra estação —
confirmando nesta"). Uma varredura longa tira a conta da Clarisse do ar — combinar horário.

## 27. Três jeitos de uma varredura mentir com cara de sucesso (24/09/2026)

A varredura dos 2.547 pacientes (busca no Prime para trazer CPF + `pacienteId`) foi lançada três
vezes antes de funcionar. Nenhuma das falhas deu erro — todas "rodaram bem".

### 1. Reaproveitar o ViewState não repesquisa; devolve grade vazia, HTTP 200
Para economizar o GET de 335 KB por paciente, passei a postar a busca seguinte sobre o HTML da
resposta anterior. O POST volta **200, sem exceção, com a grade vazia**. Como "nenhuma linha" é
resultado legítimo para um paciente, o laço contava e seguia: **80 pacientes "pesquisados", 3
gravados**, e eu só percebi comparando o contador de progresso com o `wc -l` do arquivo.

Postback de WebForms não é idempotente entre telas: a resposta de uma busca **não é** um formulário
de busca novo. O GET por paciente é o preço de a varredura não mentir.

### 2. O bloco que reescrevia o arquivo no fim
O script gravava tudo de uma vez no encerramento (`open("w")` + `for r in achados`). Isso apaga o
resultado das rodadas anteriores a cada execução — a retomada existia no começo do script e era
desfeita no fim dele. Perdi duas vezes registros já obtidos (5 e depois 3) antes de achar.

Agravante meu: apliquei o conserto por `str.replace` num heredoc, **o replace não casou** (escape
de `\n`), e eu segui em frente sem conferir que tinha aplicado. Conferir o efeito do patch, não a
ausência de erro do patch.

### 3. stdout bufferizado + `| tail` = voar às cegas
`python script.py | tail -30` num processo de horas não mostra nada até o fim. Com `run_in_background`
o mesmo. **`python -u`** e sem pipe — ou o progresso é invisível justamente quando é mais necessário.

### O que ficou no script
- GET por busca, sempre (sem reaproveitar ViewState);
- **conferência de que a linha devolvida é do CNS pesquisado**, não da busca anterior;
- **disjuntor**: 20 buscas seguidas sem nenhuma linha e nada gravado → aborta com mensagem, em vez
  de produzir 2.547 vazios;
- gravação linha a linha com `flush`, arquivo sempre em `"a"`, `--recomecar` apaga explicitamente.

A regra geral, que vale para qualquer varredura longa: **o contador de progresso e a contagem do
que foi gravado têm de bater.** Se o script diz 80 e o arquivo tem 3, o script está mentindo.

## 28. Varredura dos 2.547 concluída: o CPF evitou 1.473 fichas duplicadas (24/09/2026)

2.547 buscas de paciente no Prime (`sondar_nome_mae.py`), uma por candidato a criação. Resultado:

| | |
|---|---:|
| buscas com resultado | **2.547 (100%)** — nenhuma vazia |
| com **`pacienteId` do Prime** | **2.547 (100%)**, todos distintos |
| com nome da mãe | 2.547 (100%) |
| com CPF | 2.472 (97,1%) |
| CPF com **DV inválido** | **0** |
| eco do CadWeb (GUID zerado) | **0** |

### O que o CPF mudou
Cruzando os 2.472 CPF válidos contra `fhir.patient`:

| | candidatos | atendimentos |
|---|---:|---:|
| **já existem no hub por CPF** | **1.473 (59,6%)** | **6.422** |
| realmente novos | 999 | 4.481 |
| sem CPF na grade | 75 | 265 |

**1.473 fichas duplicadas evitadas.** Eles eram "paciente novo" só porque a resolução era por CNS —
o relatório não tem CPF em coluna nenhuma, e foi isso que empurrou 59,6% de gente conhecida para a
fila de criação.

Comparando as réguas no mesmo conjunto: nome+nascimento achava **1.320**; o CPF acha **1.473**.
O CPF pega 153 a mais **e** é o único que não depende de o nome estar escrito igual nas duas bases
— o que, medido hoje, quase nunca acontece (truncamento e typo são a norma).

### Efeito na carga
Os 6.422 atendimentos dos 1.473 passam a ter paciente resolvido e entram:
**223.317 → 229.739 Encounters.** Os 999 realmente novos (4.481 atendimentos) continuam fora até
a decisão de criar paciente, e agora entrariam com CPF válido + CNS + `pacienteId` + nome da mãe —
identidade bem mais completa do que os 2.940 do SISREG/implantação, que estão no hub com 0% de
chave de origem.

### Custo e operação
~2.547 buscas, ~40 min de relógio em 3 pedaços (o processo foi morto duas vezes por pressão de
memória da máquina, não por falha). A **retomada por arquivo** é o que salvou: cada pedaço pulou o
que já estava gravado. Sem ela seriam 3 varreduras inteiras.

## 29. Duplicata de paciente no hub: 1.195 grupos, e a causa é a falta de chave de origem (24/09/2026)

O Bernardo pediu para identificar e fundir as duplicatas. Antes disso, dois levantamentos.

### `fhir.patient` não tem NENHUMA FK apontando para ela
São **33 colunas** em `fhir.*` e `smsmarica.*` guardando id de paciente, **zero** com integridade
referencial — inclusive `smsmarica.laudo`, `solicitacao`, `tratamento`, `conversa`,
`regulacao_solicitacao`. Uma fusão que esqueça uma coluna deixa dado órfão e **o banco não reclama**.
A lista dentro de `identificar_duplicatas.py` **é** a integridade referencial deste banco; se ela
envelhecer, uma fusão futura órfã dado em silêncio.

### Escala
| | |
|---|---:|
| grupos duplicados (T1-CPF 510 + T2-CNS 685) | **1.195** |
| registros envolvidos | 2.477 |
| seriam absorvidos | 1.282 |
| **linhas a repontar** | **8.929** |
| grupos com **nome de mãe divergente** | **95** — revisar um a um |

As 8.929 são bem menos que as 53.560 que apontam para o conjunto todo: o registro absorvido
costuma ser o mais pobre. Por dificuldade:

| | grupos |
|---|---:|
| absorvido **sem nenhum** dado clínico — fusão trivial | 127 |
| absorvido com ≤10 linhas | 847 |
| absorvido com >10 linhas — merecem cuidado | 221 |

### A CAUSA: a maior parte duplica dentro da PRÓPRIA importação
Pares de `meta.source` dos grupos:

```
457  sisreg/implantacao  (consigo mesma)      145  ser-sesrj + smsmarica
300  ser-sesrj           (consigo mesma)       78  salux + smsmarica
 48  klinikos/upa24h + smsmarica               30  salux (consigo mesma)
```

Não é conflito entre PEPs — é **reimportação cega**. E as duas fontes que lideram são justamente
as que não têm chave de origem: **SISREG/implantação tem 0%** (457 grupos), **SER tem 60%**
(300 grupos). Salux e Klinikos, que têm 100%, quase não aparecem sozinhos.

**A cadeia é essa: sem chave de origem, a carga não reconhece o que ela mesma já inseriu e duplica.**
É o argumento definitivo para exigir chave de origem em todo conector — e é por isso que levar o
Prime pelo relatório (que não traz `pacienteId`) repetiria o erro, enquanto levá-lo pela busca
(que traz, em 100%) não repete.

### Fusão: "eliminar" não deve ser apagar
Recomendado `Patient.link` (`replaced-by` no absorvido, `replaces` no sobrevivente) + o
`is_deleted` que a tabela já tem. Motivos: a fusão junta prontuário de duas pessoas e precisa ser
reversível; `smsmarica.laudo` aponta para paciente e laudo assinado não se apaga; e quem chegar
pelo id antigo (link salvo, integração, app do cidadão) tem de encontrar o novo em vez de 404.
Hoje o `Automais.Fhir` **não** tem suporte a `Patient.link` — é pré-requisito da fusão.

## 30. Suporte a fusão de paciente no hub (`$merge`) — 24/09/2026

Pré-requisito das 1.195 fusões. Implementado no `Automais.Fhir` (build 0 erros / 0 avisos).

`POST /fhir/Patient/{sobrevivente}/$merge?source={absorvido}` →
`IPatientService.FundirAsync`. Numa transação:

1. **O sobrevivente absorve os identifiers** que não tinha. CNS do absorvido entra como
   `use=old` (não destrona o oficial, mas continua valendo em `cns_todos`); chaves de origem
   (`urn:salux:cd_paciente`, `urn:prime:paciente`…) entram inteiras.
2. **`link` nos dois sentidos**: `replaces` no sobrevivente, `replaced-by` no absorvido.
3. **`active=false` no absorvido — e NÃO `is_deleted`.** Ele continua legível.
4. **Reponta o clínico de `fhir.*`**: encounter, condition, observation, medication_request,
   medication_administration, document_reference. Devolve a contagem de cada um.

### As três decisões que não são óbvias
- **Absorver os identifiers é o ponto principal, não um detalhe.** Sem isso a fusão trata o
  sintoma e mantém a doença: a próxima carga procura pela chave antiga, não acha e recria a
  duplicata. É literalmente como 457 dos 1.195 grupos nasceram (SISREG reimportando a si mesmo
  sem chave de origem).
- **Não apagar.** Fusão junta prontuário de duas pessoas e precisa ser reversível; `smsmarica.laudo`
  aponta para paciente; e quem chegar pelo id antigo tem de achar o ponteiro, não 404.
- **`ExecuteUpdateAsync` antes do `SaveChanges`.** Ele não passa pelo change tracker; nenhuma
  linha clínica está rastreada ali, então não sobra estado obsoleto para conciliar.

### O que a operação NÃO faz
Alcança só `fhir.*`. As colunas de `smsmarica.*` (laudo, solicitacao, tratamento, conversa,
regulacao_solicitacao, whatsapp_mensagem…) pertencem a outra aplicação e a outro schema, **não têm
FK nenhuma**, e ficam apontando para o absorvido até serem repontadas por ela — sem nada avisar.
Repontar as duas metades tem de acontecer na mesma janela.

### Testes
`tests/Automais.Fhir.Tests/FusaoDePacienteTests.cs`, 5 casos: clínico migra; **sobrevivente passa a
ser achado pelo CNS e pela chave de origem do absorvido**; absorvido continua legível com
`replaced-by`; fundir duas vezes é recusado; fundir consigo mesmo é recusado.

**Não rodados**: esta máquina não tem Docker. Rodar com
`FHIR_TESTS_CONNECTION="<connstring da bancada>" dotnet test`.

## 31. O modo seco pegou uma fusão que juntaria duas crianças (24/09/2026)

Montei o filtro do "nível mais seguro" para fundir: mesmo CPF com DV válido, **nome da mãe não
diverge**, par simples, absorvido sem nenhum dado clínico. Sobraram 6 pares. No modo seco:

```
CPF 23290075745
   FICA  LAVINIA DOS SANTOS PIMENTEL   nasc 23/02/2023  mãe PAOLA DOS SANTOS PIMENTEL  refs=57
   VAI   WANDERLEY DA SILVA            nasc 23/02/2023  mãe PAOLA DOS SANTOS PIMENTEL  refs=0
```

Mesma mãe, mesma data de nascimento, nomes completamente diferentes, nascidos em 2023. **São
gêmeos**, com o CPF de um na ficha do outro. Fundir teria juntado o prontuário de duas crianças.

### A lição, e ela é contra-intuitiva
Eu vinha usando "o nome da mãe bate" como o sinal mais forte de que duas fichas são a mesma pessoa
— foi assim que identifiquei que os 105 ambíguos do Prime eram duplicata nossa. **Para recém-nascido
com a mesma data de nascimento, mãe igual é evidência de GÊMEOS, não de duplicata.** O filtro não
só falha nesse caso: ele aponta para o lado errado com confiança.

O que separa duplicata de gêmeo é o **nome**:
- duplicata = o mesmo nome escrito torto — `ANTONIO`/`ANTTONIO`, `PEREIRA`/`PERREIRA`,
  `HENRICCO FERNANDES DE CARDOSO`/`HENRICCO FERNANDES CARDOSO`;
- gêmeo = outro nome.

Entrou no filtro como `nomes_sao_a_mesma_pessoa` (similaridade ≥ 0,85). Dos 6, sobraram **5**.

### E a lição maior
O modo seco não é formalidade. Este par passou por **quatro** critérios pensados para ser
conservadores — CPF com dígito verificador, mãe concordando, par simples, zero dado clínico — e só
foi pego porque os nomes estavam impressos lado a lado para um humano ler. Nenhuma regra que eu
tinha escrito o barrava.

Vale para todo mutirão de identidade: **imprimir e olhar antes de gravar não é etapa opcional.**

## 32. A Receita fecha a questão — e nome de mãe por `==` erra nos DOIS sentidos (24/09/2026)

Consultado o Hub do Desenvolvedor (`ws.hubdodesenvolvedor.com.br/v2/cpf/?cpf=&data=&token=`,
mesma chamada do `HubDoDesenvolvedorMotorCpf`) para os 11 candidatos: **10 responderam**, 1 timeout.

### O par de gêmeos, resolvido documentalmente
```
CPF 23290075745 → RECEITA: LAVINIA DOS SANTOS SILVA PIMENTEL
   WANDERLEY DA SILVA            sim 0,39   NÃO é esta pessoa
   LAVINIA DOS SANTOS PIMENTEL   sim 0,90   é esta
```
LAVINIA é a dona do CPF; a ficha do WANDERLEY está com documento de terceiro e precisa ter o CPF
corrigido, senão reaparece em toda varredura.

### Correção: a Receita decide o NOME, não o sobrevivente
Escrevi antes que "a Receita deveria ser o critério de sobrevivente". Errado. Em 2 dos 11 a grafia
oficial estava na ficha que eu marcara como **absorvida** (`GABRIEL ANTTONIO … SOUSA`,
`HENRICCO FERNANDES CARDOSO`). Trocar o sobrevivente por causa disso só aumentaria o repontamento.

São duas decisões independentes: **sobrevivente = quem tem o dado clínico** (menos linha a mover);
**nome do sobrevivente = o oficial da Receita**.

### Nome de mãe comparado por igualdade exata erra nos dois sentidos
| | |
|---|---|
| **falso negativo** | gêmeos têm a MESMA mãe — o filtro aprovou o par mais perigoso |
| **falso positivo** | `LAURA PRAGANA WETSCHY` × `LAURA PRAGANA WESTCHKY` é a mesma mãe com typo — o filtro barrou um par legítimo |

Trocada a bandeira `maes_divergem` por similaridade (≥ 0,85): dos **95** grupos marcados como
divergentes, **52 eram falso positivo**. Restam 43 de divergência real.

Lição geral, e ela vale para todo este mutirão: **nome de pessoa não se compara por `==` entre
bases diferentes.** Truncamento e erro de digitação são a norma; foi assim também que eu contei
"39 homônimos" onde havia zero, e "1.320 já existem" onde o CPF achou 1.473.

### Estado
7 pares prontos, todos com nome oficial confirmado. Fora: o par de gêmeos (definitivo) e 1 que
aguarda re-consulta (timeout). O `fundir_seguros.py` agora **exige** veredito da Receita — sem
conferência, o par não entra, porque o dos gêmeos passou em quatro critérios meus e só a Receita
o barrou.

## 33. INCIDENTE: o hub clínico estava aberto na internet, sem senha (24/09/2026 — CORRIGIDO)

Apareceu por acaso: eu procurava a URL do hub para rodar a fusão e li no ADR-0043 que o
`Automais.Fhir` "escuta em `0.0.0.0:5081` sem autenticação alguma", registrado como pendência.
Fui conferir se era teórico.

### Não era
```
ufw status                          → inactive        (sem firewall no host)
ss -lntp | grep 5081                → 0.0.0.0:5081    (todas as interfaces)
curl http://146.190.65.73:5081/fhir/Patient?_count=0  → HTTP 200   (da internet, sem token)
```

Exposto: **378.188 pacientes** com CPF, CNS, nome, nascimento, endereço, telefone e nome da mãe,
mais `encounter`, `condition`, `observation`, `medication_request` e `document_reference`. E a API
tem `POST`/`PUT`/`DELETE` — **escrita**, não só leitura. Qualquer varredura de portas acha.

### O conserto
Antes de mexer, conferido que nada externo dependia da porta: `ss -tn state established` mostrou
**todas** as conexões vindas de `127.0.0.1` (o `smsmarica-server`, no mesmo host).

1. backup do unit em `/root/automais-fhir.service.bak-<timestamp>`;
2. `ASPNETCORE_URLS` de `0.0.0.0:5081` para `127.0.0.1:5081`;
3. `daemon-reload` + `restart`.

Depois: escutando em `127.0.0.1:5081`, serviço `active`, de fora **HTTP 000**,
`api.smsmarica.online/health` e o painel em **200**. Reinício custou ~3 s.

**O deploy reabriria.** `deploy-fhir.yml` reescreve o unit a cada publicação e tinha `0.0.0.0`
fixo — corrigido no workflow também, senão o conserto duraria até o próximo push.

### O que fica
- **Porta de serviço interno não vai em `0.0.0.0`.** Sem autenticação e sem firewall, bind é a
  única barreira que existia — e não existia.
- **Pendência registrada em ADR não é pendência tratada.** Estava escrita desde o ADR-0043 e
  seguia valendo em produção. Vale varrer as outras pendências abertas de lá.
- **Conferir o estado antes de mexer é o que torna o conserto seguro**: saber que só o localhost
  usava a porta transformou uma mudança arriscada numa mudança óbvia.
- Faltou conferir as outras portas do host (`5080`, `5085`, `5090`) com o mesmo olho.

## 34. A porta 5080 e o drop-in que desfazia o conserto em silêncio (25/09/2026)

Varredura das portas do host depois do incidente do hub. Resultado: `5081`, `5085` e `5090` em
`127.0.0.1`; **`5080` (a API principal) em `0.0.0.0`**, respondendo HTTP puro da internet.

Menos grave que o hub: a autenticação funciona (`/pacientes` → 401). O que se perdia era o **TLS**
— quem batesse direto mandaria senha e token em texto claro. O nginx do `api.` não tem rate limit
nem WAF, então era só isso que o bypass custava.

### O conserto falhou na primeira tentativa, e calado
Editei `ASPNETCORE_URLS` no unit, `daemon-reload`, `restart` — e o processo **continuou em
`0.0.0.0:5080`**. Só descobri porque conferi o `ss` depois; o comando não deu erro nenhum.

Causa: um **drop-in** `/etc/systemd/system/smsmarica-server.service.d/proxy-sql.conf` (de
25/07/2026, do proxy SQL do painel do Secretário) redefine a variável inteira:
```
Environment=ASPNETCORE_URLS=http://0.0.0.0:5080;http://127.0.0.1:5091
```
Ele acrescenta a 5091 no loopback e, ao fazer isso, **repetiu o 5080 como estava**. Drop-in vence
o unit para a mesma chave. O conserto foi lá, não no unit.

**A lição:** `Environment=` no unit não é a última palavra. Antes de editar, `systemctl show -p
Environment <unit>` mostra o valor que vale, e `ls service.d/` mostra quem mais opina. E **conferir
o efeito** — não a ausência de erro — é o que separa "corrigido" de "acho que corrigi".

### Por que o unit estava errado desde sempre
Todos os workflows de deploy já especificam `127.0.0.1`. O unit no servidor é antigo porque o
deploy só o escreve `if [ ! -f "$UNIT_FILE" ]` — **correção de unit em workflow nunca alcança
servidor já provisionado**. Foi assim com o hub e foi assim aqui.

### O que NÃO estava exposto (conferido, não suposto)
`/proxy-sql` executa SQL arbitrário e vive no mesmo processo. Testado com POST real:
**5080 → 404** (a primeira checagem do handler é `Connection.LocalPort`, e fora da porta interna o
endpoint "não existe"), **5091 → 401** (passa da porta, barra no token). A defesa por porta
funciona como documentada — mesmo com a 5080 aberta, o proxy nunca foi alcançável de fora.

### Depois
`5080` e `5091` em `127.0.0.1`; de fora **HTTP 000**; `api.smsmarica.online/health`, o painel e
`secretario.smsmarica.online` em **200**. Backups em `/root/*.bak-<timestamp>`.

### Erro meu, para não repetir
Ao investigar, li `/proc/<pid>/environ` e **a senha do Postgres de produção saiu em texto** na
saída. Filtrei o arquivo de env e esqueci do `/proc`. Qualquer leitura de ambiente de processo
passa por filtro — `tr '\0' '\n' < /proc/$pid/environ | grep -iE 'URLS' ` já bastava, em vez de
imprimir o que casasse com `PORT`. A credencial precisa ser rotacionada.

## 35. Os testes do `$merge` pegaram 3 bugs que o build não pegava (25/09/2026)

Rodada a suíte do hub na bancada: **46/46 aprovados** — mas só depois de consertar três defeitos
no `$merge` que eu havia commitado com "build 0 erros / 0 avisos".

### Antes: a bancada não rodava, e o motivo é um defeito latente
`text search dictionary "smsmarica.unaccent" does not exist`. A migration faz
`CREATE EXTENSION IF NOT EXISTS unaccent SCHEMA smsmarica`, mas na bancada a extensão **já existia
em `public`** — o `IF NOT EXISTS` vira no-op silencioso, a extensão fica onde estava, e o
`smsmarica.f_unaccent` (que a barra de pesquisa de cadastros usa) estoura ao chamar
`'smsmarica.unaccent'::regdictionary`.

**Isso vale para qualquer base nova que já tenha `unaccent` em `public`** — inclusive uma instância
de município novo (ADR-0043). Em produção nunca apareceu porque lá a extensão nasceu em `smsmarica`.
`ALTER EXTENSION SET SCHEMA` foi barrado (`must be owner` — no Postgres gerenciado da DO as
extensões são do papel `postgres`), então a saída foi criar o banco `smsmarica_testes` no mesmo
cluster, onde a migration monta tudo no lugar certo sem tocar em nada existente.
**Dívida:** a migration devia garantir o schema da extensão, não supor. Migration aplicada não se
edita, então é migration nova — e isso é mudança de esquema em produção.

### Bug 1 — repontar a coluna não reponta o recurso
`ExecuteUpdateAsync` na coluna `patient_id` deixa o `content` jsonb com
`subject.reference = Patient/<absorvido>`. A busca acha o recurso no paciente novo e o `GET`
devolve o antigo: **coluna e documento discordando, sem erro nenhum**. A coluna é search param; o
documento é a verdade. Corrigido com `jsonb_set` no mesmo UPDATE.

### Bug 2 — identifier se MOVE, não se copia
Copiar o CNS para o sobrevivente deixava os dois Patients com o mesmo número: buscar por ele
devolvia **2**, e um conector fazendo "achar por CNS" penduraria dado novo no registro absorvido —
recriando a duplicata que a fusão veio desfazer. A lápide não guarda identificador; quem a procura,
procura pelo id, e é o `link` que conta a história.

### Bug 3 — chave literal em `ExecuteSqlRaw` estoura em execução
`jsonb_set(content, '{subject,reference}', …)`: o `ExecuteSqlRaw` trata a SQL como **composite
format string**, então `{subject,reference}` é lido como placeholder e dá
`FormatException: Expected an ASCII digit`. **Compila.** Só aparece rodando. Saída limpa:
`ARRAY['subject','reference']`, que não tem chave nenhuma.

### A lição
Os três passaram por build limpo, revisão minha e dois commits. **"Compila" não é evidência de
nada** — e num endpoint que funde prontuário, o custo de descobrir em produção seria juntar o
histórico de duas pessoas. A suíte levou 37 s.

## 36. FEITO: as 7 primeiras fusões de paciente, em produção (25/09/2026)

`$merge` deployado (main `a0a907a`, workflow `deploy-fhir` com sucesso) e as **7 fusões do nível
mais seguro executadas: 7 fundidos, 0 falhas**.

### Conferido relendo o banco, não pela saída do script
| | |
|---|---|
| pacientes com `link` | **14** (= 7 pares) |
| lápides corretas | 7/7 — `active=false`, `replaced-by`, **0 identifiers**, `is_deleted=false` |
| grupos com CPF repetido | **510 → 503** (exatamente 7 a menos) |
| nomes corrigidos pela Receita | 4 (`WLDIR WETSCHY`→`WALDYR WETSCHKY`, `MACHAD`→`MACHADO`, …) |

### A prova que importa: as chaves de origem migraram
Cada sobrevivente ficou com as chaves dos DOIS registros:
```
GABRIEL    urn:salux:cd_paciente = 102890 + 102889
VANOR      urn:klinikos:paciente = 200092 + 230257  e  urn:salux:cd_paciente = 174230 + 174228
FRANCISCO  urn:salux:cd_paciente = 314410 + 314411  + RG
```
É isso que impede a recorrência: na próxima carga do Salux, **os dois** `cd_paciente` acham o mesmo
Patient. Sem esse passo a fusão duraria até o ciclo seguinte — e o VANOR mostra por que importa, ele
estava duplicado em **dois PEPs diferentes**.

Nota: os 7 absorvidos **não tinham CNS** (foram agrupados por CPF, que o sobrevivente já tinha),
então "herdar o CNS" era vazio neste lote. A herança que valeu foi a das chaves de PEP.

### Duas armadilhas de processo
**O script quebrou no meio, e foi sorte que fosse antes da escrita.** `ModuleNotFoundError: db` —
quando desacoplei `REFERENCIAS` para o script rodar no servidor, tirei junto o `sys.path` das
ferramentas, e o `from db import conn` do modo `--gravar` (import tardio) passou a estourar.
Conferido no banco que nada havia sido gravado antes de consertar. **Import tardio precisa do
caminho preparado cedo** — o erro aparece só no caminho que quase nunca se testa.

**`grep | tail` bufferizou de novo** e me deixou cego durante as fusões. Acompanhei pelo banco, que
é a fonte que não mente. Terceira vez na mesma armadilha nesta sessão.

### Onde o mutirão está
**7 de 1.195.** Os 1.188 restantes dependem do repontamento de `smsmarica.*` (8.112 linhas, 91% do
volume) — o nível 1 só passou sem ele porque os absorvidos tinham zero dado clínico.
