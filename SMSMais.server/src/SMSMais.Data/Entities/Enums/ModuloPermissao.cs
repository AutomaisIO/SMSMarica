namespace SMSMais.Data.Entities.Enums;

/// <summary>
/// Módulos do sistema que aceitam controle de acesso. O valor inteiro
/// é estável (persistido) — não renumerar valores existentes.
/// </summary>
public enum ModuloPermissao
{
    Pacientes = 1,
    Unidades = 2,
    Veiculos = 3,
    Motoristas = 4,
    Usuarios = 5,
    TiposTratamento = 6,
    Perfis = 7,
    Tratamentos = 8,
    Translados = 9,
    Rastreamento = 10,
    Avaliacoes = 11,
    Pacs = 12,
    Medicos = 13,
    Laudos = 14,
    LaudosTemplates = 15,
    /// <summary>Solicitações reguladas — exames de imagem, consultas, laboratório… numa lista só
    /// (ADR-0021): listar, ver o detalhe, autorizar a chegada, cancelar, comunicar.</summary>
    Solicitacoes = 16,
    TiposExame = 17,
    ProcedimentosSigtap = 18,

    /// <summary>Usar o assistente de IA (fazer perguntas em linguagem natural).</summary>
    Inteligencia = 19,

    /// <summary>Configurar o módulo IA: token do provedor e bases de dados.</summary>
    InteligenciaConfiguracao = 20,

    /// <summary>Governar o aprendizado da IA: ver o log de melhorias e remover instruções aprendidas.</summary>
    InteligenciaAprendizado = 21,

    /// <summary>Especialidades médicas (tabela de referência do agendamento).</summary>
    Especialidades = 22,

    /// <summary>Agendas dos médicos e marcação de consultas (agendamento local).</summary>
    Agendamentos = 23,

    /// <summary>Consultar o feed de leitura do SISREG (solicitações/marcações).</summary>
    Sisreg = 24,

    /// <summary>Configurar a integração SISREG: credenciais, UF/município, centrais reguladoras.</summary>
    SisregConfiguracao = 25,

    /// <summary>Equipamentos das unidades (recurso agendável para exames de imagem).</summary>
    Equipamentos = 26,

    /// <summary>Sincronização/importação de prontuários de PEPs (Salux e futuros) para o hub FHIR.</summary>
    SincronizacaoPep = 27,

    /// <summary>Gerenciar tokens de API (chaves de serviço para integrações externas, ex.: CentralIA).</summary>
    ApiTokens = 28,

    /// <summary>Portal do cidadão/paciente (PWA): acesso ao próprio escopo (translados, acompanhante).</summary>
    Cidadao = 29,

    /// <summary>Configurar integrações externas (Google Maps, WhatsApp via Automais.Zap).</summary>
    IntegracoesConfig = 30,

    /// <summary>Faturamento SUS / geração de BPA do transporte (TFD).</summary>
    Faturamento = 31,

    /// <summary>Configurar o cabeçalho/rodapé institucional global dos laudos (PDF).</summary>
    ConfiguracaoLaudo = 32,

    /// <summary>Consultar a trilha de auditoria do sistema (somente leitura).</summary>
    Auditoria = 33,

    /// <summary>Consultar o log de erros não tratados do sistema (diagnóstico, só leitura).</summary>
    Erros = 34,

    /// <summary>Central de Atendimento (chat WhatsApp): ver/atender conversas da(s) própria(s) unidade(s).</summary>
    Conversas = 35,

    /// <summary>Supervisão do chat: ver TODAS as conversas (qualquer unidade), reatribuir de terceiros
    /// e encaminhar entre unidades.</summary>
    ConversasSupervisao = 36,

    /// <summary>Gestão de tickets de suporte: ver TODOS os tickets, responder, mudar status e
    /// configurar a visibilidade. Abrir/ver os próprios tickets não exige este módulo (qualquer
    /// usuário autenticado pode).</summary>
    Ticket = 37,

