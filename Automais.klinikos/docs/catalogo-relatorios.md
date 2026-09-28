# Klinikos — catálogo de relatórios e telas (dos menus, sem clicar)

> Gerado por `catalogo_relatorios.py` a partir das homes dos 10 módulos (`capturas/modulo_*.html`, 16/09/2026, instância do Conde). 539 URLs únicas: **268 relatórios** e 271 telas. Só rótulo de menu e URL — sem PII.

Tipos: `parametro` = `Relatorios/ParametroRelatorio.aspx?parrel=N&Modulo=X` (tela genérica de parâmetros; o relatório sai por `rptview(Xls).aspx?parRel=N&parNum=K&par1..parK`); `relatorio` = `<Modulo>/Relatorios/Relatorio(s).aspx?parrel=N&origem=K` (tela própria do módulo, mesmo mecanismo); `rptview` = link direto `rptView.aspx?parrel=N&parNum=1&par1=unidDef` (sem parâmetro); `rel-proprio` = página própria com grade/filtros.

## Relatórios por módulo

### Acesso (10)

| parrel | tipo | caminho no menu | rótulo | URL |
|---|---|---|---|---|
| 657 | relatorio |  > Relatórios > Ambulância | Como Chegou | `/KlinikosNet/Acesso/Relatorios/Relatorios.aspx?parRel=657` |
| 655 | relatorio |  > Relatórios > Ambulância | Motivo de Chegada Diário | `/KlinikosNet/Acesso/Relatorios/Relatorios.aspx?parRel=655` |
| 656 | relatorio |  > Relatórios > Ambulância | Motivo de Saída Diário | `/KlinikosNet/Acesso/Relatorios/Relatorios.aspx?parRel=656` |
| 620 | relatorio |  > Relatórios > Ambulância | Movimento Diário | `/KlinikosNet/Acesso/Relatorios/Relatorios.aspx?parRel=620` |
| 652 | relatorio |  > Relatórios > Ambulância | Tipo Viatura Diário | `/KlinikosNet/Acesso/Relatorios/Relatorios.aspx?parRel=652` |
| 654 | relatorio |  > Relatórios > Ambulância | Unidade de Destino Diário | `/KlinikosNet/Acesso/Relatorios/Relatorios.aspx?parRel=654` |
| 653 | relatorio |  > Relatórios > Ambulância | Unidade de Origem Diário | `/KlinikosNet/Acesso/Relatorios/Relatorios.aspx?parRel=653` |
| 714 | relatorio |  > Relatórios > Visitante | Controle de acompanhantes por paciente | `/KlinikosNet/Acesso/Relatorios/Relatorios.aspx?parRel=714` |
| 712 | relatorio |  > Relatórios > Visitante | Controle de visitantes por paciente | `/KlinikosNet/Acesso/Relatorios/Relatorios.aspx?parRel=712` |
| 713 | relatorio |  > Relatórios > Visitante | Estatística de visitantes | `/KlinikosNet/Acesso/Relatorios/Relatorios.aspx?parRel=713` |

### Administracao (3)

| parrel | tipo | caminho no menu | rótulo | URL |
|---|---|---|---|---|
|  | rel-proprio |  > Relatórios | Configuração de Cabeçalho | `/KlinikosNet/Administracao/Relatorios/ConfiguraCabecalho.aspx` |
|  | rel-proprio |  > Relatórios | Configuração de Impressora de Pulseiras | `/KlinikosNet/Administracao/Relatorios/ConfiguraImpressaoPulseira.aspx` |
|  | rel-proprio |  > Relatórios | Configuração de Relatórios | `/KlinikosNet/Administracao/Relatorios/ConfiguraRelatorio.aspx` |

### Ambulatorio (53)

| parrel | tipo | caminho no menu | rótulo | URL |
|---|---|---|---|---|
|  | rel-proprio |  > Relatórios > Ambulatório | Exames | `/KlinikosNet/Ambulatorio/Relatorios/Central/Exames.aspx` |
|  | rel-proprio |  > Relatórios > Ambulatório > Exames | Marcação de Exames | `/KlinikosNet/Ambulatorio/Relatorios/Central/Exames.aspx?id=0` |
|  | rel-proprio |  > Relatórios > Ambulatório > Exames | Marcação de Exames agrupados | `/KlinikosNet/Ambulatorio/Relatorios/Central/Exames.aspx?id=1` |
|  | rel-proprio |  > Relatórios > Ambulatório > Exames | Marcação de Exames por Grupo, Data e Hora | `/KlinikosNet/Ambulatorio/Relatorios/Central/ExameGrupo.aspx` |
|  | rel-proprio |  > Relatórios > Ambulatório > Marcação de Consulta | Agenda de Consultas | `/KlinikosNet/Ambulatorio/Relatorios/Central/MarcacaoConsulta.aspx?id=0` |
|  | rel-proprio |  > Relatórios > Ambulatório > Marcação de Consulta | Agenda do Profissional de Saúde | `/KlinikosNet/Ambulatorio/Relatorios/Central/MarcacaoConsulta.aspx?id=1` |
|  | rel-proprio |  > Relatórios > Ambulatório > Marcação de Consulta | Agendas Disponíveis e Marcadas | `/KlinikosNet/Ambulatorio/Relatorios/Central/MarcacaoConsulta.aspx?id=8` |
|  | rel-proprio |  > Relatórios > Ambulatório > Marcação de Consulta | Boleto Consulta/Exame | `/KlinikosNet/Ambulatorio/Relatorios/Central/OutrosRelatorios.aspx?id=0` |
|  | rel-proprio |  > Relatórios > Ambulatório > Marcação de Consulta | Cartão Municipal Paciente | `/KlinikosNet/Ambulatorio/Relatorios/Central/MarcacaoConsulta.aspx?id=7` |
|  | rel-proprio |  > Relatórios > Ambulatório > Marcação de Consulta | Dados Pacientes Agendados | `/KlinikosNet/Ambulatorio/Relatorios/Central/MarcacaoConsulta.aspx?id=2` |
|  | rel-proprio |  > Relatórios > Ambulatório > Marcação de Consulta | Ficha de Identificação do Paciente | `/KlinikosNet/Ambulatorio/Relatorios/Central/MarcacaoConsulta.aspx?id=6` |
|  | rel-proprio |  > Relatórios > Ambulatório > Marcação de Consulta | Fila de Espera | `/KlinikosNet/Ambulatorio/Relatorios/Central/OutrosRelatorios.aspx?id=2` |
|  | rel-proprio |  > Relatórios > Ambulatório > Marcação de Consulta | Histórico de Marcação (Clínica) | `/KlinikosNet/Ambulatorio/Relatorios/Central/MarcacaoConsulta.aspx?id=4` |
|  | rel-proprio |  > Relatórios > Ambulatório > Marcação de Consulta | Histórico de Marcação (Usuário) | `/KlinikosNet/Ambulatorio/Relatorios/Central/MarcacaoConsulta.aspx?id=3` |
|  | rel-proprio |  > Relatórios > Ambulatório > Marcação de Consulta | Histórico do Paciente | `/KlinikosNet/Ambulatorio/Relatorios/Central/MarcacaoConsulta.aspx?id=5` |
|  | rel-proprio |  > Relatórios > Ambulatório > Marcação de Consulta | Marcação de Consulta por Especialidade Clínica | `/KlinikosNet/Ambulatorio/Relatorios/Central/OutrosRelatorios.aspx?id=1` |
|  | rel-proprio |  > Relatórios > Ambulatório > Marcação de Consulta | Movimentação Diária | `/KlinikosNet/Ambulatorio/Relatorios/Central/OutrosRelatorios.aspx?id=3` |
|  | rel-proprio |  > Relatórios > Ambulatório > Marcação de Consulta | Notificação Compulsória | `/KlinikosNet/Ambulatorio/Relatorios/Central/OutrosRelatorios.aspx?id=8` |
|  | rel-proprio |  > Relatórios > Ambulatório > Marcação de Consulta | Ordem de Prontuário | `/KlinikosNet/Ambulatorio/Relatorios/Central/OutrosRelatorios.aspx?id=5` |
|  | rel-proprio |  > Relatórios > Ambulatório > Marcação de Consulta | Paciente Novo | `/KlinikosNet/Ambulatorio/Relatorios/Central/OutrosRelatorios.aspx?id=7` |
|  | rel-proprio |  > Relatórios > Ambulatório > Marcação de Consulta | Paciente com Consulta Marcada para o mesmo Dia | `/KlinikosNet/Ambulatorio/Relatorios/Central/OutrosRelatorios.aspx?id=4` |
|  | rel-proprio |  > Relatórios > Ambulatório > Marcação de Consulta | Resumo Diário de Agendas | `/KlinikosNet/Ambulatorio/Relatorios/Central/OutrosRelatorios.aspx?id=6` |
|  | rel-proprio |  > Relatórios > CheckIn | Pacientes Ausentes | `/KlinikosNet/Ambulatorio/Relatorios/Central/PedidosAgendas.aspx?id=10` |
|  | rel-proprio |  > Relatórios > CheckOut > GeoEstatísticos | Geo-Estatístico Diário por Município e Bairro | `/KlinikosNet/Ambulatorio/Relatorios/CheckIn/GeoEstatisticos.aspx?id=1` |
|  | rel-proprio |  > Relatórios > CheckOut > GeoEstatísticos | Geo-Estatístico por Município, Bairro e Clínica | `/KlinikosNet/Ambulatorio/Relatorios/CheckIn/GeoEstatisticos.aspx?id=0` |
|  | rel-proprio |  > Relatórios > CheckOut > GeoEstatísticos | Geo-Estatístico por Município, Bairro e Faixa Etária | `/KlinikosNet/Ambulatorio/Relatorios/CheckIn/GeoEstatisticos.aspx?id=2` |
|  | rel-proprio |  > Relatórios > CheckOut > GeoEstatísticos | Geo-Estatístico por Município, Bairro e Grupo Diagnóstico | `/KlinikosNet/Ambulatorio/Relatorios/CheckIn/GeoEstatisticos.aspx?id=3` |
|  | rel-proprio |  > Relatórios > CheckOut > GeoEstatísticos | Geo-Estatístico por Período, Município, Bairro e Clínica | `/KlinikosNet/Ambulatorio/Relatorios/CheckIn/GeoEstatisticos.aspx?id=5` |
|  | rel-proprio |  > Relatórios > CheckOut > Produção | Atendimentos Ambulatoriais Pendentes Realizados | `/KlinikosNet/Ambulatorio/Relatorios/CheckIn/Producao.aspx?id=5` |
|  | rel-proprio |  > Relatórios > CheckOut > Produção | Procedimentos por Clínica | `/KlinikosNet/Ambulatorio/Relatorios/CheckIn/CheckOut.aspx?id=0` |
|  | rel-proprio |  > Relatórios > CheckOut > Produção | Procedimentos por Profissional | `/KlinikosNet/Ambulatorio/Relatorios/CheckIn/CheckOut.aspx?id=1` |
|  | rel-proprio |  > Relatórios > CheckOut > Produção | Produção Clínica Hora | `/KlinikosNet/Ambulatorio/Relatorios/CheckIn/Producao.aspx?id=2` |
|  | rel-proprio |  > Relatórios > CheckOut > Produção | Produção Clínica Mensal | `/KlinikosNet/Ambulatorio/Relatorios/CheckIn/Producao.aspx?id=1` |
|  | rel-proprio |  > Relatórios > CheckOut > Produção | Produção Clínica Mensal por Profissional | `/KlinikosNet/Ambulatorio/Relatorios/CheckIn/Producao.aspx?id=3` |
|  | rel-proprio |  > Relatórios > CheckOut > Produção | Produção Diária por Clínica | `/KlinikosNet/Ambulatorio/Relatorios/CheckIn/Producao.aspx?id=0` |
|  | rel-proprio |  > Relatórios > CheckOut > Produção | Produção Grupo Diagnóstico, Sexo e Faixa Etária | `/KlinikosNet/Ambulatorio/Relatorios/CheckIn/Producao.aspx?id=4` |
|  | rel-proprio |  > Relatórios > CheckOut > Produção | Produção por tipo de saída | `/KlinikosNet/Ambulatorio/Relatorios/CheckIn/Producao.aspx?id=6` |
|  | rel-proprio |  > Relatórios > Pedidos e Agendas | Agenda Futura de Consulta | `/KlinikosNet/Ambulatorio/Relatorios/Central/PedidosAgendas.aspx?id=12` |
|  | rel-proprio |  > Relatórios > Pedidos e Agendas | Agendas Diária por Turnos | `/KlinikosNet/Ambulatorio/Relatorios/Central/PedidosAgendas.aspx?id=0` |
|  | rel-proprio |  > Relatórios > Pedidos e Agendas | Bloqueio de Agenda | `/KlinikosNet/Ambulatorio/Relatorios/Central/PedidosAgendas.aspx?id=14` |
|  | rel-proprio |  > Relatórios > Pedidos e Agendas | Geo Agendas Especialidade | `/KlinikosNet/Ambulatorio/Relatorios/Central/PedidosAgendas.aspx?id=6` |
|  | rel-proprio |  > Relatórios > Pedidos e Agendas | Geo Agendas Faixa Etária | `/KlinikosNet/Ambulatorio/Relatorios/Central/PedidosAgendas.aspx?id=7` |
|  | rel-proprio |  > Relatórios > Pedidos e Agendas | Geo Agendas Sexo | `/KlinikosNet/Ambulatorio/Relatorios/Central/PedidosAgendas.aspx?id=8` |
|  | rel-proprio |  > Relatórios > Pedidos e Agendas | Geo Agendas Turno | `/KlinikosNet/Ambulatorio/Relatorios/Central/PedidosAgendas.aspx?id=5` |
|  | rel-proprio |  > Relatórios > Pedidos e Agendas | Ocorrências de Agendas | `/KlinikosNet/Ambulatorio/Relatorios/Central/PedidosAgendas.aspx?id=17` |
|  | rel-proprio |  > Relatórios > Pedidos e Agendas | Pacientes Agendados | `/KlinikosNet/Ambulatorio/Relatorios/Central/PedidosAgendas.aspx?id=9` |
|  | rel-proprio |  > Relatórios > Pedidos e Agendas | Produção Usuários 1a Vez | `/KlinikosNet/Ambulatorio/Relatorios/Central/PedidosAgendas.aspx?id=15` |
|  | rel-proprio |  > Relatórios > Pedidos e Agendas | Produção Usuários Subsequente | `/KlinikosNet/Ambulatorio/Relatorios/Central/PedidosAgendas.aspx?id=16` |
|  | rel-proprio |  > Relatórios > Pedidos e Agendas | Produção de Usuários | `/KlinikosNet/Ambulatorio/Relatorios/Central/PedidosAgendas.aspx?id=11` |
|  | rel-proprio |  > Relatórios > Pedidos e Agendas | Ranking por Profissionais | `/KlinikosNet/Ambulatorio/Relatorios/Central/PedidosAgendas.aspx?id=3` |
|  | rel-proprio |  > Relatórios > Pedidos e Agendas | Turnos por Especialidade | `/KlinikosNet/Ambulatorio/Relatorios/Central/PedidosAgendas.aspx?id=2` |
|  | rel-proprio |  > Relatórios > Pedidos e Agendas | Turnos por Profissionais | `/KlinikosNet/Ambulatorio/Relatorios/Central/PedidosAgendas.aspx?id=1` |
|  | rel-proprio |  > Relatórios > Pedidos e Agendas | Utilização de Agendas | `/KlinikosNet/Ambulatorio/Relatorios/Central/PedidosAgendas.aspx?id=4` |

