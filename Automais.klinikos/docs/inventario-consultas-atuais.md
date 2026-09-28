# Inventário: o que lemos hoje do Klinikos por SQL × onde isso sai pela web

> Escrito em 16/09/2026 a partir do código que roda hoje (`SMSMais.server` —
> `KlinikosImportacaoStrategy`; `SMSMarica.secretario.pwa` — `ConsultasUpa`) e do que foi
> **medido** contra `klinikosconde.smsmarica.online` neste laboratório (ver `APRENDIZADOS.md`,
> `sondagem-relatorios.md`, `mapa-endpoints.md`, `catalogo-relatorios.md`). Onde ainda não
> foi medido, está escrito "a medir".

## 0. Por que este documento existe

Hoje há **dois consumidores** dos bancos SQL Server do Klinikos, os dois pelo agente WSS
reverso + proxy SQL interno (ADR-0014/0023):

1. **Conector FHIR** (`KlinikosImportacaoStrategy`): alimenta o hub com Patient, Practitioner,
   Organization, Encounter, Condition, DocumentReference, MedicationRequest, Observation.
2. **Painel do Secretário** (`ConsultasUpa`): números ao vivo de UPA Maricá e Santa Rita.
   **O Conde não está no painel** desde que migrou do Salux para o Klinikos — as consultas
   Oracle (`ConsultasPainel`) morreram com o cutover.

A decisão do operador (16/09) é **desligar a leitura por banco** e trazer **Conde, UPA e Santa
Rita** pelo caminho "como usuário" (login web + relatórios/serviços), com requisições leves para
não onerar o sistema do fornecedor. Este inventário diz, item a item, o que precisa sair de onde.

## 1. As três instâncias

| Unidade | Instância web | `unid_codigo` | Base SQL hoje (slug) | Estado |
|---|---|---|---|---|
| HMCML / Conde | `klinikosconde.smsmarica.online` | **0005** | (Salux `salux-hcml`, morto para o painel) | **medida neste laboratório** |
| UPA 24h Maricá (CNES 7164440) | **a confirmar** (`klinikosupa…`?) | 0006 | `upa24h-marica-sqlserver` | agente WSS |
| UPA 24h Santa Rita | **a confirmar** | 0007 | `santarita-marica-sqlserver` | agente WSS |

Mesmo produto, mesmo build (`K.2024.09.2.1` no Conde — conferir nas outras). Tudo o que foi
medido no Conde deve valer nas UPAs **depois de confirmado lá** (o SER-RJ × SERNIT ensinou
que ids mudam entre builds). `par3`/`par1`=unidade nos relatórios é o `unid_codigo` da instância.

## 2. Conector FHIR — o que cada consulta SQL lê e a fonte web candidata