    /// <summary>Gestão das notificações WhatsApp de agendamento (confirmação de exames):
    /// acompanhar envios/entregas/falhas, respostas do paciente e reenviar.</summary>
    NotificacoesAgendamento = 38,

    /// <summary>Sandbox de QA (admin): criar pacientes de teste, semear exames/agendamentos,
    /// gerar magic link e enviar mensagens de teste por WhatsApp (texto livre, janela 24h).</summary>
    Sandbox = 39,

    /// <summary>Cadastro das mensagens prontas ("respostas rápidas") do chat: criar, editar e
    /// definir as variáveis. USAR os atalhos no chat exige só <see cref="Conversas"/>.</summary>
    RespostasRapidas = 40,

    // 41 era "Consultas": fundido em Solicitacoes (exames e consultas numa lista só) — as
    // permissões foram migradas para o 16. Não reutilizar o número.

    /// <summary>Mapeamento SIGTAP→TipoExame: curadoria dos exames de imagem importados sem tipo
    /// (pendentes) — criar/vincular TipoExame com backfill por código SIGTAP.</summary>
    MapeamentoSigtap = 42,

    /// <summary>Agente IA: terminal do agente que roda NO SERVIDOR, com shell, banco de produção
    /// e o clone do repositório. Pode alterar código e commitar. Conceder apenas a quem já teria
    /// acesso administrativo ao servidor — não é um assistente de consulta como
    /// <see cref="Inteligencia"/>.</summary>
    AgenteIa = 43,

    /// <summary>Indicadores contratuais do HMCML: consultar os painéis por aba da planilha.
    /// A ação de Edição libera alterar o SQL do motor de cada indicador (consulta read-only
    /// contra a base de origem) — conceder só a quem for aprimorar o cálculo.</summary>
    Indicadores = 44,

    /// <summary>Estatísticas de atendimento (retrato do WhatsApp): dashboard gerencial com
    /// contagens de mensagens (templates do sistema x atendente, sessão, recebidas), séries
    /// diárias, status de entrega e ranking de atendentes. Visão global (só leitura).</summary>
    Estatistica = 45,

    /// <summary>Modo desenvolvedor da Consulta Inteligente: libera o checkbox que mostra o
    /// raciocínio completo do motor (consultas SQL, ferramentas, passos) em vez de só o
    /// resultado concreto. Puramente de UI — não abre nenhum acesso novo a dado. Conceder a
    /// quem precisa depurar/entender as consultas.</summary>
    InteligenciaConsultaDev = 46,

    // ---- Regulação → Solicitações (ADR-0052) ----
    // Módulos separados, e não ações do mesmo, porque AcoesPermissao só tem 4 flags: não há como
    // separar "assumir da fila" de "abrir solicitação" dentro de um módulo só. Mesmo idioma de
    // Conversas/ConversasSupervisao e Sisreg/SisregConfiguracao.
    //
    // O comentário anterior citava um "ADR-0024" que não é deste assunto (0024 é o sync contínuo
    // Salux→FHIR) e descrevia um desenho — parecer médico, prioridade, deferimento — que não é o
    // que foi construído. Corrigido em 06/09/2026.

    /// <summary>Regulação → Solicitações, lado da UNIDADE SOLICITANTE: abrir, editar, anexar,
    /// responder regras e pendências, enviar para a fila de pré-regulação, cancelar; ver a própria
    /// fila e as notificações. Escopado por unidade (<c>EscopoUnidade</c>, fail-closed): o usuário
    /// só enxerga as solicitações das unidades a que está vinculado. ADR-0052.</summary>
    Regulacao = 47,

    /// <summary>Agente regulador: vê a fila de TODAS as unidades, assume, ajusta (com histórico),
    /// devolve, recusa, registra ou envia ao sistema de regulação e aprova as pendências vindas da
    /// ponta. Quem tem 48 recebe 47 também — o agente também abre solicitação. ADR-0052.</summary>
    RegulacaoTriagem = 48,