### Cadastro (15)

| parrel | tipo | caminho no menu | rótulo | URL |
|---|---|---|---|---|
| 390 | rel-proprio |  > Relatórios > Operacionais | Cadastro no Estabelecimento de Saúde | `/KlinikosNet/Cadastro/Relatorios/ParamPaciente.aspx?parrel=390` |
| 296 | rel-proprio |  > Relatórios > Operacionais | Cartão Paciente | `/KlinikosNet/Cadastro/Relatorios/CartaoPaciente.aspx?parrel=296` |
| 462 | rel-proprio |  > Relatórios > Operacionais | Hist. Movimento Prontuário | `/KlinikosNet/Cadastro/Relatorios/HistorioMovPront.aspx?parrel=462&codPar=1` |
| 18 | rel-proprio |  > Relatórios > Operacionais | Homônimos | `/KlinikosNet/Cadastro/Relatorios/ListaHomonimos.aspx?parrel=18` |
| 664 | rel-proprio |  > Relatórios > Operacionais | Nominal Prontuários Inativos | `/KlinikosNet/Cadastro/Relatorios/NominalProntuarioInativos.aspx?parrel=664` |
| 418 | rel-proprio |  > Relatórios > Operacionais | Nominal por Prontuário | `/KlinikosNet/Cadastro/Relatorios/NominalProntuario.aspx?parrel=418` |
| 99 | parametro |  > Relatórios > Operacionais | Notificação Compulsória (Ficha) | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=99&Modulo=INTERNACAO` |
| 123 | parametro |  > Relatórios > Operacionais | Notificação Compulsória (Relação) | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=123&Modulo=INTERNACAO` |
| 488 | rel-proprio |  > Relatórios > Operacionais | Origem Prontuário | `/KlinikosNet/Cadastro/Relatorios/OrigemCriacaoProntuario.aspx?parrel=488` |
| 731 | rel-proprio |  > Relatórios > Operacionais | Pacientes com CEP Inválido | `/KlinikosNet/Cadastro/Relatorios/CepInvalido.aspx?parrel=731` |
| 21 | rel-proprio |  > Relatórios > Operacionais | Prontuários Abertos | `/KlinikosNet/Cadastro/Relatorios/ProntuariosAbertos.aspx?parrel=21` |
| 19 | rel-proprio |  > Relatórios > Operacionais | Prontuários Repetidos | `/KlinikosNet/Cadastro/Relatorios/ProntuariosRepetidos.aspx?parrel=19` |
| 463 | rel-proprio |  > Relatórios > Operacionais | Prontuários dias excedidos | `/KlinikosNet/Cadastro/Relatorios/MovProntExcedido.aspx?parrel=463&codPar=1` |
| 381 | rel-proprio |  > Relatórios > Operacionais | Registros Abertos | `/KlinikosNet/Cadastro/Relatorios/RegistrosAbertos.aspx?parrel=381` |
| 695 | rel-proprio |  > Relatórios > Operacionais | Óbitos Geral | `/KlinikosNet/Cadastro/Relatorios/ObitosGeral.aspx?parrel=695` |

### CentroCirurgico (10)