| # | Consulta SQL (hoje) | Colunas que o mapper usa | Fonte web candidata | Medido |
|---|---|---|---|---|
| 1 | `unidade` | código, nome, fantasia, sigla, **CNES**, telefone, e-mail | fixo por instância (1 linha por base); `Administracao/Tabelas/UnidHospitalar/*` | a medir (tela) |
| 2 | `profissional` (integral, sem rowversion) | código, nome, CPF, CNS, conselho, CBO, ativo | `Administracao/Tabelas/Profissional/Profissional.aspx` (grade RadGrid); rel. 788 *Profissionais sem CNS* (`rptView`, PDF) | a medir (grade exige postback) |
| 3 | `paciente` por `rv_atualizacao` | código, nome, CPF, CNS, nascimento, sexo, mãe, pai, telefones, e-mail, óbito, responsável, raça | **Cadastro**: rel. 390 *Cadastro no Estabelecimento de Saúde* (`ParamPaciente.aspx`), 21 *Prontuários Abertos*, 381 *Registros Abertos*, 488 *Origem Prontuário*; tela `Cadastro/Paciente/CadastroBasico.aspx` (por prontuário) | **em sondagem** (`sondar_relatorios.py`) |
| 4 | `Pronto_Atendimento` por rowversion (boletim) | spa_codigo, pac_codigo, unid, chegada, dt_boletim, nome social, CNS, forma de chegada, risco | **407** *Pacientes Registrados no Dia* → Nº Boletim, Prontuário, Paciente, Nasc/Idade, Clínica (XLS, 1 dia = 545 KB); **667** *Nominal por Classificação de Risco* → Nº Boletim, Paciente, Idade, Data/Hora Entrada, Clínica, Origem; 683 *Como Chegou* | **medido** (407, 667) |
| 5 | `UPA_Evolucao` (flag "teve atendimento", CID primário/secundário, tipo, texto) | spa, tipo, data, prof, CID | **815** *Atendimento Nominal por CID*; 484 *Diagnósticos por Clínica* (estatístico); 830 *Evoluções de Multiprofissionais*; 791 *Boletim Atendimento Médico PDF* (por competência+paciente, `RptViewCompetencia.aspx`, pesado: 504) | 484 medido (só totais); 815/830 em sondagem |
| 6 | `atendimento_ambulatorial` + `UPA_Atendimento_Medico` + `Tipo_Saida` (fechamento, desfecho, médico) | spa, data final, tipo de saída, prof | **65** *Tipo de Saída, Sexo e Faixa Etária* (estatístico); **834** *Encerrados por evasão*; 520 *Remoções por destino*; 527 *Óbito/Chegou Cadáver*; 66 *Tempo de Permanência* (estatístico) | 520/527/66 medidos (1 dia sem registro/só totais); 834 em sondagem |
| 7 | `UPA_Atendimento_Medico` narrativa (anamnese, exame físico, hipótese, conduta, observação) | texto livre por boletim | **791** *Boletim Atendimento Médico PDF* (`RptViewCompetencia.aspx?parRel=489&par1=MM/AAAA&par2=<paciente>`) — PDF por paciente/competência; tela `UPA/AtendimentoMedico.aspx` (por boletim) | 791 estourou (504) com par2 vazio — testar com paciente |
| 8 | `Item_Prescricao_Medicamento` + `Prescricao` | insumo, quantidade, unidade, via, frequência, duração, SOS | tela `UPA/PrescricaoReceita.aspx` (por boletim); rel. 816 *Justificativa de Antibiótico*; `Share/Prescricao/PlanoTerapeutico.aspx` | a medir — **não há relatório nominal de prescrição no menu do UPA** |
| 9 | `TB_CID` (catálogo, 14.242) | código, nome | `Administracao/Tabelas/Outras/Outras/CID.aspx` (grade) — ou **não buscar**: CID-10 é tabela pública, o hub já tem o nome | não precisa da web |
| 10 | `UPA_SinaisVitais` | PA, pulso, temperatura, FR, HGT, SpO2, peso | tela `Share/UPAShare/RegistrosEnfermagem.aspx` (271 campos, por boletim); rel. 843 *Escalas BRADEN/FUGULIN/MORSE* | a medir — sem relatório nominal no menu |
| 11 | `MIN_ACTIVE_ROWVERSION()` (ponteiro CDC) | — | **não existe na web**. Marca incremental = **data** (chegada/registro) + reconciliação dos últimos N dias | decisão de projeto |

**Leitura honesta da tabela:** boletim, chegada, CID, desfecho e classificação de risco **saem
por relatório** (XLS, 1 GET). Narrativa médica, prescrição e sinais vitais **não têm relatório
nominal no menu do UPA** — saem só por tela, boletim a boletim (postback com ViewState), o que
é caro para o fornecedor e lento para nós. Ver §5.

## 3. Painel do Secretário — cada número e a fonte web