    /// <summary>Reservado — não implementado. O número fica preso para não ser reaproveitado por
    /// outro assunto: perfil já concedido em produção passaria a valer para o módulo novo.</summary>
    RegulacaoMedica = 49,

    /// <summary>Reservado — não implementado (ver a nota em <see cref="RegulacaoMedica"/>).</summary>
    RegulacaoAgendamento = 50,

    /// <summary>Configuração da regulação: regras de elegibilidade, curadoria do catálogo canônico,
    /// credenciais e motores do SER/SERNIT, e as configurações do módulo (corte da busca, limites
    /// de anexo, rótulo da fila). ADR-0052.</summary>
    RegulacaoConfiguracao = 51,

    /// <summary>SISREG — MAPEAMENTO: a "verdade" da unidade no SISREG (profissionais executantes e
    /// seus procedimentos), com habilita/desabilita que define o que entra na varredura de agenda,
    /// e a credencial de operador do SISREG daquela unidade.</summary>
    SisregMapeamento = 52,

    /// <summary>Abrir SOLICITAÇÃO DE EXAME MANUAL (o botão "Nova solicitação" da tela de exames /
    /// <c>POST /solicitacoes</c>). Separado de <see cref="Solicitacoes"/> porque criar
    /// à mão é a exceção — o fluxo normal entra pela importação do SISREG (serviço, sem passar por
    /// este gate) — e queremos liberar o botão só a usuários específicos, não a todo perfil com
    /// Inclusão no módulo. Usa apenas a ação <c>Inclusao</c> (ticket #89).</summary>
    SolicitacaoExameManual = 53,

    /// <summary>Regulação — **SER** (Sistema Estadual de Regulação, SES-RJ): a fila do Estado
    /// espelhada na nossa base (ADR-0042), com o histórico de cada solicitação. É **só leitura
    /// por natureza** — nada aqui escreve no SER —, então usa apenas <c>Consulta</c>. Separado de
    /// <see cref="RegulacaoAgendamento"/> porque é outra fonte, com outro vocabulário de situação
    /// e outra operação (acompanhar fila estadual, não marcar).</summary>
    RegulacaoSer = 54,

    /// <summary>Correção de IDENTIDADE de exame — trocar de qual paciente é um estudo já no PACS.
    /// Nasce do incidente de 11/08/2026 (a técnica puxou o item de worklist errado e as imagens de
    /// uma paciente foram arquivadas sob outro).
    /// <para>Módulo próprio, e não uma ação de <see cref="Pacs"/>, porque a operação é de outra
    /// natureza: <b>reescreve o objeto DICOM no PACS</b> (apaga o original e re-armazena com UIDs
    /// novos), descarta rascunhos de laudo e revoga o link já enviado ao paciente. Quem cuida do
    /// dia a dia de exames não precisa disso; quem precisa é um punhado de nomes. Mesmo caminho de
    /// <see cref="SolicitacaoExameManual"/> e <see cref="Sandbox"/>.</para>
    /// <para>Usa apenas <c>Edicao</c>: é ela que faz aparecer o card no pedido e libera a
    /// correção. Não há fila nem triagem — quem tem a permissão resolve na hora, na própria tela
    /// da solicitação; quem não tem, não vê o card.</para></summary>
    CorrecaoIdentidadeExame = 55,

    /// <summary>Enviar a pesquisa de satisfação de um atendimento ao paciente, pelo histórico.
    /// <para>Módulo próprio, e não uma ação de <see cref="Pacientes"/>, porque a operação
    /// <b>manda mensagem para o cidadão</b>: quem consulta o histórico não deveria, por isso,
    /// poder disparar WhatsApp. Enquanto o envio for manual é aqui que se controla quem valida
    /// o fluxo; quando o gatilho automático entrar, ele passa a conviver com esta permissão para
    /// o reenvio pontual.</para>
    /// <para>Usa apenas <c>Edicao</c> — é ela que faz aparecer o botão no atendimento.</para></summary>
    PesquisaSatisfacao = 56,