| parrel | tipo | caminho no menu | rótulo | URL |
|---|---|---|---|---|
|  | rel-proprio |  > Relatórios | Estatísticos | `/KlinikosNet/CentroCirurgico/Relatorios/Estatisticas.aspx` |
|  | rel-proprio |  > Relatórios > Cirúrgicos | Anotação Cirurgia | `/KlinikosNet/CentroCirurgico/Relatorios/Cirurgico.aspx?id=1` |
|  | rel-proprio |  > Relatórios > Cirúrgicos | Cirurgias Realizadas | `/KlinikosNet/CentroCirurgico/Relatorios/Cirurgico.aspx?id=0` |
|  | rel-proprio |  > Relatórios > Estatísticos | Cirurgias por Profissionais | `/KlinikosNet/CentroCirurgico/Relatorios/Estatisticas.aspx?id=2` |
| 802 | parametro |  > Relatórios > Estatísticos | Relatório de Óbito por Locais de Internação | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=802` |
|  | rel-proprio |  > Relatórios > Estatísticos | Utilização de Materiais | `/KlinikosNet/CentroCirurgico/Relatorios/Estatisticas.aspx?id=0` |
|  | rel-proprio |  > Relatórios > Estatísticos | Utilização de Salas | `/KlinikosNet/CentroCirurgico/Relatorios/Estatisticas.aspx?id=1` |
|  | rel-proprio |  > Relatórios > Preparação | Atendida e Não Atendida | `/KlinikosNet/CentroCirurgico/Relatorios/Preparacao.aspx?id=0` |
|  | rel-proprio |  > Relatórios > Preparação | Mapa Cirúrgico | `/KlinikosNet/CentroCirurgico/Relatorios/Preparacao.aspx?id=1` |
|  | rel-proprio |  > Relatórios > Pré-Operatório | Pré-Operatório Completo | `/KlinikosNet/CentroCirurgico/Relatorios/PreCirurgico.aspx?id=0` |

### Internacao (61)

| parrel | tipo | caminho no menu | rótulo | URL |
|---|---|---|---|---|
| 447 | parametro |  > Relatórios > Censo Diário | Censo - Pacientes Internados | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=447&Modulo=INTERNACAO` |
| 46 | parametro |  > Relatórios > Censo Diário | Censo Hospitalar - Diário | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=46&Modulo=INTERNACAO` |
| 261 | parametro |  > Relatórios > Censo Diário | Grade de Ocupação de Leitos | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=261&Modulo=INTERNACAO` |
| 321 | parametro |  > Relatórios > Censo Diário | Internações Fora do Censo | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=321&Modulo=INTERNACAO` |
| 514 | parametro |  > Relatórios > Censo Diário | Leitos Operacionais | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=514&Modulo=INTERNACAO` |
| 102 | parametro |  > Relatórios > Censo Diário | Mapa Diário - Leitos | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=102&Modulo=INTERNACAO` |
| 104 | parametro |  > Relatórios > Censo Diário | Movimentação Diária | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=104&Modulo=INTERNACAO` |
| 322 | rel-proprio |  > Relatórios > Censo Diário | Ocorrências na Internação | `/KlinikosNet/Internacao/Relatorios/MovimentacoesPaciente.aspx?parrel=322&Modulo=INTERNACAO` |
| 293 | parametro |  > Relatórios > Diagnósticos | Diagnóstico por Município / Bairro | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=293&Modulo=INTERNACAO` |
| 92 | parametro |  > Relatórios > Diagnósticos | Diário | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=92&Modulo=INTERNACAO` |
| 720 | relatorio |  > Relatórios > Diagnósticos | Ficha de Notificação Compulsória | `/KlinikosNet/UPA/Relatorios/Relatorio.aspx?parrel=720&origem=0&Modulo=INTERNACAO` |
| 95 | parametro |  > Relatórios > Diagnósticos | Mensal | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=95&Modulo=INTERNACAO` |
| 103 | parametro |  > Relatórios > Diagnósticos | Morbidade Hospitalar | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=103&Modulo=INTERNACAO` |
| 399 | rel-proprio |  > Relatórios > Diagnósticos | Por CID | `/KlinikosNet/Internacao/Relatorios/RelatorioDiagnosticoCID.aspx?parrel=399` |
| 400 | rel-proprio |  > Relatórios > Diagnósticos | Por CID e Município | `/KlinikosNet/Internacao/Relatorios/RelatorioDiagnosticoCIDMunicipioBairro.aspx?parrel=400` |
| 243 | parametro |  > Relatórios > Diagnósticos | Por Clínica | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=243&Modulo=INTERNACAO` |
| 93 | parametro |  > Relatórios > Diagnósticos | Por Frequência | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=93&Modulo=INTERNACAO` |
| 529 | relatorio |  > Relatórios > Diagnósticos | Relatório de Notificações Compulsórias | `/KlinikosNet/Internacao/Relatorios/Relatorio.aspx?parrel=529&Modulo=INTERNACAO` |
| 90 | parametro |  > Relatórios > Estatísticas | Boletim Movimento Hospitalar | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=90&Modulo=INTERNACAO&Clinica=S` |
|  | rel-proprio |  > Relatórios > Estatísticas | Dispositivos / Bundle | `/KlinikosNet/Internacao/Relatorios/RelatorioDispositivosBundle.aspx?` |
| 50 | parametro |  > Relatórios > Estatísticas | Diária - Clínica | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=50&Modulo=INTERNACAO` |
| 94 | parametro |  > Relatórios > Estatísticas | Diária/Mensal - Leitos de Apoio | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=94&Modulo=INTERNACAO` |
| 283 | parametro |  > Relatórios > Estatísticas | Estatística Anual - Internações | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=283&Modulo=INTERNACAO` |
| 282 | parametro |  > Relatórios > Estatísticas | Estatística Anual - Mortalidade | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=282&Modulo=INTERNACAO` |
| 281 | parametro |  > Relatórios > Estatísticas | Estatística Anual - Ocupação | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=281&Modulo=INTERNACAO` |
| 723 | parametro |  > Relatórios > Estatísticas | Indicadores Hospitalares | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=723&Modulo=INTERNACAO` |
| 51 | parametro |  > Relatórios > Estatísticas | Mensal - Clínica | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=51&Modulo=INTERNACAO` |
| 105 | parametro |  > Relatórios > Estatísticas | Movimentação do Censo | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=105&Modulo=INTERNACAO` |
| 122 | parametro |  > Relatórios > Estatísticas | Quantidade de Óbitos | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=122&Modulo=INTERNACAO` |
| 249 | rel-proprio |  > Relatórios > Internações e Altas | Conta Paciente | `/KlinikosNet/Internacao/Relatorios/ContaPaciente.aspx?parrel=249&Modulo=INTERNACAO` |
| 101 | parametro |  > Relatórios > Internações e Altas | Internados no Dia | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=101&Modulo=INTERNACAO` |
| 100 | parametro |  > Relatórios > Internações e Altas | Internação Diaria | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=100&Modulo=INTERNACAO` |
| 320 | parametro |  > Relatórios > Internações e Altas | Internação com Alta no mesmo Dia | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=320&Modulo=INTERNACAO` |
| 294 | parametro |  > Relatórios > Internações e Altas | Internação de Adolescentes | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=294&Modulo=INTERNACAO` |
| 106 | rel-proprio |  > Relatórios > Internações e Altas | Movimentações do Paciente | `/KlinikosNet/Internacao/Relatorios/MovimentacoesPaciente.aspx?parrel=106&Modulo=INTERNACAO` |
| 118 | parametro |  > Relatórios > Internações e Altas | Ped. Pendentes X Realizados | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=118&Modulo=INTERNACAO` |
| 802 | parametro |  > Relatórios > Internações e Altas | Relatório de Óbito por Locais de Internação | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=802&Modulo=INTERNACAO` |
| 125 | parametro |  > Relatórios > Internações e Altas | Saídas Diárias | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=125&Modulo=INTERNACAO` |
|  | rel-proprio |  > Relatórios > Internações e Altas | Saídas por Ordem Alfabética | `/KlinikosNet/Internacao/Relatorios/RelatorioSaidaOrdemAlfabetica.aspx?` |
|  | rel-proprio |  > Relatórios > Internações e Altas | Situação dos Leitos | `/KlinikosNet/Internacao/Relatorios/RelatorioLeitos.aspx?` |
| 404 | rel-proprio |  > Relatórios > Internações e Altas | Tempo de Permanência Clínica/Leito | `/KlinikosNet/Internacao/Relatorios/InternacaoAltaPermanenciaClinicaLeito.aspx?parrel=404` |
| 107 | parametro |  > Relatórios > Internações e Altas | Óbitos Diários | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=107&Modulo=INTERNACAO` |
| 108 | parametro |  > Relatórios > Internações e Altas | Óbitos por Municipio | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=108&Modulo=INTERNACAO` |
| 109 | parametro |  > Relatórios > Pacientes Internados | Alfabético | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=109&Modulo=INTERNACAO` |
| 110 | parametro |  > Relatórios > Pacientes Internados | Clínica do Paciente | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=110&Modulo=INTERNACAO` |
| 111 | parametro |  > Relatórios > Pacientes Internados | Diagnósticos | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=111&Modulo=INTERNACAO` |
| 112 | rel-proprio |  > Relatórios > Pacientes Internados | Faixa Etária | `/KlinikosNet/Internacao/Relatorios/RelatorioInternadosFaixaEtaria.aspx?parrel=112` |
| 113 | parametro |  > Relatórios > Pacientes Internados | Fora de Clínica | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=113&Modulo=INTERNACAO` |
| 114 | relatorio |  > Relatórios > Pacientes Internados | Localização Física | `/KlinikosNet/UPA/Relatorios/Relatorio.aspx?parrel=114&origem=4` |
| 115 | rel-proprio |  > Relatórios > Pacientes Internados | Município e Bairro | `/KlinikosNet/Internacao/Relatorios/RelatorioInternadosMunicipioBairro.aspx?parrel=115` |
| 747 | parametro |  > Relatórios > Pacientes Internados | Pendências e Observações | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=747&Modulo=INTERNACAO` |
| 116 | parametro |  > Relatórios > Pacientes Internados | Procedimentos | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=116&Modulo=INTERNACAO` |
| 117 | rel-proprio |  > Relatórios > Pacientes Internados | Tempo de Permanência | `/KlinikosNet/Internacao/Relatorios/RelatorioInternadosTempoPermanencia.aspx?parrel=117` |
| 119 | parametro |  > Relatórios > Procedimentos e Outros | Diários | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=119&Modulo=INTERNACAO` |
| 96 | parametro |  > Relatórios > Procedimentos e Outros | Mensais | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=96&Modulo=INTERNACAO` |
| 120 | parametro |  > Relatórios > Procedimentos e Outros | Por Clínica | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=120&Modulo=INTERNACAO` |
| 121 | parametro |  > Relatórios > Procedimentos e Outros | Por Frequência | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=121&Modulo=INTERNACAO` |
| 295 | parametro |  > Relatórios > Procedimentos e Outros | Procedimento Município/Bairro | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=295&Modulo=INTERNACAO` |
| 823 | parametro |  > Relatórios > Procedimentos e Outros | Produção de Fisioterapia por procedimento e profissional | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=823&Modulo=INTERNACAO` |
| 408 | parametro |  > Relatórios > Procedimentos e Outros | Reinternação | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=408&Modulo=INTERNACAO` |
| 477 | parametro |  > Relatórios > Procedimentos e Outros | Tempo Médio | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=477&Modulo=INTERNACAO` |

### Laboratorio (25)

| parrel | tipo | caminho no menu | rótulo | URL |
|---|---|---|---|---|
|  | rel-proprio |  > Relatórios | Laudos | `/KlinikosNet/Laboratorio/Relatorios/Laudo.aspx` |
|  | rel-proprio |  > Relatórios > Laboratório | Cadastrais | `/KlinikosNet/Laboratorio/Relatorios/Cadastrais.aspx` |
|  | rel-proprio |  > Relatórios > Laboratório > Estatísticos | Envio para Laboratório Externo | `/KlinikosNet/Laboratorio/Relatorios/Estatisticos.aspx?id=11` |
|  | rel-proprio |  > Relatórios > Laboratório > Estatísticos | Notific. compulsória | `/KlinikosNet/Laboratorio/Relatorios/Estatisticos.aspx?id=7` |
|  | rel-proprio |  > Relatórios > Laboratório > Estatísticos | Pacientes Atendidos Analítico | `/KlinikosNet/Laboratorio/Relatorios/Estatisticos.aspx?id=10` |
|  | rel-proprio |  > Relatórios > Laboratório > Estatísticos | Pacientes Atendidos Sintético | `/KlinikosNet/Laboratorio/Relatorios/Estatisticos.aspx?id=9` |
|  | rel-proprio |  > Relatórios > Laboratório > Estatísticos | Por Clínica | `/KlinikosNet/Laboratorio/Relatorios/Estatisticos.aspx?id=1` |
|  | rel-proprio |  > Relatórios > Laboratório > Estatísticos | Por Destino | `/KlinikosNet/Laboratorio/Relatorios/Estatisticos.aspx?id=3` |
|  | rel-proprio |  > Relatórios > Laboratório > Estatísticos | Por Exame/Ano | `/KlinikosNet/Laboratorio/Relatorios/Estatisticos.aspx?id=6` |
|  | rel-proprio |  > Relatórios > Laboratório > Estatísticos | Por Município | `/KlinikosNet/Laboratorio/Relatorios/Estatisticos.aspx?id=2` |
|  | rel-proprio |  > Relatórios > Laboratório > Estatísticos | Por Seção | `/KlinikosNet/Laboratorio/Relatorios/Estatisticos.aspx?id=4` |
|  | rel-proprio |  > Relatórios > Laboratório > Estatísticos | Por Seção/Equipamento | `/KlinikosNet/Laboratorio/Relatorios/Estatisticos.aspx?id=5` |
|  | rel-proprio |  > Relatórios > Laboratório > Estatísticos | Por Seção/Unid. Solicit. | `/KlinikosNet/Laboratorio/Relatorios/Estatisticos.aspx?id=0` |
|  | rel-proprio |  > Relatórios > Laboratório > Estatísticos | Prod. Laudos por Operador | `/KlinikosNet/Laboratorio/Relatorios/Estatisticos.aspx?id=8` |
|  | rel-proprio |  > Relatórios > Laboratório > Operacionais | Amostras Excluídas | `/KlinikosNet/Laboratorio/Relatorios/AmostrasExcluidas.aspx` |
|  | rel-proprio |  > Relatórios > Laboratório > Operacionais | Coleta de Exame | `/KlinikosNet/Laboratorio/Relatorios/Operacionais.aspx?id=8` |
|  | rel-proprio |  > Relatórios > Laboratório > Operacionais | Exames Pendentes | `/KlinikosNet/Laboratorio/Relatorios/Operacionais.aspx?id=1` |
|  | rel-proprio |  > Relatórios > Laboratório > Operacionais | Exames por Município | `/KlinikosNet/Laboratorio/Relatorios/Operacionais.aspx?id=6` |
|  | rel-proprio |  > Relatórios > Laboratório > Operacionais | Faturamento de Exames | `/KlinikosNet/Laboratorio/Relatorios/Operacionais.aspx?id=7` |
|  | rel-proprio |  > Relatórios > Laboratório > Operacionais | Guia de Remessa para o Lab Externo | `/KlinikosNet/Laboratorio/Relatorios/Estatisticos.aspx?id=12` |
|  | rel-proprio |  > Relatórios > Laboratório > Operacionais | Mapa de Trabalho | `/KlinikosNet/Laboratorio/Relatorios/Operacionais.aspx?id=3` |
|  | rel-proprio |  > Relatórios > Laboratório > Operacionais | Movimentação Diária | `/KlinikosNet/Laboratorio/Relatorios/Operacionais.aspx?id=2` |
|  | rel-proprio |  > Relatórios > Laboratório > Operacionais | Notificação Compulsória | `/KlinikosNet/Laboratorio/Relatorios/Operacionais.aspx?id=4` |
|  | rel-proprio |  > Relatórios > Laboratório > Operacionais | Promessa de Entrega | `/KlinikosNet/Laboratorio/Relatorios/Operacionais.aspx?id=0` |
|  | rel-proprio |  > Relatórios > Laboratório > Operacionais | Unidade Solicitante | `/KlinikosNet/Laboratorio/Relatorios/Operacionais.aspx?id=5` |

### Radiologia (14)

| parrel | tipo | caminho no menu | rótulo | URL |
|---|---|---|---|---|
| 174 | parametro |  > Relatórios > Estatísticas | Clínica e Exame | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=174&Modulo=RADIOLOGIA` |
| 697 | parametro |  > Relatórios > Estatísticas | Estatística Analítico Unidade Referenciada | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=697&Modulo=RADIOLOGIA` |
| 696 | parametro |  > Relatórios > Estatísticas | Estatística Consolidado Unidade Referenciada | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=696&Modulo=RADIOLOGIA` |
| 176 | parametro |  > Relatórios > Estatísticas | Estatística de Produção | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=176&Modulo=RADIOLOGIA` |
| 179 | parametro |  > Relatórios > Estatísticas | Por Hora | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=179&Modulo=RADIOLOGIA` |
| 188 | parametro |  > Relatórios > Estatísticas | Produção Diária de Exame | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=188&Modulo=RADIOLOGIA` |
| 784 | parametro |  > Relatórios > Estatísticas | Quantitativo Exames de Imagem e Radiologia | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=784&origem=0` |
| 804 | parametro |  > Relatórios > Estatísticas | Relatório Mensal de Exames | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=804&Modulo=RADIOLOGIA` |
| 751 | rel-proprio |  > Relatórios > Estatísticas | Relatório de Laudos por Profissional | `/KlinikosNet/Radiologia/Relatorios/Radiologicos.aspx?parrel=751&origem=0` |
| 479 | parametro |  > Relatórios > Exame Radiológico | Agenda Radiológica Aberta | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=479&Modulo=RADIOLOGIA` |
| 171 | parametro |  > Relatórios > Exame Radiológico | Consumo de Filmes | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=171&Modulo=RADIOLOGIA` |
| 481 | parametro |  > Relatórios > Exame Radiológico | Pedido e Agenda | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=481&Modulo=RADIOLOGIA` |
| 480 | parametro |  > Relatórios > Exame Radiológico | Pedido e Agenda - Sintético | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=480&Modulo=RADIOLOGIA` |
| 182 | parametro |  > Relatórios > Exame Radiológico | Técnicos e Filmes | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=182&Modulo=RADIOLOGIA` |