| Indicador (contrato do painel) | SQL hoje | Fonte web candidata | Medido |
|---|---|---|---|
| **U1a** aguardando médico agora, por cor, T1/T2 | boletins 12h sem `atendamb_datainicio`, na `UPA_Fila` não cancelada, cor da 1ª classificação | **629** *Fila de Espera* (`rptView…parNum=1&par1=unidDef`, sem parâmetro): Nº Boletim, Nome, Início Atendimento, Tempo, Clínica — **sem cor**; cor vem de **667** *Nominal por Classificação de Risco* (1 dia) cruzando pelo Nº Boletim; **818** *Tempo Médio da Classificação de Risco por Período* | 629 medido (PDF 37 p. / XLS 2.414 linhas — traz lixo de teste de 2025 e boletins de 600 h: **a fila da web não filtra `DATA_CANCELAMENTO`**, filtrar por idade do boletim); 667 medido; 818 em sondagem |
| **U1b** em atendimento agora + atendimentos de hoje | `dt_med` preenchido e `dt_fim` nulo; `COUNT` do dia | **631** *Atendimentos em Andamento* (2 dias, todas as especialidades: Nº Boletim, Nome, Início, Tempo, Clínica, Profissional) — atenção: **com 1 dia só veio vazio**, com 15–16/09 veio 425 linhas (semântica do período a entender); total do dia = **407** | medido |
| **U2** total no período | `COUNT` por `spa_chegada` | 407 (contar linhas) ou **56/57** *Registro Diário/Mensal por Clínica* (só totais, XLS de 9–11 linhas — **barato**) | medido (56/57 são tabelas de totais) |
| **U3** série diária 35 dias | `GROUP BY dia` | 57 *Registro Mensal por Clínica* (1 chamada/mês) ou 56 por dia | medido |
| **U4** por hora de hoje | `DATEPART(hour)` | **58** *Registro por Hora de Urgência por Clínica* (`Relatorio.aspx?parrel=58&origem=5`) | em sondagem |
| **U6** espera por cor (classificação → médico; média/mediana/p90; meta Manchester fixa) | CTEs sobre classificação + atendimento | **526** *Atendimentos por Profissional* (`Relatorio.aspx?parrel=526&origem=5`, XLS 1 dia = 937 linhas / 1,1 MB): **No Boletim, Código (prof), Hora Atendimento, Nome, Classificação (cor), Idade, Sexo, Clínica** — cruzado com **667** (Data/Hora Entrada por boletim) dá chegada → atendimento por cor, por boletim, e daí média/mediana/p90 do nosso lado. 818 *Tempo Médio* e 831/835 **não servem**: 818 dá 504 até com 1 semana; 831 `COMException` OLE DB; 835 `FormulaException` do Crystal (relatórios quebrados no vendor) | **medido** (526, 667); 818/831/835 quebrados |
| **L4** leitos de observação (ocupação) | `Leito.lei_status` | **630** *Pacientes em Observação* (`rptviewXls.aspx?parRel=630&parNum=1&par1=unidDef`, sem parâmetro, 166 KB): **Nº Boletim, Nome, Início Atendimento, Tempo, Especialidade, Profissional, Espec. Obs., Profissional Obs., Leito** — 210 linhas no Conde (é quem está em observação agora, com o leito); tela `Share/Leitos/MapaLeitos.aspx?Modulo=E` para o inventário de leitos | **medido** (630) |
| **L5** fluxo para observação | subdescrição da classificação | 667 (coluna Origem?) / 630 | a medir |
| internações / maternidade | nulas nas UPAs | Conde interna: **100** *Internações Diárias* (XLS: Prontuário-Nome, Nasc, Idade, Sexo, Hora, Município/Bairro, Local) — parNum=9 | medido |

## 4. Política de requisições (para não onerar o fornecedor)

Regras propostas, todas derivadas de medição:

1. **Uma sessão por instância, persistida.** Login derruba a sessão anterior do mesmo usuário
   → usuário dedicado do conector e cookies guardados; relogar só quando `sessaoexpirada`.
2. **Uma requisição por vez por sessão.** O ASP.NET serializa a sessão; paralelizar só
   enfileira e provoca 504 no nginx (medido). Concorrência real = uma sessão por instância.
3. **Relatório em XLS, janela de 1 dia.** 30 dias no 631 estourou o Crystal
   (`OutOfMemoryException`) e deu 504 no XLS. 1 dia do 407 = 545 KB / ~2.000 linhas, poucos
   segundos. Para carga inicial: 1 dia por chamada, com intervalo entre chamadas.
4. **Preferir relatórios de totais quando o número basta.** 56/57 (registro por clínica) têm
   9–11 linhas; usar para U2/U3 em vez de somar o 407.
5. **Cadência por indicador, não por tela.** "Agora" (U1a/U1b/L4): 629 + 631 + 630 a cada
   60–120 s — três GETs sem parâmetro/2 dias. "Dia" (U2/U4): 407/58 a cada 10–15 min.
   "Mês" (U3/U6): 57/818 uma vez por hora. Carga do conector FHIR: 1× por dia, madrugada,
   dia anterior + reconciliação de D-1..D-3.
6. **Nada de varredura em produção.** O `probe_mapa.py` (368 telas, 23 min) é ferramenta
   de laboratório, uma vez. O conector só chama o que está na tabela do §3.
7. **Cache do lado de cá.** O painel já tem tick de 60 s; guardar o último XLS por
   (instância, parrel, parâmetros) e só recalcular quando o tick vencer.