    /// <summary>Identidade da instituição desta instância (ADR-0043): nome da secretaria, marca
    /// (logo e cores), domínios e contatos legais (LGPD).
    /// <para>Módulo próprio porque o alcance não se parece com nenhum outro: o que se edita aqui
    /// aparece no login, no PDF de laudo, na página pública de verificação e no app do cidadão —
    /// inclusive para quem <b>não</b> está autenticado. Errar o e-mail do DPO ou trocar o logo é
    /// mudança institucional, não operacional; fica com um punhado de nomes, como
    /// <see cref="ConfiguracaoLaudo"/>.</para>
    /// <para><c>Consulta</c> para ver a tela; <c>Edicao</c> para salvar. A <b>leitura pública</b>
    /// (<c>GET /publico/instituicao</c>) não passa por aqui — é anônima por necessidade: o front
    /// precisa se pintar antes de existir sessão.</para></summary>
    Instituicao = 57,

    /// <summary>Regulação — **SERNIT** (SER de Niterói): a fila de Niterói espelhada na nossa base,
    /// subsistema irmão do <see cref="RegulacaoSer"/> (ADR-0042), em tabelas <c>sernit_*</c>
    /// próprias. Mesma natureza do SER-RJ — acompanhar a fila estadual e a trilha de cada
    /// solicitação —, com <c>Consulta</c> para ler o espelho e <c>Edicao</c> para as ações que
    /// escrevem no SERNIT (FollowUP, telefones), assinadas pelo operador.</summary>
    RegulacaoSernit = 58,

    /// <summary>Robô de atendimento (WhatsApp): cadastrar os ASSUNTOS que o robô atende, seus
    /// treinos/condições e os comandos liberados por assunto, além da configuração global do
    /// robô. É a tela de gestão do bot — atender/ver conversas continua sendo
    /// <see cref="Conversas"/>. <c>Consulta</c> para ver; <c>Inclusao</c>/<c>Edicao</c>/
    /// <c>Exclusao</c> para gerir assuntos e ligar/desligar comandos.</summary>
    RoboAtendimento = 59,

    /// <summary>Pendências de ajuste de cadastro ("números errados"): a fila de casos em que o
    /// cidadão avisou que o número não é dele. O robô só registra (com o vínculo declarado); a
    /// recepção resolve o cadastro aqui. <c>Consulta</c> para ver a fila; <c>Edicao</c> para
    /// resolver/ignorar e registrar manualmente.</summary>
    AjusteCadastro = 60,

    /// <summary>Alterações de agenda: a fila do que o SISREG mudou em agendamentos já importados
    /// (remarcação, troca de profissional ou procedimento). <c>Consulta</c> para ver a fila;
    /// <c>Edicao</c> para tratar e para reenviar a mensagem ao paciente.</summary>
    AlteracoesAgenda = 61,

    /// <summary>Agenda: a oferta de vagas do SISREG cruzada com a ocupação já importada — consulta
    /// por unidade/especialidade/profissional e as estatísticas de gestão dessas vagas.</summary>
    Agenda = 62,

    /// <summary>Revelar a <b>chave de confirmação</b> de uma solicitação, lida na hora no SISREG
    /// (ficha do <c>cons_marcados_reg</c>), pela seção "SISREG" do detalhe da solicitação.
    /// <para>Módulo próprio, e não uma ação de <see cref="Solicitacoes"/>,
    /// porque a chave é a <b>prova de comparecimento</b>: é o que o executante digita no SISREG para
    /// dar baixa, e o SISREG só a entrega a quem traz o comprovante. Quem vê o pedido não deveria,
    /// por isso, poder dar baixa sem o paciente. Cada revelação gasta uma requisição do orçamento
    /// anti-robô e fica na auditoria (sem o valor da chave).</para>
    /// <para>Usa apenas <c>Consulta</c> — é ela que mostra o botão "Mostrar chave".</para></summary>
    RevelarChaveSisreg = 63,