### UPA (75)

| parrel | tipo | caminho no menu | rótulo | URL |
|---|---|---|---|---|
| 791 | parametro |  > Gestão | Eventos Clínicos | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=791&Modulo=UPA` |
| 788 | rptview |  > Gestão | Profissionais sem CNS | `/KlinikosNet/Relatorios/rptView.aspx?parrel=788&parNum=1&par1=unidDef` |
| 815 | relatorio |  > Relatórios > Consolidado | Atendimento Nominal por CID | `/KlinikosNet/UPA/Relatorios/Relatorio.aspx?parrel=815&origem=5` |
| 834 | relatorio |  > Relatórios > Consolidado | Atendimentos encerrados por evasão | `/KlinikosNet/UPA/Relatorios/Relatorio.aspx?parrel=834&origem=0` |
| 550 | relatorio |  > Relatórios > Consolidado | Censo Diário da Urgência e Emergência | `/KlinikosNet/UPA/Relatorios/Relatorio.aspx?parrel=550&origem=3` |
| 711 | relatorio |  > Relatórios > Consolidado | Classificação de Risco, Sexo e Faixa Etária | `/KlinikosNet/UPA/Relatorios/Relatorio.aspx?parrel=711&origem=0` |
| 683 | relatorio |  > Relatórios > Consolidado | Como Chegou | `/KlinikosNet/UPA/Relatorios/Relatorio.aspx?parrel=683&origem=0` |
| 843 | relatorio |  > Relatórios > Consolidado | Escalas BRADEN, FUGULIN e MORSE | `/KlinikosNet/UPA/Relatorios/Relatorio.aspx?parrel=843&origem=0` |
| 720 | relatorio |  > Relatórios > Consolidado | Ficha de Notificação Compulsória | `/KlinikosNet/UPA/Relatorios/Relatorio.aspx?parrel=720&origem=0` |
| 763 | relatorio |  > Relatórios > Consolidado | Historico Eventos da Fila | `/KlinikosNet/UPA/Relatorios/Relatorio.aspx?parrel=763&origem=0` |
| 833 | relatorio |  > Relatórios > Consolidado | Histórico de Classificação de Risco | `/KlinikosNet/UPA/Relatorios/Relatorio.aspx?parrel=833&origem=0` |
| 527 | parametro |  > Relatórios > Consolidado | Nominal de Óbito/Chegou Cadáver | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=527&Modulo=UPA` |
| 667 | relatorio |  > Relatórios > Consolidado | Nominal por Classificação de Risco | `/KlinikosNet/UPA/Relatorios/Relatorio.aspx?parrel=667&origem=5` |
| 831 | relatorio |  > Relatórios > Consolidado | Pacientes Atendidos Por Clinica e Risco | `/KlinikosNet/UPA/Relatorios/Relatorio.aspx?parrel=831&origem=5&Modulo=UPA` |
| 483 | relatorio |  > Relatórios > Consolidado | Produção Diária | `/KlinikosNet/UPA/Relatorios/Relatorio.aspx?parrel=483&origem=0` |
| 823 | parametro |  > Relatórios > Consolidado | Produção de Fisioterapia por procedimento e profissional | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=823&Modulo=UPA` |
|  | rel-proprio |  > Relatórios > Consolidado | Reentradas | `/KlinikosNet/UPA/Relatorios/Reentradas.aspx` |
| 816 | relatorio |  > Relatórios > Consolidado | Relatório Justificativa de Antibiótico | `/KlinikosNet/UPA/Relatorios/Relatorio.aspx?parrel=816&origem=0` |
| 529 | relatorio |  > Relatórios > Consolidado | Relatório de Notificações Compulsórias | `/KlinikosNet/UPA/Relatorios/Relatorio.aspx?parrel=529&origem=0` |
| 762 | relatorio |  > Relatórios > Consolidado | Relatório de Pesquisa de Satisfacão | `/KlinikosNet/UPA/Relatorios/Relatorio.aspx?parrel=762&origem=0` |
| 520 | parametro |  > Relatórios > Consolidado | Remoções por destino | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=520&Modulo=UPA` |
| 835 | relatorio |  > Relatórios > Consolidado | Resumo Atendimento | `/KlinikosNet/UPA/Relatorios/Relatorio.aspx?parrel=835&origem=5&Modulo=UPA` |
| 818 | relatorio |  > Relatórios > Consolidado | Tempo Médio da Classificação de Risco por Período | `/KlinikosNet/UPA/Relatorios/Relatorio.aspx?parrel=818&origem=0` |
| 542 | relatorio |  > Relatórios > Emergência > Estatísticos | Atendimento CID e Faixa Etária | `/KlinikosNet/UPA/Relatorios/Relatorio.aspx?parrel=542&origem=3` |
| 544 | relatorio |  > Relatórios > Emergência > Estatísticos | Atendimento CID e Sexo | `/KlinikosNet/UPA/Relatorios/Relatorio.aspx?parrel=544&origem=3` |
| 526 | relatorio |  > Relatórios > Emergência > Estatísticos | Atendimentos por Profissional | `/KlinikosNet/UPA/Relatorios/Relatorio.aspx?parrel=526&origem=3` |
| 539 | relatorio |  > Relatórios > Emergência > Estatísticos | Estatísticas de Emergência | `/KlinikosNet/UPA/Relatorios/Relatorio.aspx?parrel=539&origem=3` |
| 546 | relatorio |  > Relatórios > Emergência > Estatísticos | Estatísticas de Emergência por CID | `/KlinikosNet/UPA/Relatorios/Relatorio.aspx?parrel=546&origem=3` |
| 54 | relatorio |  > Relatórios > Emergência > Estatísticos | Grupo Diagnóstico, Sexo e Faixa Etária | `/KlinikosNet/UPA/Relatorios/Relatorio.aspx?parrel=54&origem=3` |
| 541 | relatorio |  > Relatórios > Emergência > Estatísticos | Percentual Internações Emergência | `/KlinikosNet/UPA/Relatorios/Relatorio.aspx?parrel=541&origem=3` |
| 85 | relatorio |  > Relatórios > Emergência > Estatísticos | Tipo de Saída, Sexo e Faixa Etária | `/KlinikosNet/UPA/Relatorios/Relatorio.aspx?parrel=85&origem=3` |
| 75 | relatorio |  > Relatórios > Emergência > Estatísticos | Óbito por Grupo Diag, Sexo e Tempo Permanêcia | `/KlinikosNet/UPA/Relatorios/Relatorio.aspx?parrel=75&origem=3` |
| 388 | relatorio |  > Relatórios > Emergência > Geo-Estatístico | Atend. Emergência Diagnóstico, Município e Bairro | `/KlinikosNet/UPA/Relatorios/Relatorio.aspx?parrel=388&origem=3` |
| 72 | relatorio |  > Relatórios > Emergência > Geo-Estatístico | Atend. Emergência Município, Bairro e Clínica | `/KlinikosNet/UPA/Relatorios/Relatorio.aspx?parrel=72&origem=3` |
| 74 | relatorio |  > Relatórios > Emergência > Geo-Estatístico | Registro Emergência Município, Bairro e Faixa Etária | `/KlinikosNet/UPA/Relatorios/Relatorio.aspx?parrel=74&origem=3` |
| 677 | relatorio |  > Relatórios > Emergência > Registro | Bol. Pendentes x Realizados | `/KlinikosNet/UPA/Relatorios/Relatorio.aspx?parrel=677&origem=3` |
| 70 | relatorio |  > Relatórios > Emergência > Registro | Registro Mensal de Emergência por Clínica | `/KlinikosNet/UPA/Relatorios/Relatorio.aspx?parrel=70&origem=3` |
| 690 | relatorio |  > Relatórios > Emergência > Registro | Registro Nominal por Ocorrência - Emergência | `/KlinikosNet/UPA/Relatorios/Relatorio.aspx?parrel=690&origem=3` |
| 71 | relatorio |  > Relatórios > Emergência > Registro | Registro por Hora de Emergência por Clínica | `/KlinikosNet/UPA/Relatorios/Relatorio.aspx?parrel=71&origem=3` |
| 802 | parametro |  > Relatórios > Internação | Relatório de Óbito por Locais de Internação | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=802&Modulo=UPA` |
| 100 | parametro |  > Relatórios > Internação > Relatório de Internação | Relatório de Internações Diarias | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=100&Modulo=UPA` |
| 543 | relatorio |  > Relatórios > Urgência > Estatísticos | Atendimento CID e Faixa Etária | `/KlinikosNet/UPA/Relatorios/Relatorio.aspx?parrel=543&origem=5` |
| 66 | parametro |  > Relatórios > Urgência > Estatísticos | Atendimento de Urgência por Tempo de Permanência | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=66&Modulo=UPA` |
| 518 | parametro |  > Relatórios > Urgência > Estatísticos | Atendimentos por Faixa Etária | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=518&Modulo=UPA` |
| 829 | relatorio |  > Relatórios > Urgência > Estatísticos | Atendimentos por MultiProfissional | `/KlinikosNet/UPA/Relatorios/Relatorio.aspx?parrel=829&origem=5` |
| 526 | relatorio |  > Relatórios > Urgência > Estatísticos | Atendimentos por Profissional | `/KlinikosNet/UPA/Relatorios/Relatorio.aspx?parrel=526&origem=5` |
| 63 | relatorio |  > Relatórios > Urgência > Estatísticos | Diagnóstico, Sexo e Faixa Etária | `/KlinikosNet/UPA/Relatorios/Relatorio.aspx?parrel=63&origem=5` |
| 602 | relatorio |  > Relatórios > Urgência > Estatísticos | Diagnósticos mais atendidos - Urgência | `/KlinikosNet/UPA/Relatorios/Relatorio.aspx?parrel=602&Modulo=UPA&origem=5` |
| 484 | parametro |  > Relatórios > Urgência > Estatísticos | Diagnósticos por Clínica | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=484&Modulo=UPA` |
| 540 | relatorio |  > Relatórios > Urgência > Estatísticos | Estatísticas Mensal da Urgência | `/KlinikosNet/UPA/Relatorios/Relatorio.aspx?parrel=540&origem=5` |
| 547 | relatorio |  > Relatórios > Urgência > Estatísticos | Estatísticas da Urgência por CID | `/KlinikosNet/UPA/Relatorios/Relatorio.aspx?parrel=547&origem=5` |
| 523 | parametro |  > Relatórios > Urgência > Estatísticos | Pacientes sem certidão de nascimento | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=523&Modulo=UPA` |
| 524 | parametro |  > Relatórios > Urgência > Estatísticos | Procedimentos Odontológicos Diário | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=524&Modulo=UPA` |
| 525 | parametro |  > Relatórios > Urgência > Estatísticos | Procedimentos Odontológicos Mensal | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=525&Modulo=UPA` |
| 519 | parametro |  > Relatórios > Urgência > Estatísticos | Registros por Clínica | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=519&Modulo=UPA` |
| 830 | relatorio |  > Relatórios > Urgência > Estatísticos | Relatório Evoluções de Multiprofissionais | `/KlinikosNet/UPA/Relatorios/Relatorio.aspx?parrel=830&origem=5` |
| 65 | relatorio |  > Relatórios > Urgência > Estatísticos | Tipo de Saída, Sexo e Faixa Etária | `/KlinikosNet/UPA/Relatorios/Relatorio.aspx?parrel=65&origem=5` |
| 68 | relatorio |  > Relatórios > Urgência > Estatísticos | Óbito por Grupo Diag, Sexo e Tempo Permanêcia | `/KlinikosNet/UPA/Relatorios/Relatorio.aspx?parrel=68&origem=5` |
| 61 | relatorio |  > Relatórios > Urgência > Geo-Estatístico | Atend. Urgência Município, Bairro e Faixa Etária | `/KlinikosNet/UPA/Relatorios/Relatorio.aspx?parrel=61&origem=5` |
| 631 | parametro |  > Relatórios > Urgência > Registro | Atendimentos em Andamento | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=631&Modulo=UPA` |
| 678 | relatorio |  > Relatórios > Urgência > Registro | Bol. Pendentes x Realizados | `/KlinikosNet/UPA/Relatorios/Relatorio.aspx?parrel=678&origem=5` |
|  | rel-proprio |  > Relatórios > Urgência > Registro | Devolução / Descarte | `/KlinikosNet/PostoEnfermagem/Relatorios/DevolucaoDescarte.aspx` |
|  | rel-proprio |  > Relatórios > Urgência > Registro | Estorno de Boletins de Atendimento Médico | `/KlinikosNet/UPA/Relatorios/RelatorioEstornoBAM.aspx` |
| 629 | rptview |  > Relatórios > Urgência > Registro | Fila de Espera | `/KlinikosNet/Relatorios/rptView.aspx?parrel=629&parNum=1&par1=unidDef` |
|  | rel-proprio |  > Relatórios > Urgência > Registro | Medicamento Não Padronizado x Sem Saldo | `/KlinikosNet/UPA/Relatorios/MedicamentoSaldoGrupo.aspx` |
| 630 | rptview |  > Relatórios > Urgência > Registro | Pacientes em Observação | `/KlinikosNet/Relatorios/rptView.aspx?parrel=630&parNum=1&par1=unidDef` |
| 693 | parametro |  > Relatórios > Urgência > Registro | Pedidos de Exames | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=693&Modulo=UPA` |
| 56 | parametro |  > Relatórios > Urgência > Registro | Registro Diário de Urgência por Clínica | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=56&Modulo=UPA` |
| 57 | parametro |  > Relatórios > Urgência > Registro | Registro Mensal de Urgência por Clínica | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=57&Modulo=UPA` |
| 691 | relatorio |  > Relatórios > Urgência > Registro | Registro Nominal por Ocorrência-Urgência | `/KlinikosNet/UPA/Relatorios/Relatorio.aspx?parrel=691&origem=5` |
| 58 | relatorio |  > Relatórios > Urgência > Registro | Registro por Hora de Urgência por Clínica | `/KlinikosNet/UPA/Relatorios/Relatorio.aspx?parrel=58&origem=5` |
| 801 | relatorio |  > Relatórios > Urgência > Registro | Registro por Hora de Urgência por Clínica e Faixa Etária | `/KlinikosNet/UPA/Relatorios/Relatorio.aspx?parrel=801&origem=5` |
| 407 | parametro |  > Relatórios > Urgência > Registro | Relatório de Pacientes Registrados no Dia | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=407&Modulo=UPA` |
| 727 | parametro |  > Relatórios > Urgência > Registro | Relatório de encaminhamento do cidadão | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=727&Modulo=UPA` |
| 716 | parametro |  > Relatórios > Urgência > Registro | Relatório de orientação ao cidadão | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=716&Modulo=UPA` |

