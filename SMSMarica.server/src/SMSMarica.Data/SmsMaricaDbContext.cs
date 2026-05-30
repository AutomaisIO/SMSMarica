using Microsoft.EntityFrameworkCore;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Fhir;
using SMSMarica.Data.Entities.Fhir.Lookups;

namespace SMSMarica.Data;

public sealed class SmsMaricaDbContext(DbContextOptions<SmsMaricaDbContext> options) : DbContext(options)
{
    public const string SchemaPadrao = "smsmarica";
    public const string SchemaFhir = "fhir";

    // ---- schema: smsmarica (regras de negócio) ----
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

    // ---- schema: fhir (modelo FHIR R4 canônico) ----
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<PatientIdentifier> PatientIdentifiers => Set<PatientIdentifier>();
    public DbSet<PatientName> PatientNames => Set<PatientName>();
    public DbSet<PatientAddress> PatientAddresses => Set<PatientAddress>();
    public DbSet<PatientTelecom> PatientTelecoms => Set<PatientTelecom>();
    public DbSet<PatientContact> PatientContacts => Set<PatientContact>();
    public DbSet<PatientCommunication> PatientCommunications => Set<PatientCommunication>();
    public DbSet<PatientLink> PatientLinks => Set<PatientLink>();
    public DbSet<PatientPhoto> PatientPhotos => Set<PatientPhoto>();
    public DbSet<PatientDisability> PatientDisabilities => Set<PatientDisability>();
    public DbSet<Practitioner> Practitioners => Set<Practitioner>();
    public DbSet<PractitionerIdentifier> PractitionerIdentifiers => Set<PractitionerIdentifier>();
    public DbSet<PractitionerName> PractitionerNames => Set<PractitionerName>();
    public DbSet<PractitionerAddress> PractitionerAddresses => Set<PractitionerAddress>();
    public DbSet<PractitionerTelecom> PractitionerTelecoms => Set<PractitionerTelecom>();
    public DbSet<PractitionerQualification> PractitionerQualifications => Set<PractitionerQualification>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationIdentifier> OrganizationIdentifiers => Set<OrganizationIdentifier>();
    public DbSet<Consent> Consents => Set<Consent>();
    public DbSet<MunicipioIbge> MunicipiosIbge => Set<MunicipioIbge>();
    public DbSet<PaisIso> PaisesIso => Set<PaisIso>();
    public DbSet<CboOcupacao> CbosOcupacao => Set<CboOcupacao>();
    public DbSet<EtniaIndigena> EtniasIndigenas => Set<EtniaIndigena>();
    public DbSet<BarreiraComunicacao> BarreirasComunicacao => Set<BarreiraComunicacao>();
    public DbSet<Religiao> Religioes => Set<Religiao>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaPadrao);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SmsMaricaDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
