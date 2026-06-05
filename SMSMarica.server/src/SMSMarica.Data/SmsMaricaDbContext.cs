using Microsoft.EntityFrameworkCore;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Agendamentos;
using SMSMarica.Data.Entities.Ia;
using SMSMarica.Data.Entities.Sisreg;

namespace SMSMarica.Data;

public sealed class SmsMaricaDbContext(DbContextOptions<SmsMaricaDbContext> options) : DbContext(options)
{
    public const string SchemaPadrao = "smsmarica";

    public DbSet<Tratamento> Tratamentos => Set<Tratamento>();
    public DbSet<TipoTratamento> TiposTratamento => Set<TipoTratamento>();
    public DbSet<Periodicidade> Periodicidades => Set<Periodicidade>();
    public DbSet<SessaoDeTratamento> Sessoes => Set<SessaoDeTratamento>();
    public DbSet<Unidade> Unidades => Set<Unidade>();
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
    public DbSet<LaudoTemplate> LaudoTemplates => Set<LaudoTemplate>();
    public DbSet<ProcedimentoSigtap> ProcedimentosSigtap => Set<ProcedimentoSigtap>();
    public DbSet<TipoExame> TiposExame => Set<TipoExame>();
    public DbSet<SolicitacaoExame> SolicitacoesExame => Set<SolicitacaoExame>();

    // Módulo IA (consulta em linguagem natural)
    public DbSet<IaConfiguracao> IaConfiguracoes => Set<IaConfiguracao>();
    public DbSet<IaFonte> IaFontes => Set<IaFonte>();
    public DbSet<IaDocumentoConhecimento> IaDocumentosConhecimento => Set<IaDocumentoConhecimento>();
    public DbSet<IaChunkConhecimento> IaChunksConhecimento => Set<IaChunkConhecimento>();
    public DbSet<IaAprendizado> IaAprendizados => Set<IaAprendizado>();
    public DbSet<IaConsulta> IaConsultas => Set<IaConsulta>();
    public DbSet<IaCorrecao> IaCorrecoes => Set<IaCorrecao>();

    // Integração SISREG (feed de leitura) — ADR-0012
    public DbSet<SisregConfiguracao> SisregConfiguracoes => Set<SisregConfiguracao>();

    // Domínio de Agendamento (Especialidade → Agenda → Agendamento) — ADR-0012
    public DbSet<Especialidade> Especialidades => Set<Especialidade>();
    public DbSet<Agenda> Agendas => Set<Agenda>();
    public DbSet<DisponibilidadeRecorrente> DisponibilidadesRecorrentes => Set<DisponibilidadeRecorrente>();
    public DbSet<DisponibilidadeAvulsa> DisponibilidadesAvulsas => Set<DisponibilidadeAvulsa>();
    public DbSet<BloqueioAgenda> BloqueiosAgenda => Set<BloqueioAgenda>();
    public DbSet<Agendamento> Agendamentos => Set<Agendamento>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaPadrao);
        modelBuilder.HasPostgresExtension("smsmarica", "vector"); // pgvector — embeddings do módulo IA
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SmsMaricaDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