    /// <summary>Consulta Inteligente — base <b>Atendimento</b>: perguntar sobre as conversas com os
    /// pacientes (WhatsApp), o conteúdo das mensagens, o robô e as comunicações enviadas.
    /// <para>Módulo próprio, e não parte de <see cref="Inteligencia"/>, porque o que se lê aqui é a
    /// <b>conversa do cidadão</b> — texto livre, com queixa, telefone e às vezes dado clínico — da
    /// rede toda, sem o recorte de unidade que a Central de Atendimento aplica. Quem pergunta sobre
    /// fila e agenda não deveria, por isso, poder ler conversas. Toda pergunta e todo SQL ficam na
    /// auditoria da consulta (<c>ia_consulta</c>).</para>
    /// <para>Usa apenas <c>Consulta</c>: é ela que faz a base aparecer na Consulta Inteligente. Exige
    /// também <see cref="Inteligencia"/> (a tela).</para></summary>
    InteligenciaAtendimento = 64,

    /// <summary>Confirmações de agendamento por WhatsApp: fila de disparo (o que está empilhado e
    /// por quê), respostas dos pacientes (confirmou / não vai, com o motivo) e as regras de disparo
    /// (janela de horário, vazão, só SISREG, chave por unidade). <c>Edicao</c> altera as regras e
    /// reenvia.</summary>
    Confirmacoes = 65,

    /// <summary>Estratégias de fila (ADR-0058): simular mudanças na oferta de um procedimento
    /// (unidades, profissionais, dias, vagas) contra a fila real e pedir ao agente uma estratégia
    /// para zerá-la. <b>Só planejamento</b> — nada escreve no SISREG; a estratégia fica no nosso
    /// banco para consulta. <c>Consulta</c> vê a lista, o cenário e simula sem gravar;
    /// <c>Inclusao</c> salva estratégia e roda o agente; <c>Edicao</c> edita, reroda e marca como
    /// aplicada; <c>Exclusao</c> arquiva.</summary>
    EstrategiasFila = 66,

    /// <summary>Estatísticas: CUSTOS (IA e Meta). Sem este módulo, o retrato do WhatsApp sai sem
    /// tokens e sem custo do robô e sem a estimativa de custo Meta — quem vê volume não precisa
    /// saber quanto se gasta. Só <c>Consulta</c>. Exige também <see cref="Estatistica"/> (a tela).</summary>
    EstatisticaCustos = 67,

    // ---- Estatísticas dos operadores por sistema de regulação (20/09/2026) ----
    // Três módulos, e não um: cada sistema tem a sua equipe, e quem gerencia a regulação estadual
    // não precisa ver o ranking do SISREG (nem o contrário). Só <c>Consulta</c> — a tela é
    // leitura; escolher quem entra nas estatísticas continua na configuração de cada sistema
    // (<see cref="SisregConfiguracao"/> e <see cref="RegulacaoConfiguracao"/>). Nascem
    // desligados em todo perfil que não seja o Admin.

    /// <summary>Estatísticas do SISREG: trabalho dos operadores autorizadores (equipe, individual e
    /// rankings) a partir do export da agenda importado. Antes vivia dentro de <see cref="Sisreg"/>;
    /// saiu porque quem consulta a agenda não precisa ver a produção de cada colega.</summary>
    EstatisticaSisreg = 68,

    /// <summary>Estatísticas do SER (SES-RJ): trabalho dos operadores a partir da trilha de eventos
    /// da fila espelhada (quem agendou, cancelou, pendenciou, fez FollowUP e quando).</summary>
    EstatisticaSer = 69,

