using Microsoft.EntityFrameworkCore;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Conversas;
using SMSMais.Data.Entities.Ia;
using SMSMais.Data.Entities.Integracoes;
using SMSMais.Data.Entities.Pep;
using SMSMais.Data.Entities.Regulacao;
using SMSMais.Data.Entities.Ser;
using SMSMais.Data.Entities.Sernit;
using SMSMais.Data.Entities.Sisreg;
using SMSMais.Data.Entities.Geo;
using SMSMais.Data.Entities.Notificacoes;
using SMSMais.Data.Entities.Robo;
using SMSMais.Data.Entities.Tfd;

namespace SMSMais.Data;

public sealed class SmsMaisDbContext(DbContextOptions<SmsMaisDbContext> options) : DbContext(options)
{
    public const string SchemaPadrao = "smsmarica";

    public DbSet<Tratamento> Tratamentos => Set<Tratamento>();
    public DbSet<TipoTratamento> TiposTratamento => Set<TipoTratamento>();
    public DbSet<Periodicidade> Periodicidades => Set<Periodicidade>();
    public DbSet<SessaoDeTratamento> Sessoes => Set<SessaoDeTratamento>();
    public DbSet<Unidade> Unidades => Set<Unidade>();
    /// <summary>Identidade da instituição desta instância (singleton) — ADR-0043.</summary>
    public DbSet<Instituicao> Instituicoes => Set<Instituicao>();
    public DbSet<Veiculo> Veiculos => Set<Veiculo>();
    public DbSet<Fileira> Fileiras => Set<Fileira>();
    public DbSet<Assento> Assentos => Set<Assento>();
    public DbSet<Motorista> Motoristas => Set<Motorista>();
    public DbSet<RotaDiaria> Rotas => Set<RotaDiaria>();
    public DbSet<Alocacao> Alocacoes => Set<Alocacao>();
    public DbSet<PontoGps> PontosGps => Set<PontoGps>();
    public DbSet<Geofence> Geofences => Set<Geofence>();
    public DbSet<EventoChegada> EventosChegada => Set<EventoChegada>();
    public DbSet<Avaliacao> Avaliacoes => Set<Avaliacao>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<EstudoAnotacao> EstudoAnotacoes => Set<EstudoAnotacao>();
    public DbSet<Perfil> Perfis => Set<Perfil>();
    public DbSet<PermissaoPerfil> PermissoesPerfil => Set<PermissaoPerfil>();
    public DbSet<UsuarioPerfil> UsuariosPerfis => Set<UsuarioPerfil>();
    public DbSet<PermissaoUsuario> PermissoesUsuario => Set<PermissaoUsuario>();
    public DbSet<Laudo> Laudos => Set<Laudo>();
    public DbSet<LaudoAssinatura> LaudoAssinaturas => Set<LaudoAssinatura>();
    public DbSet<LaudoTemplate> LaudoTemplates => Set<LaudoTemplate>();
    public DbSet<LaudoConfiguracao> LaudoConfiguracoes => Set<LaudoConfiguracao>();
    public DbSet<Midia> Midias => Set<Midia>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketComentario> TicketComentarios => Set<TicketComentario>();
    public DbSet<TicketAnexo> TicketAnexos => Set<TicketAnexo>();
    public DbSet<TicketConfiguracao> TicketConfiguracoes => Set<TicketConfiguracao>();
    public DbSet<ProcedimentoSigtap> ProcedimentosSigtap => Set<ProcedimentoSigtap>();
    public DbSet<TipoExame> TiposExame => Set<TipoExame>();
    // Ecossistema de solicitação (ADR-0021): espinha de regulação + satélite de execução de imagem.
    public DbSet<Solicitacao> Solicitacoes => Set<Solicitacao>();
    public DbSet<ExameImagem> ExamesImagem => Set<ExameImagem>();
    public DbSet<Anamnese> Anamneses => Set<Anamnese>();
    public DbSet<AssinaturaMedico> AssinaturasMedico => Set<AssinaturaMedico>();
    public DbSet<ExameAssociacao> ExameAssociacoes => Set<ExameAssociacao>();
    public DbSet<DeclaracaoComparecimentoVerificacao> DeclaracaoComparecimentoVerificacoes => Set<DeclaracaoComparecimentoVerificacao>();
    public DbSet<DownloadToken> DownloadTokens => Set<DownloadToken>();
    public DbSet<CidadaoLoginLink> CidadaoLoginLinks => Set<CidadaoLoginLink>();
    public DbSet<PesquisaSatisfacao> PesquisasSatisfacao => Set<PesquisaSatisfacao>();
    public DbSet<UnidadePesquisaConfig> UnidadePesquisaConfigs => Set<UnidadePesquisaConfig>();

