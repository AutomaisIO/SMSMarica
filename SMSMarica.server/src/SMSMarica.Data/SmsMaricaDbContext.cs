using Microsoft.EntityFrameworkCore;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Agendamentos;
using SMSMarica.Data.Entities.Conversas;
using SMSMarica.Data.Entities.Ia;
using SMSMarica.Data.Entities.Integracoes;
using SMSMarica.Data.Entities.Pep;
using SMSMarica.Data.Entities.Ser;
using SMSMarica.Data.Entities.Sisreg;
using SMSMarica.Data.Entities.Geo;
using SMSMarica.Data.Entities.Notificacoes;
using SMSMarica.Data.Entities.Tfd;

namespace SMSMarica.Data;

public sealed class SmsMaricaDbContext(DbContextOptions<SmsMaricaDbContext> options) : DbContext(options)
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
    public DbSet<SisregVarreduraAgenda> SisregVarreduraAgendas => Set<SisregVarreduraAgenda>();
    public DbSet<SisregVarreduraExecucao> SisregVarreduraExecucoes => Set<SisregVarreduraExecucao>();
    public DbSet<SisregVarreduraExecucaoItem> SisregVarreduraExecucaoItens => Set<SisregVarreduraExecucaoItem>();

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

    // Indicadores contratuais do HMCML — o motor de cada indicador é o SQL guardado no cadastro
    public DbSet<Indicador> Indicadores => Set<Indicador>();
    public DbSet<IndicadorVersao> IndicadorVersoes => Set<IndicadorVersao>();
    public DbSet<IndicadorExecucao> IndicadorExecucoes => Set<IndicadorExecucao>();

    // Domínio de Agendamento (Especialidade → Agenda → Agendamento) — ADR-0012/0013
    public DbSet<Especialidade> Especialidades => Set<Especialidade>();
    public DbSet<Equipamento> Equipamentos => Set<Equipamento>();
    public DbSet<Agenda> Agendas => Set<Agenda>();
    public DbSet<DisponibilidadeRecorrente> DisponibilidadesRecorrentes => Set<DisponibilidadeRecorrente>();
    public DbSet<DisponibilidadeAvulsa> DisponibilidadesAvulsas => Set<DisponibilidadeAvulsa>();
    public DbSet<BloqueioAgenda> BloqueiosAgenda => Set<BloqueioAgenda>();
    public DbSet<Agendamento> Agendamentos => Set<Agendamento>();

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

    // Módulo Conversas — chat WhatsApp multi-operador, transversal a todo o SMSMarica.
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
    public DbSet<ContatoRegistro> ContatosRegistro => Set<ContatoRegistro>();

    // Consentimento de NÃO validar o WhatsApp (com motivo) — válvula de escape do gate de
    // contato verificado na recepção. Uma ativa por paciente; as revogadas ficam de trilha.
    public DbSet<DispensaVerificacaoContato> DispensasVerificacaoContato => Set<DispensaVerificacaoContato>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaPadrao);
        modelBuilder.HasPostgresExtension("smsmarica", "vector"); // pgvector — embeddings do módulo IA
        modelBuilder.HasPostgresExtension("unaccent"); // busca acento-insensível (chat/#45); instalada no schema public
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SmsMaricaDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