    /// <summary>Estatísticas do SERNIT (SER de Niterói): mesma natureza de
    /// <see cref="EstatisticaSer"/>, sobre as tabelas <c>sernit_*</c>.</summary>
    EstatisticaSernit = 70,

    // ---- Ouvidoria (ADR-0060, 20/09/2026) ----
    // Quatro módulos, e não um: <c>AcoesPermissao</c> só tem 4 flags (Consulta/Inclusao/Edicao/
    // Exclusao) e elas já são consumidas pelo trabalho normal da ouvidoria (registrar, triar,
    // encaminhar, responder, arquivar). Sigilo e ponto de resposta são eixos ORTOGONAIS a esse
    // trabalho — "quem pode ver a identidade de um manifestante sigiloso" e "quem responde pela
    // unidade/área sem ver o manifestante" não são graus da mesma escala, são pessoas diferentes
    // com recortes diferentes. Gestão (pontos, assuntos, configuração, painel) também sai porque
    // o técnico que tria não é quem cadastra a corregedoria como ponto de apuração.

    /// <summary>Trabalho da ouvidoria: fila e detalhe das manifestações (manifestante mascarado
    /// quando sigilosa; denúncias inteiras só com <see cref="OuvidoriaSigilo"/>). <c>Consulta</c> =
    /// ver fila/detalhe; <c>Inclusao</c> = registrar manifestação; <c>Edicao</c> = triar, encaminhar,
    /// pedir complementação, validar, responder ao cidadão, prorrogar, cobrar, recurso, concluir;
    /// <c>Exclusao</c> = arquivar (não há DELETE — arquivar é estado final com motivo).</summary>
    Ouvidoria = 71,

    /// <summary>Gestão do módulo: painel e indicadores, cadastro de pontos de resposta, assuntos,
    /// marcadores e configuração de prazos. Separado de <see cref="Ouvidoria"/> porque quem tria
    /// não é quem define a estrutura. <c>Consulta</c> = painel/indicadores e configuração;
    /// <c>Inclusao</c> = criar pontos de resposta, assuntos, marcadores; <c>Edicao</c> = editar os
    /// mesmos + configuração + escalonar manifestação; <c>Exclusao</c> = desativar.</summary>
    OuvidoriaGestao = 72,

    /// <summary>Sigilo (Decreto 10.153/2019): ver denúncias e revelar a identidade de manifestante
    /// sigiloso — <b>cada revelação exige justificativa e é gravada em
    /// <c>ouvidoria_acesso_identidade</c></b>. Eixo ortogonal a <see cref="Ouvidoria"/>: um técnico
    /// pode triar sem nunca ver quem denunciou. <c>Consulta</c> = ver denúncias e revelar identidade;
    /// <c>Inclusao</c> = habilitar denúncia (juízo de admissibilidade); <c>Edicao</c> = editar o teor
    /// pseudonimizado que vai à apuração. <c>Exclusao</c> não é usada.</summary>
    OuvidoriaSigilo = 73,

    /// <summary>Ponto de resposta: o servidor da unidade/área/apuração que responde à ouvidoria
    /// pelas manifestações encaminhadas aos pontos de que é <i>membro</i>
    /// (<c>ouvidoria_ponto_resposta_membro</c>), <b>sem nunca ver dados do manifestante</b> e, em
    /// denúncia, só o teor pseudonimizado. Ortogonal a <see cref="Ouvidoria"/>: não vê a fila
    /// geral. <c>Consulta</c> = ver as encaminhadas aos seus pontos; <c>Edicao</c> = responder pela
    /// área. <c>Inclusao</c> e <c>Exclusao</c> não são usadas.</summary>
    OuvidoriaPontoResposta = 74,