    // Anexos de exame (ponte QR → PWA "Arquivos Saúde Maricá")
    public DbSet<DocumentoExame> DocumentosExame => Set<DocumentoExame>();
    public DbSet<AnexoUploadToken> AnexoUploadTokens => Set<AnexoUploadToken>();

    // Tokens de API (chaves de serviço para integrações externas, ex.: CentralIA)
    public DbSet<ApiToken> ApiTokens => Set<ApiToken>();

    // Módulo IA (consulta em linguagem natural)
    public DbSet<IaConfiguracao> IaConfiguracoes => Set<IaConfiguracao>();
    public DbSet<IaFonte> IaFontes => Set<IaFonte>();
    public DbSet<IaDocumentoConhecimento> IaDocumentosConhecimento => Set<IaDocumentoConhecimento>();
    public DbSet<IaChunkConhecimento> IaChunksConhecimento => Set<IaChunkConhecimento>();
    public DbSet<IaAprendizado> IaAprendizados => Set<IaAprendizado>();
    public DbSet<IaConsulta> IaConsultas => Set<IaConsulta>();
    public DbSet<IaCorrecao> IaCorrecoes => Set<IaCorrecao>();
    public DbSet<IaConsultaFeedback> IaConsultaFeedbacks => Set<IaConsultaFeedback>();

    // Integração SISREG (feed de leitura) — ADR-0012
    public DbSet<SisregConfiguracao> SisregConfiguracoes => Set<SisregConfiguracao>();

    // Credenciais de provedores de login OAuth (Microsoft/Facebook/Google), cifradas
    public DbSet<IntegracaoCredencial> IntegracaoCredenciais => Set<IntegracaoCredencial>();

    // Motores de proxy (CPF/CEP) com fallback configurável por serviço
    public DbSet<ProxyMotorConfig> ProxyMotores => Set<ProxyMotorConfig>();

    // Sincronização de PEPs (importação Salux/outros → hub FHIR) — ADR-0014
    public DbSet<PepSincronizacaoExecucao> PepSincronizacaoExecucoes => Set<PepSincronizacaoExecucao>();
    public DbSet<PepSincronizacaoEstado> PepSincronizacaoEstados => Set<PepSincronizacaoEstado>();
    public DbSet<PepSincronizacaoFalha> PepSincronizacaoFalhas => Set<PepSincronizacaoFalha>();
    public DbSet<PepSincronizacaoAgenda> PepSincronizacaoAgendas => Set<PepSincronizacaoAgenda>();
    public DbSet<PepDivergenciaIdentidade> PepDivergenciasIdentidade => Set<PepDivergenciaIdentidade>();

    // Linhas do export do SISREG que não viraram solicitação (com o RAW, para revalidar)
    public DbSet<Entities.Sisreg.SisregImportacaoFalha> SisregImportacaoFalhas => Set<Entities.Sisreg.SisregImportacaoFalha>();

    // Operações observadas no SISREG pela extensão de navegador (fase de análise; só inclusão)
    public DbSet<Entities.Sisreg.SisregCapturaNavegador> SisregCapturasNavegador => Set<Entities.Sisreg.SisregCapturaNavegador>();

