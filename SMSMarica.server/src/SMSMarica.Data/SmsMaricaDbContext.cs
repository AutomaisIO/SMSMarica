using Microsoft.EntityFrameworkCore;
using SMSMarica.Data.Entities;

namespace SMSMarica.Data;

public sealed class SmsMaricaDbContext(DbContextOptions<SmsMaricaDbContext> options) : DbContext(options)
{
    public const string SchemaPadrao = "smsmarica";

    public DbSet<Paciente> Pacientes => Set<Paciente>();
    public DbSet<Tratamento> Tratamentos => Set<Tratamento>();
    public DbSet<TipoTratamento> TiposTratamento => Set<TipoTratamento>();
    public DbSet<Periodicidade> Periodicidades => Set<Periodicidade>();
    public DbSet<SessaoDeTratamento> Sessoes => Set<SessaoDeTratamento>();
    public DbSet<Unidade> Unidades => Set<Unidade>();
    public DbSet<Veiculo> Veiculos => Set<Veiculo>();
    public DbSet<Fileira> Fileiras => Set<Fileira>();
    public DbSet<Assento> Assentos => Set<Assento>();
    public DbSet<Motorista> Motoristas => Set<Motorista>();
    public DbSet<Medico> Medicos => Set<Medico>();
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaPadrao);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SmsMaricaDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