    /// <summary>Confirmações: EQUIPE — a produção de cada atendente na tela de Confirmações (quem
    /// pegou, confirmou, cancelou, estacionou, marcou e resolveu telefone errado, tempos e ritmo),
    /// lida da trilha <c>atendimento_confirmacao_evento</c>. Fora de <see cref="Confirmacoes"/> de
    /// propósito: quem atende não precisa ver o ranking das colegas — sem este módulo a aba nem
    /// aparece. Só <c>Consulta</c>. Exige também <see cref="Confirmacoes"/> (a tela). Nasce
    /// desligado em todo perfil que não seja o Admin.</summary>
    ConfirmacoesEquipe = 75,

    /// <summary>Transporte de Pacientes: cadastro das UNIDADES DE ATENDIMENTO — os destinos da van
    /// (clínica, hospital de referência), com endereço e coordenada usados no cálculo da rota.
    /// Cadastro manual e próprio do transporte, separado de <see cref="Unidades"/> (a unidade de
    /// saúde do SISREG/CNES). Quem só cadastra tratamento escolhe o destino pela lista de
    /// <see cref="Tratamentos"/>, sem precisar deste módulo.</summary>
    UnidadesAtendimento = 76,

    /// <summary>Regulação — <b>ESUS de São Gonçalo</b> (ADR-0063): o espelho da fila e dos
    /// agendamentos que Maricá tem no ESUS de SG (PPI). <c>Consulta</c> = ver fila, agendados,
    /// detalhe e notificações. Só leitura nesta entrega: <c>Edicao</c> fica reservada para quando
    /// a escrita no ESUS for mapeada e autorizada. Configuração do motor usa
    /// <see cref="RegulacaoConfiguracao"/>.</summary>
    RegulacaoEsusSg = 77,

    /// <summary>Estatísticas de operadores do ESUS de São Gonçalo (quem incluiu na fila, quem
    /// agendou), lidas da trilha <c>esussg_evento</c>. Irmão de <see cref="EstatisticaSer"/> e
    /// <see cref="EstatisticaSernit"/>. Só <c>Consulta</c>.</summary>
    EstatisticaEsusSg = 78,

    /// <summary>Indicadores de Regulação (série mensal de vagas, absenteísmo, regulados, fila, desfechos
    /// com motivo, espera e judicializadas) nas telas de SISREG, SER e SERNIT, com exportação em PDF.
    /// Um módulo só para os três sistemas (decisão do Bernardo, 30/09/2026). Só <c>Consulta</c>. A
    /// configuração dos coletores do SISREG fica em <see cref="SisregConfiguracao"/>.</summary>
    IndicadoresRegulacao = 79,

    /// <summary>Administração da extensão do Chrome distribuída pela plataforma (ADR-0064): os
    /// computadores autorizados (com o inventário que cada um informa) e as versões publicadas da
    /// extensão e do atualizador. <c>Consulta</c> = ver; <c>Inclusao</c> = publicar versão;
    /// <c>Edicao</c> = promover versão e trocar o canal de um computador; <c>Exclusao</c> = retirar
    /// versão e revogar computador. Baixar o instalador e autorizar o próprio computador NÃO pedem
    /// este módulo: basta estar logado (mesma regra do envio das capturas).</summary>
    ExtensaoNavegador = 80,

    /// <summary>Notificação (push) no app do cidadão, pela ficha do paciente: ver os aparelhos com
    /// as notificações ativas e o histórico de envios, e mandar uma mensagem que aparece na tela do
    /// celular dele.
    /// <para>Módulo próprio, e não uma ação de <see cref="Pacientes"/>, pelo mesmo motivo de
    /// <see cref="PesquisaSatisfacao"/>: a operação <b>manda mensagem para o cidadão</b>, e quem
    /// consulta o cadastro não deveria, por isso, poder escrever no celular dele.</para>
    /// <para><c>Consulta</c> = ver aparelhos e histórico; <c>Edicao</c> = enviar. Sem item de menu:
    /// vive na ficha do paciente.</para></summary>
    NotificacaoAppCidadao = 81,
}