    // Uma linha por arquivo importado (rastreio: quando, quem, válidos, inválidos)
    public DbSet<Entities.Sisreg.SisregImportacaoExecucao> SisregImportacaoExecucoes => Set<Entities.Sisreg.SisregImportacaoExecucao>();

    // Mapeamento da "verdade" do SISREG: profissionais da unidade e seus procedimentos,
    // com habilita/desabilita para a varredura de agenda não gastar requisição à toa
    public DbSet<SisregProfissionalUnidade> SisregProfissionaisUnidade => Set<SisregProfissionalUnidade>();
    public DbSet<SisregProcedimentoProfissional> SisregProcedimentosProfissional => Set<SisregProcedimentoProfissional>();

    // De-para global do código de procedimento do SISREG (o `pa`) para o SIGTAP oficial — a agenda
    // não informa SIGTAP, e sem ele a solicitação nasceria sem categoria e sem worklist
    public DbSet<SisregProcedimentoSigtap> SisregProcedimentosSigtap => Set<SisregProcedimentoSigtap>();

    // Motor diário que varre a agenda do SISREG por unidade: agenda (quando roda) e rastreio (o que rodou)
    /// <summary>Grade de OFERTA do SISREG (tela cons_escalas) — vagas por profissional × unidade ×
    /// procedimento × dia da semana. Base da Agenda.</summary>
    public DbSet<SisregEscala> SisregEscalas => Set<SisregEscala>();

    /// <summary>Alterações que o SISREG fez em solicitações já importadas (remarcação, troca de
    /// profissional ou procedimento) — a fila que o regulador trata.</summary>
    public DbSet<SisregAlteracaoAgenda> SisregAlteracoesAgenda => Set<SisregAlteracaoAgenda>();

    /// <summary>Rastreio das sincronizações da grade de escalas.</summary>
    public DbSet<SisregEscalaSincronizacaoExecucao> SisregEscalaSincronizacaoExecucoes =>
        Set<SisregEscalaSincronizacaoExecucao>();

    /// <summary>Quem pediu no SISREG e ainda NAO foi agendado — a fila de espera de verdade.</summary>
    public DbSet<SisregFilaPendente> SisregFilaPendentes => Set<SisregFilaPendente>();

    public DbSet<SisregVarreduraAgenda> SisregVarreduraAgendas => Set<SisregVarreduraAgenda>();
    public DbSet<SisregVarreduraExecucao> SisregVarreduraExecucoes => Set<SisregVarreduraExecucao>();
    public DbSet<SisregVarreduraExecucaoItem> SisregVarreduraExecucaoItens => Set<SisregVarreduraExecucaoItem>();

    // "Sincroniza tudo" do mapeamento: rastreio da rede inteira (o progresso vivo é memória; isto
    // é o que sobra depois — quantas unidades o SISREG tem, quantas nasceram aqui, quantos médicos
    // e procedimentos por unidade).
    public DbSet<SisregMapeamentoLoteExecucao> SisregMapeamentoLoteExecucoes => Set<SisregMapeamentoLoteExecucao>();
    public DbSet<SisregMapeamentoLoteExecucaoItem> SisregMapeamentoLoteExecucaoItens => Set<SisregMapeamentoLoteExecucaoItem>();

    // SER (Sistema Estadual de Regulação, SES-RJ) — ESPELHO da fila do Estado, ADR-0042.
    // Deliberadamente separado de `solicitacao`: solicitação do SER não tem unidade executante
    // em Maricá nem código SIGTAP, e misturá-las contaminaria worklist/recepção/PACS.
    public DbSet<SerSolicitacao> SerSolicitacoes => Set<SerSolicitacao>();
    public DbSet<SerEvento> SerEventos => Set<SerEvento>();
    public DbSet<SerGatilho> SerGatilhos => Set<SerGatilho>();
    public DbSet<SerVarreduraExecucao> SerVarreduraExecucoes => Set<SerVarreduraExecucao>();
    public DbSet<SerVarreduraFalha> SerVarreduraFalhas => Set<SerVarreduraFalha>();