8. **Sem ViewState fora do login/gate.** Tudo que é relatório é GET; `.asmx` é JSON.
   Telas com grade (profissional, cadastro por prontuário) ficam para o que não tem
   relatório — e aí com postback mínimo (uma página por vez).

## 5. Plano de corte (desligar o banco, ligar a web) — **nada disto foi executado**

Ordem sugerida; cada passo é uma ação em produção e só roda com OK explícito:

1. **Conde no painel primeiro** (é o que está parado): implementar em
   `SMSMarica.secretario.pwa/back/Painel/` um `ConsultasKlinikosWeb` que produza o **mesmo
   contrato** (`contrato-painel.json`) a partir de 629/631/630/407/56/57/58/818 via
   `KlinikosSession` portado para .NET (login + gate + GET XLS + parser BIFF). Sem tocar nas
   UPAs ainda.
2. **Confirmar as instâncias web das UPAs** (URL, usuário dedicado, `unid_codigo`) e repetir
   `probe_login.py` + `sondar_relatorios.py` lá — só então trocar `ConsultasUpa` pelo mesmo
   `ConsultasKlinikosWeb`.
3. **Conector FHIR**: nova `KlinikosWebImportacaoStrategy` para o que sai por relatório
   (boletim/chegada/CID/desfecho/risco). Narrativa, prescrição e sinais vitais: **decidir**
   (tela por boletim × pedir ao fornecedor um relatório/exportação × manter agente SQL só
   para isso).
4. **Desligar o agente WSS / slugs SQL** por instância, depois que a web estiver alimentando
   o mesmo contrato por ≥ 1 semana com conferência de números lado a lado.
5. **"Quais e como as informações podem ser enviadas"** (quando o nosso sistema atualizar o
   deles): o único caminho de escrita medido é o mesmo da tela — postback WebForms com
   ViewState + `__EVENTVALIDATION` — e os `.asmx` expostos são de leitura (`GetData`,
   `GetProcedimento`) ou administrativos (`DeletarVinculo`). **Não há API de escrita.**
   Escrever = replicar a tela (Registro.aspx tem 111 campos, 22 botões). Fica para depois,
   com ADR próprio e trava invertida como no SER.

## 6. Sincronismo de cadastros — "quem foi cadastrado desde ontem" (medido)

Não há `rv_atualizacao` na web, mas o módulo Cadastro tem dois relatórios **por data de
criação do prontuário**, os dois em XLS por GET (a tela só oferece PDF, mas o `rptviewXls`
aceita os mesmos parâmetros):

| Relatório | Endpoint (1 dia) | Colunas | Medido 15/09 |
|---|---|---|---|
| **21** *Prontuários Abertos* (`Cadastro/Relatorios/ProntuariosAbertos.aspx`) | `rptviewXls.aspx?parRel=21&parNum=4&par1=ini&par2=fim&par3=0000&par4=0005` (`par3` = clínica, `0000` = todas) | **Prontuário, Paciente, Clínica, Nascimento, Idade, Sexo, CNS** | 37 prontuários novos no dia (rodapé do PDF: 765 abertos no período/total) |
| **488** *Origem Criação Prontuário* (`Cadastro/Relatorios/OrigemCriacaoProntuario.aspx`) | `rptviewXls.aspx?parRel=488&parNum=5&par1=0005&par2=&par3=&par4=ini&par5=fim` (`par2/par3` = faixa de prontuário, alternativa às datas) | **Data Abertura, Código (boletim), Paciente, Prontuário, Setor de Origem** | 7 linhas |

Os dois usam o controle de data `UCPesquisaData1$rdtpDataInicio/Fim` (RadDateTimePicker) —
diferente do `ctlParam$rdpData*` das telas genéricas. O 21 é a fonte do **sincronismo de
cadastro**: prontuário (chave local), nascimento, sexo e **CNS**. Faltam CPF, mãe, telefone e
endereço — só na tela `Cadastro/Paciente/CadastroBasico.aspx` (por prontuário, postback) ou
num relatório que o fornecedor exporte. **390** *Cadastro no Estabelecimento de Saúde*
(`ParamPaciente.aspx`) é tela de consulta por paciente (só `btnLimpar`/`imbVoltar`), não lista.
**381** *Registros Abertos* exige clínica selecionada (`ddlClinica`) — testar por clínica.

Para a **UPA** o 407 (registrados no dia: Nº Boletim + Prontuário + Paciente + Nasc/Idade)
já cobre "paciente novo que apareceu"; o 21 completa com CNS e sexo.