### eProntuario (2)

| parrel | tipo | caminho no menu | rótulo | URL |
|---|---|---|---|---|
| 816 | parametro |  > Relatórios | Relatório Justificativa de Antibiótico | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=816&Modulo=PEP` |
| 802 | parametro |  > Relatórios | Relatório de Óbito por Locais de Internação | `/KlinikosNet/Relatorios/ParametroRelatorio.aspx?parrel=802&Modulo=PEP` |

## Telas por módulo

### Acesso (15)

| caminho no menu | rótulo | URL |
|---|---|---|
|  |  | `/KlinikosNet/Acesso/Default.aspx` |
|  | Administração do Sistema | `../Administracao/Default.aspx?mod=001` |
|  | Ambulatório | `../Ambulatorio/Default.aspx?mod=002` |
|  | Cadastro | `../Cadastro/Default.aspx?mod=004` |
|  | Centro Cirúrgico | `../CentroCirurgico/Default.aspx?mod=005` |
|  | Internação | `../Internacao/Default.aspx?mod=009` |
|  | Laboratório | `../Laboratorio/Default.aspx?mod=010` |
|  | PEP | `../eProntuario/Default.aspx?mod=012` |
|  | Radiologia | `../Radiologia/Default.aspx?mod=016` |
|  | Recepção e Portaria | `Default.aspx?mod=013` |
|  | Restrições | `/KlinikosNet/Acesso/RestricaoDeVisitante.aspx` |
|  | Urgência e Emergência | `../UPA/Default.aspx?mod=014` |
|  | Visitante | `/KlinikosNet/Acesso/VisitanteAcompanhante.aspx` |
|  > Ambulancia | Chegada | `/KlinikosNet/Acesso/AmbEntrada.aspx` |
|  > Ambulancia | Saída | `/KlinikosNet/Acesso/AmbSaida.aspx` |

### Administracao (67)

| caminho no menu | rótulo | URL |
|---|---|---|
|  | Administração do Sistema | `Default.aspx?mod=001` |
|  | Parâmetros | `/KlinikosNet/Administracao/Tabelas/UnidHospitalar/UnidadeIII/Parametros.aspx` |
|  | Recepção e Portaria | `../Acesso/Default.aspx?mod=013` |
|  > Ferramentas | Parâmetros de Integração | `/KlinikosNet/Administracao/admsys/ParametrosIntegracao.aspx` |
|  > Ferramentas > Auditoria | Alterações | `/KlinikosNet/Administracao/Auditoria/ConsultaAudit.aspx` |
|  > Ferramentas > Auditoria | Emissão de Relatório | `/KlinikosNet/Administracao/Auditoria/AuditReport.aspx` |
|  > Importação/Exportação > Exportação | CNS | `/KlinikosNet/Administracao/Importacao/Exportacoes.aspx?Tipo=CNS` |
|  > Importação/Exportação > Exportação | Hiperdia | `/KlinikosNet/Administracao/Importacao/Exportacoes.aspx?Tipo=Hiperdia` |
|  > Importação/Exportação > Exportação | SISPrenatal | `/KlinikosNet/Administracao/Importacao/Exportacoes.aspx?Tipo=SISPrenatal` |
|  > Importação/Exportação > Importação | CNES | `/KlinikosNet/Administracao/Importacao/ImportacaoExportacaoHiperdia.aspx` |
|  > Importação/Exportação > Importação | CNS | `/KlinikosNet/Administracao/Importacao/ImportacaoExportacaoCNS.aspx` |
|  > Importação/Exportação > Importação | SIGTAP | `/KlinikosNet/Administracao/Importacao/ImportacaoDatasus.aspx` |
|  > Paciente Rede | Avaliação Cadastro Permanente | `/KlinikosNet/Administracao/tabelas/PacienteRede/PacientePermanenteRede.aspx` |
|  > Paciente Rede | Homologação de Cadastro | `/KlinikosNet/Administracao/tabelas/PacienteRede/AtualizaPacienteHomonimoRede.aspx` |
|  > Paciente Rede | Obrigatoriedade do Cadastro | `/KlinikosNet/Administracao/tabelas/PacienteRede/ObrigatoriedadeCadastroUnidade.aspx` |
|  > Paciente Rede | Política do Cadastro | `/KlinikosNet/Administracao/tabelas/PacienteRede/ParametroCadastroUnidade.aspx` |
|  > Relatórios | Configuração de Impressora | `/KlinikosNet/Share/Impressora/LaudoRede.aspx` |
|  > Segurança | Grupo | `/KlinikosNet/Administracao/Acesso/GrupoAcesso.aspx` |
|  > Segurança | Usuário | `/KlinikosNet/Administracao/Acesso/Usuario.aspx` |
|  > Tabelas > Org. Regional | GER | `/KlinikosNet/Administracao/Tabelas/OrgRegional/GerenciaRegional.aspx` |
|  > Tabelas > Org. Regional | Macro/Micro | `/KlinikosNet/Administracao/Tabelas/OrgRegional/MacroRegiao.aspx` |
|  > Tabelas > Organização Hospitalar | Enfermaria | `/KlinikosNet/Administracao/Tabelas/OrgHospitalar/Enfermaria.aspx` |
|  > Tabelas > Organização Hospitalar | Leito | `/KlinikosNet/Administracao/Tabelas/OrgHospitalar/Leito.aspx` |
|  > Tabelas > Organização Hospitalar | Natureza da Atividade | `/KlinikosNet/Administracao/Tabelas/OrgHospitalar/Divisao.aspx` |
|  > Tabelas > Organização Hospitalar | Posto de Enfermagem | `/KlinikosNet/Administracao/Tabelas/OrgHospitalar/PostoEnfermagem.aspx` |
|  > Tabelas > Organização Hospitalar > Setor | Classificação da Atividade | `/KlinikosNet/Administracao/Tabelas/OrgHospitalar/Setor/SetorClassificacao.aspx` |
|  > Tabelas > Organização Hospitalar > Setor | Integração com Stok | `/KlinikosNet/Administracao/Tabelas/OrgHospitalar/Setor/SetorStok.aspx` |
|  > Tabelas > Organização Hospitalar > Setor | Setores/Execução da Atividade | `/KlinikosNet/Administracao/Tabelas/OrgHospitalar/Setor/SetorUnidade.aspx` |
|  > Tabelas > Outras | CID | `/KlinikosNet/Administracao/Tabelas/Outras/Outras/CID.aspx` |
|  > Tabelas > Outras | Como Chegou | `/KlinikosNet/Administracao/Tabelas/Outras/Outras/ComoChegouUnidade.aspx` |
|  > Tabelas > Outras | Declaração | `/KlinikosNet/Administracao/Tabelas/Outras/Outras/Declaracao.aspx` |
|  > Tabelas > Outras | Estado do Paciente | `/KlinikosNet/Administracao/Tabelas/Outras/Outras/EstadoPaciente.aspx` |
|  > Tabelas > Outras | Exames de Rede | `/KlinikosNet/Administracao/Tabelas/Outras/Outras/ExamesRede.aspx` |
|  > Tabelas > Outras | Feriado | `/KlinikosNet/Administracao/Tabelas/Outras/Outras/Feriado.aspx` |
|  > Tabelas > Outras | Modelo de Classificação de Risco | `/KlinikosNet/Administracao/Tabelas/Outras/Outras/ProtocoloAcolhimento.aspx` |
|  > Tabelas > Outras | Notificação Compulsória - CID 10 | `/KlinikosNet/Administracao/Tabelas/Outras/Outras/CID10.aspx` |
|  > Tabelas > Outras | Pendência | `/KlinikosNet/Administracao/Tabelas/Outras/Outras/Pendencia.aspx` |
|  > Tabelas > Outras | Procedimento | `/KlinikosNet/Administracao/Tabelas/Outras/Outras/Procedimento.aspx` |
|  > Tabelas > Outras | Protocolo de Prescrição | `/KlinikosNet/Administracao/Tabelas/Outras/Outras/PrescricaoPadrao.aspx` |
|  > Tabelas > Outras | Seção | `/KlinikosNet/Administracao/Tabelas/OrgHospitalar/Secao.aspx` |
|  > Tabelas > Outras | Unidade de Referência | `/KlinikosNet/Administracao/Tabelas/Outras/Outras/UnidadeReferencia.aspx` |
|  > Tabelas > Outras | Visitante e Acompanhante | `/KlinikosNet/Administracao/Tabelas/Outras/Outras/VisitanteAcompanhante.aspx` |
|  > Tabelas > Profissional | Lotação do Profissional | `/KlinikosNet/Administracao/Tabelas/Profissional/Lotacao.aspx` |
|  > Tabelas > Profissional | Profissional | `/KlinikosNet/Administracao/Tabelas/Profissional/Profissional.aspx` |
|  > Tabelas > Profissional | Tipo de Profissional | `/KlinikosNet/Administracao/Tabelas/Profissional/ConfiguracaoTipoProfissional.aspx` |
|  > Tabelas > Unidade Hospitalar > Unidade Hospitalar I | Básico | `/KlinikosNet/Administracao/Tabelas/UnidHospitalar/UnidadeI/Basico.aspx` |
|  > Tabelas > Unidade Hospitalar > Unidade Hospitalar I | Complementar | `/KlinikosNet/Administracao/Tabelas/UnidHospitalar/UnidadeI/BasicoComplementar.aspx` |
|  > Tabelas > Unidade Hospitalar > Unidade Hospitalar I | Endereço Complementar | `/KlinikosNet/Administracao/Tabelas/UnidHospitalar/UnidadeI/EndComplementar.aspx` |
|  > Tabelas > Unidade Hospitalar > Unidade Hospitalar I | Identificação | `/KlinikosNet/Administracao/Tabelas/UnidHospitalar/UnidadeI/Identificacao.aspx` |
|  > Tabelas > Unidade Hospitalar > Unidade Hospitalar I | Infraestrutura de Comunicação e Informática | `/KlinikosNet/Administracao/Tabelas/UnidHospitalar/UnidadeI/InfraComInf.aspx` |
|  > Tabelas > Unidade Hospitalar > Unidade Hospitalar I | Quimio-Rádio | `/KlinikosNet/Administracao/Tabelas/UnidHospitalar/UnidadeI/QuimioRadio.aspx` |
|  > Tabelas > Unidade Hospitalar > Unidade Hospitalar II | Diálise I | `/KlinikosNet/Administracao/Tabelas/UnidHospitalar/UnidadeII/DialiseI.aspx` |
|  > Tabelas > Unidade Hospitalar > Unidade Hospitalar II | Diálise II | `/KlinikosNet/Administracao/Tabelas/UnidHospitalar/UnidadeII/DialiseII.aspx` |
|  > Tabelas > Unidade Hospitalar > Unidade Hospitalar II | Equipamentos I | `/KlinikosNet/Administracao/Tabelas/UnidHospitalar/UnidadeII/EquipamentosI.aspx` |
|  > Tabelas > Unidade Hospitalar > Unidade Hospitalar II | Equipamentos II | `/KlinikosNet/Administracao/Tabelas/UnidHospitalar/UnidadeII/EquipamentosII.aspx` |
|  > Tabelas > Unidade Hospitalar > Unidade Hospitalar II | Equipamentos III | `/KlinikosNet/Administracao/Tabelas/UnidHospitalar/UnidadeII/EquipamentosIII.aspx` |
|  > Tabelas > Unidade Hospitalar > Unidade Hospitalar II | Equipamentos IV | `/KlinikosNet/Administracao/Tabelas/UnidHospitalar/UnidadeII/EquipamentosIV.aspx` |
|  > Tabelas > Unidade Hospitalar > Unidade Hospitalar II | Hemoterapia I | `/KlinikosNet/Administracao/Tabelas/UnidHospitalar/UnidadeII/HemoterapiaI.aspx` |
|  > Tabelas > Unidade Hospitalar > Unidade Hospitalar II | Hemoterapia II | `/KlinikosNet/Administracao/Tabelas/UnidHospitalar/UnidadeII/HemoterapiaII.aspx` |
|  > Tabelas > Unidade Hospitalar > Unidade Hospitalar II | Residência | `/KlinikosNet/Administracao/Tabelas/UnidHospitalar/UnidadeII/Residencia.aspx` |
|  > Tabelas > Unidade Hospitalar > Unidade Hospitalar II | Vínculo Cooperativo | `/KlinikosNet/Administracao/Tabelas/UnidHospitalar/UnidadeII/VinculoCooperativo.aspx` |
|  > Tabelas > Unidade Hospitalar > Unidade Hospitalar II | Vínculo Profissional | `/KlinikosNet/Administracao/Tabelas/UnidHospitalar/UnidadeII/VinculoProfissional.aspx` |
|  > Tabelas > Unidade Hospitalar > Unidade Hospitalar III | Leitos | `/KlinikosNet/Administracao/Tabelas/UnidHospitalar/UnidadeIII/Leitos.aspx` |
|  > Tabelas > Unidade Hospitalar > Unidade Hospitalar III | Mantenedora | `/KlinikosNet/Administracao/Tabelas/UnidHospitalar/UnidadeIII/Mantenedora.aspx` |
|  > Tabelas > Unidade Hospitalar > Unidade Hospitalar III | Módulo Conjunto | `/KlinikosNet/Administracao/Tabelas/UnidHospitalar/UnidadeIII/Instalacoes.aspx` |
|  > Tabelas > Unidade Hospitalar > Unidade Hospitalar III | Procedimentos x Serviço/Classificação | `/KlinikosNet/Administracao/Tabelas/UnidHospitalar/UnidadeIII/ProcedimentoServicoClassificacao.aspx` |
|  > Tabelas > Unidade Hospitalar > Unidade Hospitalar III | Serviço Referência | `/KlinikosNet/Administracao/Tabelas/UnidHospitalar/UnidadeIII/UnidadeServicoReferencia.aspx` |

### Ambulatorio (31)

| caminho no menu | rótulo | URL |
|---|---|---|
|  |  | `/KlinikosNet/Ambulatorio/Default.aspx` |
|  | Alta Administrativa | `/KlinikosNet/Ambulatorio/AltaAdministrativa.aspx` |
|  | Ambulatório | `Default.aspx?mod=002` |
|  | Autorização | `/KlinikosNet/Ambulatorio/Autorizacao.aspx` |
|  | Cadastro | `/KlinikosNet/Cadastro/Paciente/CadastroBasico.aspx` |
|  | Consulta Encaminhamentos à Regulação | `/KlinikosNet/Ambulatorio/ConsultaSolicitacoesSer.aspx` |
|  | Encaixe | `/KlinikosNet/Ambulatorio/Encaixe.aspx` |
|  | Reagendamento | `/KlinikosNet/Ambulatorio/Reagendamento.aspx` |
|  > Agendamento | Atendimento | `/KlinikosNet/Ambulatorio/Agendamento/AtenderChamada.aspx` |
|  > Agendamento | CheckIn | `/KlinikosNet/Ambulatorio/Agendamento/CheckInLista.aspx` |
|  > Agendamento | CheckOut | `/KlinikosNet/Share/CheckOut/CheckOut.aspx` |
|  > Agendamento | CheckOut Exame | `/KlinikosNet/Ambulatorio/Agendamento/CheckOutExame.aspx` |
|  > Agendamento | Consulta | `/KlinikosNet/Ambulatorio/Agendamento/AgendaConsulta.aspx` |
|  > Agendamento | Fila de Espera | `/KlinikosNet/Ambulatorio/Agendamento/FilaEspera.aspx` |
|  > Agendamento > Exame | Exame Diagnose | `/KlinikosNet/Ambulatorio/Agendamento/PedidoExame.aspx?TipoExame=9` |
|  > Agendamento > Exame | Exame Laboratorial | `/KlinikosNet/Ambulatorio/Agendamento/PedidoExame.aspx?TipoExame=7` |
|  > Agendamento > Exame | Exame Radiológico | `/KlinikosNet/Ambulatorio/Agendamento/PedidoExame.aspx?TipoExame=8` |
|  > Parâmetros de Agenda | Bloqueia/Exclui | `/KlinikosNet/Ambulatorio/ParametrosAgenda/BloqueiaExclui.aspx` |
|  > Parâmetros de Agenda | Copia Agenda | `/KlinikosNet/Ambulatorio/ParametrosAgenda/CopiaParametrosAgenda.aspx` |
|  > Parâmetros de Agenda | Desbloquear | `/KlinikosNet/Ambulatorio/ParametrosAgenda/Desbloqueia.aspx` |
|  > Parâmetros de Agenda | Gera Agenda | `/KlinikosNet/Ambulatorio/ParametrosAgenda/ParametrosAgenda.aspx` |
|  > Parâmetros de Agenda | Permissão | `/KlinikosNet/Ambulatorio/ParametrosAgenda/Permissao.aspx` |
|  > Parâmetros de Agenda > Tabelas | Clinica de Referência | `/KlinikosNet/Ambulatorio/OutrasTabelas/ClinicaReferencia.aspx` |
|  > Parâmetros de Agenda > Tabelas | Correlação Especialidade Interconsulta | `/KlinikosNet/Ambulatorio/OutrasTabelas/EspecialidadeInterconsulta.aspx` |
|  > Parâmetros de Agenda > Tabelas | Correlação Especialidade SER2 x Klinikos | `/KlinikosNet/Administracao/admsys/ParametroEspecialidadeSER2.aspx` |
|  > Parâmetros de Agenda > Tabelas | Exames de Diagnose | `/KlinikosNet/Ambulatorio/ParametrosAgenda/Exames/Diagnose.aspx` |
|  > Parâmetros de Agenda > Tabelas | Motivo de Autorização | `/KlinikosNet/Ambulatorio/OutrasTabelas/MotivoAutorizacao.aspx` |
|  > Parâmetros de Agenda > Tabelas | Motivo de Bloqueio | `/KlinikosNet/Ambulatorio/OutrasTabelas/MotivoBloqueio.aspx` |
|  > Parâmetros de Agenda > Tabelas | Parâmetro Usa Check-in | `/KlinikosNet/Ambulatorio/ParametrosAgenda/Parametros.aspx` |
|  > Parâmetros de Agenda > Tabelas | Profissional de Referência | `/KlinikosNet/Ambulatorio/OutrasTabelas/ProfissionalReferencia.aspx` |
|  > Parâmetros de Agenda > Tabelas | Texto Padrão | `/KlinikosNet/Ambulatorio/ParametrosAgenda/TextoPadrao.aspx` |

### Cadastro (10)

| caminho no menu | rótulo | URL |
|---|---|---|
|  |  | `/KlinikosNet/Cadastro/Default.aspx` |
|  | Cadastro | `Default.aspx?mod=004` |
|  | Cadastro Incompleto | `/KlinikosNet/Cadastro/CadastroIncompleto.aspx` |
|  | Histórico do Paciente | `/KlinikosNet/Cadastro/ResumoProntuario.aspx` |
|  | Movimentação | `/KlinikosNet/Cadastro/MovProntuario.aspx` |
|  | Unificação de Prontuário | `/KlinikosNet/Cadastro/Homonimos/AtualizaCadastroPaciente.aspx` |
|  > Cadastro > Cadastro Completo | Dados do CNS | `/KlinikosNet/Cadastro/Paciente/DadosCNS.aspx` |
|  > Cadastro > Cadastro Completo | Família | `/KlinikosNet/Cadastro/Paciente/Familia.aspx` |
|  > Cadastro > Cadastro Completo | Informações Complementares | `/KlinikosNet/Cadastro/Paciente/InformacoesComplementares.aspx` |
|  > Cadastro > Cadastro Completo | Obituário | `/KlinikosNet/Cadastro/Paciente/Obituario.aspx` |

### CentroCirurgico (32)

| caminho no menu | rótulo | URL |
|---|---|---|
|  |  | `/KlinikosNet/CentroCirurgico/Default.aspx` |
|  | Centro Cirúrgico | `Default.aspx?mod=005` |
|  | Cirurgia | `/KlinikosNet/CentroCirurgico/Cirurgia.aspx` |
|  | Registro de Parto | `/KlinikosNet/CentroCirurgico/RegistroDeParto.aspx` |
|  > Pré-Anestésico | Avaliação | `/KlinikosNet/CentroCirurgico/PreAnestesico/Avaliacao.aspx` |
|  > Pré-Anestésico | Exames | `/KlinikosNet/CentroCirurgico/PreAnestesico/Exames.aspx` |
|  > Solicitação | Agenda Cirúrgica | `/KlinikosNet/CentroCirurgico/Solicitacao/AgendaCirurgica.aspx` |
|  > Solicitação | Equipamento Cirúrgico | `/KlinikosNet/CentroCirurgico/Solicitacao/EquipamentoCirurgico.aspx` |
|  > Solicitação | Equipe Cirúrgica | `/KlinikosNet/CentroCirurgico/Solicitacao/EquipeCirurgica.aspx` |
|  > Solicitação | Material Cirúrgico | `/KlinikosNet/CentroCirurgico/Solicitacao/MaterialCirurgico.aspx` |
|  > Solicitação | Pedido | `/KlinikosNet/CentroCirurgico/Solicitacao/Pedido.aspx` |
|  > Tabelas > Outras | Cancelamento | `/KlinikosNet/CentroCirurgico/Tabelas/Outras/Cancelamento.aspx` |
|  > Tabelas > Outras | Contaminação | `/KlinikosNet/CentroCirurgico/Tabelas/Outras/Contaminacao.aspx` |
|  > Tabelas > Outras | Kit Cirúrgico | `/KlinikosNet/CentroCirurgico/Tabelas/Outras/KitCirurgico.aspx` |
|  > Tabelas > Outras | Kit Material | `/KlinikosNet/CentroCirurgico/Tabelas/Outras/KitMaterial.aspx` |
|  > Tabelas > Outras | Parâmetros | `/KlinikosNet/CentroCirurgico/Tabelas/Outras/Parametros.aspx` |
|  > Tabelas > Pré-Anestésico | Exames | `/KlinikosNet/CentroCirurgico/Tabelas/PreAnestesico/Exames.aspx` |
|  > Tabelas > Pré-Anestésico | Item | `/KlinikosNet/CentroCirurgico/Tabelas/PreAnestesico/Item.aspx` |
|  > Tabelas > Pré-Anestésico | Tipo | `/KlinikosNet/CentroCirurgico/Tabelas/PreAnestesico/Tipo.aspx` |
|  > Tabelas > Pós-Anestésico | Categoria Cirúrgica | `/KlinikosNet/CentroCirurgico/Tabelas/PosAnestesico/Categoria.aspx` |
|  > Tabelas > Pós-Anestésico | Mobilidade | `/KlinikosNet/CentroCirurgico/Tabelas/PosAnestesico/Mobilidade.aspx` |
|  > Tabelas > Pós-Anestésico | Nível de Circulação | `/KlinikosNet/CentroCirurgico/Tabelas/PosAnestesico/Circulacao.aspx` |
|  > Tabelas > Pós-Anestésico | Nível de Coloração | `/KlinikosNet/CentroCirurgico/Tabelas/PosAnestesico/Coloracao.aspx` |
|  > Tabelas > Pós-Anestésico | Nível de Consciência | `/KlinikosNet/CentroCirurgico/Tabelas/PosAnestesico/Consciencia.aspx` |
|  > Tabelas > Pós-Anestésico | Nível de Respiração | `/KlinikosNet/CentroCirurgico/Tabelas/PosAnestesico/Respiracao.aspx` |
|  > Tabelas > Solicitação | Centro Cirúrgico | `/KlinikosNet/CentroCirurgico/Tabelas/Solicitacao/CentroCirurgico.aspx` |
|  > Tabelas > Solicitação | Classe Cirúrgica | `/KlinikosNet/CentroCirurgico/Tabelas/Solicitacao/Classe.aspx` |
|  > Tabelas > Solicitação | Função | `/KlinikosNet/CentroCirurgico/Tabelas/Solicitacao/Funcao.aspx` |
|  > Tabelas > Solicitação | Material | `/KlinikosNet/CentroCirurgico/Tabelas/Solicitacao/Material.aspx` |
|  > Tabelas > Solicitação | Porte Cirúrgico | `/KlinikosNet/CentroCirurgico/Tabelas/Solicitacao/Porte.aspx` |
|  > Tabelas > Solicitação | Sala Cirúrgica | `/KlinikosNet/CentroCirurgico/Tabelas/Solicitacao/SalaCirurgica.aspx` |
|  > Tabelas > Solicitação | Tipo de Anestesia | `/KlinikosNet/CentroCirurgico/Tabelas/Solicitacao/Tipo.aspx` |

### Internacao (14)

| caminho no menu | rótulo | URL |
|---|---|---|
|  |  | `/KlinikosNet/Internacao/Default.aspx` |
|  | Entrega de Pertences | `/KlinikosNet/Internacao/EntregaPertences.aspx` |
|  | Internação | `Default.aspx?mod=009` |
|  > Cegonha | Controle de Preenchimento | `/KlinikosNet/Internacao/Cegonha/ControlePreenchimento.aspx` |
|  > Cegonha | Formulário | `/KlinikosNet/Internacao/Cegonha/CentroObstetrico.aspx` |
|  > Internação | Clínica Atual | `/KlinikosNet/Internacao/ClinicaAtual.aspx` |
|  > Internação | Hospital/Dia | `/KlinikosNet/Internacao/HospitalDia.aspx` |
|  > Internação | Informações | `/KlinikosNet/Internacao/Informacoes.aspx` |
|  > Internação | Internação e Alta | `/KlinikosNet/Internacao/Internacao.aspx` |
|  > Internação | Procedimentos | `/KlinikosNet/Internacao/Procedimentos.aspx` |
|  > Leitos | Mapa de Leitos | `/KlinikosNet/Share/Leitos/MapaLeitos.aspx` |
|  > Leitos | Reprocessamento de Censo | `/KlinikosNet/Internacao/Leitos/ReprocessamentoDoCenso.aspx` |
|  > Pedidos Internação | Pedido | `/KlinikosNet/Share/PedidoInternacao/PedidoInternacao.aspx` |
|  > Pedidos Internação | Solicitação de Internação | `/KlinikosNet/Share/PedidoInternacao/SolicitacaoInternacao.aspx` |

### Laboratorio (37)

| caminho no menu | rótulo | URL |
|---|---|---|
|  |  | `/KlinikosNet/Laboratorio/Default.aspx` |
|  | Laboratório | `Default.aspx?mod=010` |
|  | Movimentar Laboratório Externo | `/KlinikosNet/Laboratorio/MovimentacaoExame.aspx` |
|  | Requisição | `/KlinikosNet/Laboratorio/Requisicao.aspx` |
|  > Monitoração | Exames | `/KlinikosNet/Laboratorio/Monitoracao/Exames.aspx` |
|  > Monitoração | Exames em Andamento | `/KlinikosNet/Laboratorio/Monitoracao/ExamesAndamento.aspx` |
|  > Monitoração | Log Geração de Laudos | `/KlinikosNet/Laboratorio/Monitoracao/GeracaoLaudoLog.aspx` |
|  > Paciente Referenciado | Cadastro | `/KlinikosNet/Share/PacienteExterno.aspx?Modulo=7` |
|  > Parâmetros | Fórmulas | `/KlinikosNet/Laboratorio/Parametros/Formulas.aspx` |
|  > Parâmetros | Integrar Exames Laboratoriais | `/KlinikosNet/Laboratorio/Parametros/Servico.aspx` |
|  > Parâmetros | Laboratório | `/KlinikosNet/Laboratorio/Parametros/Laboratorio.aspx` |
|  > Parâmetros > Impressora | Código de Barras | `/KlinikosNet/Laboratorio/Parametros/CodigoBarras.aspx` |
|  > Parâmetros > Impressora | Vinculação/Desvinculação | `/KlinikosNet/Laboratorio/Parametros/Vinculacao.aspx` |
|  > Resultado | Gerar Mapa | `/KlinikosNet/Laboratorio/Resultado/GerarMapa.aspx` |
|  > Resultado | Lançar | `/KlinikosNet/Laboratorio/Resultado/Lancar.aspx` |
|  > Resultado | Liberação | `/KlinikosNet/Laboratorio/Resultado/Liberacao.aspx` |
|  > Resultado | Ver Mapa | `/KlinikosNet/Laboratorio/Resultado/VerMapa.aspx` |
|  > Tabelas > Laboratório > Configurações | Equipamento | `/KlinikosNet/Laboratorio/Tabelas/Laboratorio/Configuracoes/Equipamento.aspx` |
|  > Tabelas > Laboratório > Configurações | Fabricante | `/KlinikosNet/Laboratorio/Tabelas/Laboratorio/Configuracoes/Fabricante.aspx` |
|  > Tabelas > Laboratório > Configurações | Material | `/KlinikosNet/Laboratorio/Tabelas/Laboratorio/Configuracoes/Material.aspx` |
|  > Tabelas > Laboratório > Configurações | Método | `/KlinikosNet/Laboratorio/Tabelas/Laboratorio/Configuracoes/Metodo.aspx` |
|  > Tabelas > Laboratório > Configurações | Pacote de Exames | `/KlinikosNet/Laboratorio/Tabelas/Laboratorio/Configuracoes/PacoteExames.aspx` |
|  > Tabelas > Laboratório > Configurações | Unidade de Medida | `/KlinikosNet/Laboratorio/Tabelas/Laboratorio/Configuracoes/UnidadeMedida.aspx` |
|  > Tabelas > Laboratório > Exames | Elemento | `/KlinikosNet/Laboratorio/Tabelas/Laboratorio/Exames/Elemento.aspx` |
|  > Tabelas > Laboratório > Exames | Equipamento/Exame | `/KlinikosNet/Laboratorio/Tabelas/Laboratorio/Exames/EquipamentoExame.aspx` |
|  > Tabelas > Laboratório > Exames | Exame/Elemento | `/KlinikosNet/Laboratorio/Tabelas/Laboratorio/Exames/ExameElemento.aspx` |
|  > Tabelas > Laboratório > Exames | Exames | `/KlinikosNet/Laboratorio/Tabelas/Laboratorio/Exames/Exame.aspx` |
|  > Tabelas > Laboratório > Exames | Texto Padrão | `/KlinikosNet/Laboratorio/Tabelas/Laboratorio/Exames/TextoPadrao.aspx` |
|  > Tabelas > Laboratório > Exames | Valor Referência | `/KlinikosNet/Laboratorio/Tabelas/Laboratorio/Exames/ValorReferencia.aspx` |
|  > Tabelas > Sistema | Destino | `/KlinikosNet/Laboratorio/Tabelas/Sistema/Destino.aspx` |
|  > Tabelas > Sistema | Layout Mapa | `/KlinikosNet/Laboratorio/Tabelas/Sistema/LayoutMapa.aspx` |
|  > Tabelas > Sistema | Local Coleta | `/KlinikosNet/Laboratorio/Tabelas/Sistema/LocalColeta.aspx` |
|  > Tabelas > Sistema | Motivo de Exclusão Requisição | `/KlinikosNet/Laboratorio/Tabelas/Sistema/MotivoExclusao.aspx` |
|  > Tabelas > Sistema | Pacote | `/KlinikosNet/Laboratorio/Tabelas/Sistema/Pacote.aspx` |
|  > Tabelas > Sistema | Profissional Solicitante | `/KlinikosNet/Share/ProfissionalSolicitante.aspx?Modulo=LABORATORIO` |
|  > Tabelas > Sistema | Seção/Usuário | `/KlinikosNet/Laboratorio/Tabelas/Sistema/SecaoUsuario.aspx` |
|  > Tabelas > Sistema | Unidade Solicitante | `/KlinikosNet/Share/UnidadeSolicitante.aspx?Modulo=LABORATORIO` |

### Radiologia (15)

| caminho no menu | rótulo | URL |
|---|---|---|
|  |  | `/KlinikosNet/Radiologia/Default.aspx` |
|  | Entrega | `/KlinikosNet/Radiologia/Entrega.aspx` |
|  | Laudos | `/KlinikosNet/Radiologia/Laudos.aspx` |
|  | Paciente Referenciado | `/KlinikosNet/share/PacienteExterno.aspx?Modulo=8` |
|  | Radiologia | `Default.aspx?mod=016` |
|  | Solicitações | `/KlinikosNet/Radiologia/Solicitacoes.aspx` |
|  > Parâmetros | Integrar Exames de Imagem | `/KlinikosNet/Radiologia/Parametros/Servico.aspx` |
|  > Tabelas | Equipamentos | `/KlinikosNet/Radiologia/Tabelas/Equipamentos.aspx` |
|  > Tabelas | Exames | `/KlinikosNet/Radiologia/Tabelas/Exames.aspx` |
|  > Tabelas | Filmes | `/KlinikosNet/Radiologia/Tabelas/Filmes.aspx` |
|  > Tabelas | Grupo de Exames / Usuário | `/KlinikosNet/Radiologia/Tabelas/GrupoExamesUsuario.aspx` |
|  > Tabelas | Grupos de Exames | `/KlinikosNet/Radiologia/Tabelas/GrupoExames.aspx` |
|  > Tabelas | Profissional Solicitante | `/KlinikosNet/Share/ProfissionalSolicitante.aspx?Modulo=RADIOLOGIA` |
|  > Tabelas | Texto Padrão | `/KlinikosNet/Radiologia/Tabelas/TextoPadrao.aspx` |
|  > Tabelas | Unidade Solicitante | `/KlinikosNet/Share/UnidadeSolicitante.aspx?Modulo=RADIOLOGIA` |

### UPA (33)

| caminho no menu | rótulo | URL |
|---|---|---|
|  | Assistente Social | `/KlinikosNet/UPA/ServicoSocial.aspx` |
|  | Multiprofissional / Evolução | `/KlinikosNet/UPA/AtendimentoMedico.aspx?MultiProfissional=1` |
|  | Remoção | `/KlinikosNet/UPA/Remocao.aspx` |
|  | Urgência e Emergência | `Default.aspx?mod=014` |
|  > Administração | Cadastro Básico do Profissional | `/KlinikosNet/UPA/CadastroBasicoProfissional.aspx` |
|  > Administração | Contingência | `/KlinikosNet/UPA/Retaguarda.aspx` |
|  > Administração | Entrega de Pertences | `/KlinikosNet/UPA/EntregaPertences.aspx` |
|  > Administração | Fila | `/KlinikosNet/UPA/AdmFila.aspx` |
|  > Administração | Monitoração de Exames | `/KlinikosNet/UPA/RequisicaoExternaAdmin.aspx` |
|  > Administração | Regulação de Leitos | `/KlinikosNet/UPA/RegulacaoDeLeitos.aspx` |
|  > Administração | SINAN | `/KlinikosNet/UPA/AdmCompulsoria.aspx?Acesso=Administracao` |
|  > Administração > Gestão de Qualidade | Pesquisa Satisfação dos Usuários | `/KlinikosNet/UPA/PesquisaSatisfacao.aspx` |
|  > Administração > Gestão de Qualidade | Plano de Educação Permanente | `/KlinikosNet/UPA/EducacaoPermanente.aspx` |
|  > Administração > Gestão de Qualidade | Resolubilidade da Ouvidoria | `/KlinikosNet/UPA/Ouvidoria.aspx` |
|  > Administração > Gestão de Qualidade | Índice de Satisfação dos Usuários com a Unidade de Saúde | `/KlinikosNet/UPA/IndiceSatisfacaoUsuarios.aspx` |
|  > Administração > Indicadores | Parâmetros | `/KlinikosNet/UPA/ParametroIndicadorSerieHistorica.aspx` |
|  > Administração > Indicadores | Reprocessar Indicadores | `/KlinikosNet/UPA/IndicadorDesempenhoReprocessamento.aspx` |
|  > Administração > Indicadores | Série Histórica | `/KlinikosNet/UPA/IndicadorDesempenhoSerieHistorica.aspx` |
|  > Atendimento | Médico | `/KlinikosNet/UPA/AtendimentoMedico.aspx` |
|  > Atendimento | Odontologia | `/KlinikosNet/UPA/AtendimentoMedicoOdonto.aspx?Odontologia=True` |
|  > Atendimento | Prescrição / Receita Favorita | `/KlinikosNet/UPA/PrescricaoReceita.aspx` |
|  > Atendimento | SINAN | `/KlinikosNet/UPA/AdmCompulsoria.aspx?Acesso=Atendimento` |
|  > Baixa de Boletim | Emergência | `/KlinikosNet/Share/CheckOut/CheckOut.aspx?origem=3` |
|  > Baixa de Boletim | Urgência | `/KlinikosNet/Share/CheckOut/CheckOut.aspx?origem=5` |
|  > Fisioterapia | Registros de Fisioterapia | `/KlinikosNet/Share/Fisioterapia/RegistrosdeFisioterapia.aspx?Modulo=SPA` |
|  > Leitos | Mapa de Leitos | `/KlinikosNet/Share/Leitos/MapaLeitos.aspx?Modulo=E` |
|  > Posto de Enfermagem | Coleta de Exames | `/KlinikosNet/UPA/requisicaoexterna.aspx` |
|  > Posto de Enfermagem | Pedidos do Posto | `/KlinikosNet/PostoEnfermagem/Pedido.aspx` |
|  > Posto de Enfermagem | Plano Terapêutico | `/KlinikosNet/Share/Prescricao/PlanoTerapeutico.aspx` |
|  > Posto de Enfermagem | Registros de Enfermagem | `/KlinikosNet/UPA/Enfermagem.aspx` |
|  > Pré-Atendimento | Classificação de Risco | `/KlinikosNet/UPA/ClassificacaoDeRisco.aspx` |
|  > Pré-Atendimento | Emergência | `/KlinikosNet/UPA/Registro.aspx?emergencia=True` |
|  > Pré-Atendimento | Urgência | `/KlinikosNet/UPA/Registro.aspx` |

### eProntuario (17)

| caminho no menu | rótulo | URL |
|---|---|---|
|  | PEP | `Default.aspx?mod=012` |
|  > Ambulatório | Triagem / Pré-Consulta | `/KlinikosNet/eProntuario/ConsultaDeEnfermagem.aspx?window=true` |
|  > Ambulatório > Consulta | Iniciar Consulta | `/KlinikosNet/eProntuario/AtendimentoMedico.aspx?window=true` |
|  > Ambulatório > Consulta | Prescrição / Receita Favorita | `/KlinikosNet/UPA/PrescricaoReceita.aspx?ambulatorio=true` |
|  > Internação | Assistente Social | `/KlinikosNet/eProntuario/Internacao/ServicoSocial.aspx?window=true` |
|  > Internação > Enfermagem | Coleta de Exames | `/KlinikosNet/PostoEnfermagem/requisicaoexterna.aspx?Modulo=INTERNACAO` |
|  > Internação > Enfermagem | Consulta Pedidos do Posto | `/KlinikosNet/PostoEnfermagem/ConsultaPedidoPosto.aspx` |
|  > Internação > Enfermagem | Devolução/Descarte do Posto | `/KlinikosNet/PostoEnfermagem/DevolucaoDescartePosto.aspx` |
|  > Internação > Enfermagem | Dispositivos/Bundles | `/KlinikosNet/PostoEnfermagem/Bundle.aspx?Modulo=PEP` |
|  > Internação > Enfermagem | Mapa de Leitos | `/KlinikosNet/Share/Leitos/ObservacaoLeito.aspx` |
|  > Internação > Enfermagem | Pedidos do Posto | `/KlinikosNet/PostoEnfermagem/Pedido.aspx?Modulo=PEP` |
|  > Internação > Enfermagem | Plano Terapêutico | `/KlinikosNet/Share/Prescricao/PlanoTerapeutico.aspx?Modulo=PEP` |
|  > Internação > Enfermagem | Registros de Enfermagem | `/KlinikosNet/PostoEnfermagem/FilaEnfermagemPEPWindow.aspx?Modulo=PEP` |
|  > Internação > Fisioterapia | Registros de Fisioterapia | `/KlinikosNet/Share/Fisioterapia/RegistrosdeFisioterapia.aspx?Modulo=PEP` |
|  > Internação > Médico | Atendimento | `/KlinikosNet/eProntuario/Internacao/Admissao.aspx?window=true` |
|  > Internação > Médico | Evolução Multiprofissional | `/KlinikosNet/eProntuario/Internacao/Admissao.aspx?MultiProfissional=1` |
|  > Internação > Médico | Prescrição / Receita Favorita | `/KlinikosNet/UPA/PrescricaoReceita.aspx?Internacao=true` |