    // Catálogo do SER espelhado: a tela de nova solicitação monta o formulário daqui, offline.
    public DbSet<SerCatalogoRecurso> SerCatalogoRecursos => Set<SerCatalogoRecurso>();
    public DbSet<SerCatalogoCampo> SerCatalogoCampos => Set<SerCatalogoCampo>();
    public DbSet<SerCatalogoLista> SerCatalogoListas => Set<SerCatalogoLista>();
    public DbSet<SerCatalogoCidLista> SerCatalogoCidListas => Set<SerCatalogoCidLista>();
    public DbSet<SerCatalogoCid> SerCatalogoCids => Set<SerCatalogoCid>();

    // Pedidos montados na nossa base, esperando autorização para ir ao SER.
    public DbSet<SerSolicitacaoRascunho> SerSolicitacaoRascunhos => Set<SerSolicitacaoRascunho>();
    public DbSet<SerRascunhoAnexo> SerRascunhoAnexos => Set<SerRascunhoAnexo>();

    // SERNIT (SER de Niterói) — ESPELHO da fila de Niterói, subsistema irmão do SER-RJ (ADR-0042),
    // em tabelas próprias `sernit_*` para isolar do SER-RJ em produção. Mesma plataforma
    // (JSF/RichFaces/Seam), instância e build diferentes; leitura por paginação (não há export).
    public DbSet<SernitSolicitacao> SernitSolicitacoes => Set<SernitSolicitacao>();
    public DbSet<SernitEvento> SernitEventos => Set<SernitEvento>();
    public DbSet<SernitGatilho> SernitGatilhos => Set<SernitGatilho>();
    public DbSet<SernitVarreduraExecucao> SernitVarreduraExecucoes => Set<SernitVarreduraExecucao>();
    public DbSet<SernitVarreduraFalha> SernitVarreduraFalhas => Set<SernitVarreduraFalha>();

    // Catálogo do SERNIT espelhado (nova solicitação monta o formulário daqui, offline) + rascunhos.
    public DbSet<SernitCatalogoRecurso> SernitCatalogoRecursos => Set<SernitCatalogoRecurso>();
    public DbSet<SernitCatalogoCampo> SernitCatalogoCampos => Set<SernitCatalogoCampo>();
    public DbSet<SernitCatalogoLista> SernitCatalogoListas => Set<SernitCatalogoLista>();
    public DbSet<SernitCatalogoCidLista> SernitCatalogoCidListas => Set<SernitCatalogoCidLista>();
    public DbSet<SernitCatalogoCid> SernitCatalogoCids => Set<SernitCatalogoCid>();
    public DbSet<SernitSolicitacaoRascunho> SernitSolicitacaoRascunhos => Set<SernitSolicitacaoRascunho>();
    public DbSet<SernitRascunhoAnexo> SernitRascunhoAnexos => Set<SernitRascunhoAnexo>();

    // Catálogo canônico de procedimentos da Regulação (ADR-0052): reúne, sob um procedimento
    // que o solicitante reconhece, as origens equivalentes do SISREG, do SER e do SERNIT.
    public DbSet<RegulacaoProcedimento> RegulacaoProcedimentos => Set<RegulacaoProcedimento>();
    public DbSet<RegulacaoProcedimentoOrigem> RegulacaoProcedimentoOrigens => Set<RegulacaoProcedimentoOrigem>();
    public DbSet<RegulacaoConfiguracao> RegulacaoConfiguracoes => Set<RegulacaoConfiguracao>();
    public DbSet<RegulacaoSolicitacao> RegulacaoSolicitacoes => Set<RegulacaoSolicitacao>();
    public DbSet<RegulacaoFormularioVersao> RegulacaoFormularioVersoes => Set<RegulacaoFormularioVersao>();
    public DbSet<RegulacaoFormularioCampoMapa> RegulacaoFormularioCampoMapas => Set<RegulacaoFormularioCampoMapa>();
    public DbSet<RegulacaoSolicitacaoExigencia> RegulacaoSolicitacaoExigencias => Set<RegulacaoSolicitacaoExigencia>();
    public DbSet<RegulacaoExigenciaArquivo> RegulacaoExigenciaArquivos => Set<RegulacaoExigenciaArquivo>();
    public DbSet<RegulacaoEvento> RegulacaoEventos => Set<RegulacaoEvento>();
    public DbSet<RegulacaoSolicitacaoDestino> RegulacaoSolicitacaoDestinos => Set<RegulacaoSolicitacaoDestino>();
    public DbSet<RegulacaoEventoVisto> RegulacaoEventosVistos => Set<RegulacaoEventoVisto>();
    public DbSet<RegulacaoRegra> RegulacaoRegras => Set<RegulacaoRegra>();
    public DbSet<RegulacaoSolicitacaoRespostaRegra> RegulacaoSolicitacaoRespostasRegra =>
        Set<RegulacaoSolicitacaoRespostaRegra>();

    // Indicadores contratuais do HMCML — o motor de cada indicador é o SQL guardado no cadastro
    public DbSet<Indicador> Indicadores => Set<Indicador>();
    public DbSet<IndicadorVersao> IndicadorVersoes => Set<IndicadorVersao>();
    public DbSet<IndicadorExecucao> IndicadorExecucoes => Set<IndicadorExecucao>();

    public DbSet<Equipamento> Equipamentos => Set<Equipamento>();

    /// <summary>O que cada unidade executa de imagem: worklist e aparelho de destino por par
    /// (tipo, unidade). Ver <see cref="TipoExameUnidade"/>.</summary>
    public DbSet<TipoExameUnidade> TiposExameUnidade => Set<TipoExameUnidade>();

    // Mensageria WhatsApp — infraestrutura transversal do município (ADR-0038).
    // NÃO é do TFD: o TFD é um dos consumidores, como qualquer outro módulo.
    public DbSet<MensagemWhatsApp> MensagensWhatsApp => Set<MensagemWhatsApp>();
    public DbSet<WhatsAppConfiguracao> WhatsAppConfiguracao => Set<WhatsAppConfiguracao>();

    // Geo — cache de geocodificação e credencial do Google Maps (ADR-0038).
    public DbSet<GeoEndereco> GeoEnderecos => Set<GeoEndereco>();
    public DbSet<GeoConfiguracao> GeoConfiguracao => Set<GeoConfiguracao>();

    // Módulo TFD propriamente dito (transporte sanitário) — ADR-0017.
    public DbSet<RegistroFaturamento> RegistrosFaturamento => Set<RegistroFaturamento>();
    public DbSet<TfdConfiguracao> TfdConfiguracao => Set<TfdConfiguracao>();

    // Autenticação do cidadão (paciente no app) — credenciais + sessão única por device.
    // NÃO é Usuario/RBAC. Fonte da verdade = CPF. Ver ADR-0018.
    public DbSet<CidadaoAcesso> CidadaoAcessos => Set<CidadaoAcesso>();
    public DbSet<CidadaoSessao> CidadaoSessoes => Set<CidadaoSessao>();
    public DbSet<CidadaoConsentimento> CidadaoConsentimentos => Set<CidadaoConsentimento>();

    // Trilha de auditoria de ações de usuário (append-only). Ver RegistroAuditoria.
    public DbSet<RegistroAuditoria> RegistrosAuditoria => Set<RegistroAuditoria>();

    // Log de erros não tratados (500) com código de referência. Ver RegistroErro.
    public DbSet<RegistroErro> RegistrosErro => Set<RegistroErro>();
    public DbSet<Entities.Alertas.AlertaDestinatario> AlertaDestinatarios => Set<Entities.Alertas.AlertaDestinatario>();
    public DbSet<Entities.Alertas.AlertaOrigem> AlertaOrigens => Set<Entities.Alertas.AlertaOrigem>();
    public DbSet<Entities.Alertas.AlertaEnvio> AlertaEnvios => Set<Entities.Alertas.AlertaEnvio>();

    // Módulo Conversas — chat WhatsApp multi-operador, transversal a todo o SMSMais.
    public DbSet<Conversa> Conversas => Set<Conversa>();
    public DbSet<ConversaEvento> ConversaEventos => Set<ConversaEvento>();

    // Mensagens prontas do chat (texto livre com tags), globais ou por unidade.
    public DbSet<RespostaRapida> RespostasRapidas => Set<RespostaRapida>();
    public DbSet<RespostaRapidaCampo> RespostaRapidaCampos => Set<RespostaRapidaCampo>();
    public DbSet<UsuarioUnidade> UsuarioUnidades => Set<UsuarioUnidade>();

    // Comunicações ao paciente (fila WhatsApp: confirmação/exame liberado/laudo pronto),
    // estado da conversa de cancelamento e registro manual de contatos.
    public DbSet<ComunicacaoPaciente> ComunicacoesPaciente => Set<ComunicacaoPaciente>();
    public DbSet<AgendamentoConfirmacaoEstado> AgendamentoConfirmacaoEstados => Set<AgendamentoConfirmacaoEstado>();
    public DbSet<Entities.Notificacoes.VerificacaoCadastralEstado> VerificacoesCadastraisEstado => Set<Entities.Notificacoes.VerificacaoCadastralEstado>();
    public DbSet<ContatoRegistro> ContatosRegistro => Set<ContatoRegistro>();

    // Consentimento de NÃO validar o WhatsApp (com motivo) — válvula de escape do gate de
    // contato verificado na recepção. Uma ativa por paciente; as revogadas ficam de trilha.
    public DbSet<DispensaVerificacaoContato> DispensasVerificacaoContato => Set<DispensaVerificacaoContato>();

    // Robô de atendimento (WhatsApp): assuntos cadastráveis (com treinos/condições/comandos),
    // fila durável de tarefas, trilha de ações executadas e configuração global (singleton).
    public DbSet<RoboAssunto> RoboAssuntos => Set<RoboAssunto>();
    public DbSet<RoboAssuntoCondicao> RoboAssuntoCondicoes => Set<RoboAssuntoCondicao>();
    public DbSet<RoboAssuntoTreino> RoboAssuntoTreinos => Set<RoboAssuntoTreino>();
    public DbSet<RoboAssuntoComando> RoboAssuntoComandos => Set<RoboAssuntoComando>();
    public DbSet<RoboAtendimentoTarefa> RoboTarefas => Set<RoboAtendimentoTarefa>();
    public DbSet<RoboAcao> RoboAcoes => Set<RoboAcao>();
    public DbSet<RoboConfiguracao> RoboConfiguracoes => Set<RoboConfiguracao>();
    public DbSet<RoboErroResposta> RoboErrosResposta => Set<RoboErroResposta>();

    // Treinamento do robô: a crítica do atendente vira um processo (análise adversarial,
    // alterações com desfazer, pendências para o humano e simulação de verificação).
    public DbSet<RoboTreinamentoItem> RoboTreinamentoItens => Set<RoboTreinamentoItem>();
    public DbSet<RoboTreinamentoPendencia> RoboTreinamentoPendencias => Set<RoboTreinamentoPendencia>();
    public DbSet<RoboTreinamentoAlteracao> RoboTreinamentoAlteracoes => Set<RoboTreinamentoAlteracao>();
    public DbSet<RoboTreinamentoSimulacao> RoboTreinamentoSimulacoes => Set<RoboTreinamentoSimulacao>();

    // Pendências de ajuste de cadastro ("números errados") levantadas no atendimento.
    public DbSet<PendenciaCadastro> PendenciasCadastro => Set<PendenciaCadastro>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaPadrao);
        modelBuilder.HasPostgresExtension("smsmarica", "vector"); // pgvector — embeddings do módulo IA
        modelBuilder.HasPostgresExtension("unaccent"); // busca acento-insensível (chat/#45); instalada no schema public
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SmsMaisDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
