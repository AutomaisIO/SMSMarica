using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Data.Entities;

/// <summary>
/// Papel profissional de <see cref="Usuario"/> (1:1) — cidadão atendido pelo
/// programa. Carrega apenas campos específicos (CNS, dados clínicos,
/// filiação, GPS de residência, contato de emergência). Dados pessoais base
/// (nome, CPF, RG, data de nascimento, sexo, endereço, foto, telefone
/// principal, e-mail) vivem em <see cref="Usuario"/>. Ver ADR-0005.
/// </summary>
public class Paciente
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    // Identificação específica
    /// <summary>Nome pelo qual o paciente prefere ser chamado (opcional).</summary>
    public string? NomeSocial { get; set; }
    public string? Cns { get; set; }
    public EstadoCivil EstadoCivil { get; set; } = EstadoCivil.NaoInformado;
    public RacaCor RacaCor { get; set; } = RacaCor.NaoInformado;
    public Escolaridade Escolaridade { get; set; } = Escolaridade.NaoInformado;
    public string? Ocupacao { get; set; }
    public string? Naturalidade { get; set; }
    public string Nacionalidade { get; set; } = "Brasileira";

    // Filiação
    public string? NomeDaMae { get; set; }
    public string? NomeDoPai { get; set; }
    public string? ResponsavelLegal { get; set; }

    // GPS de residência (específico de translado)
    public Gps? GpsResidencia { get; set; }

    // Contatos secundários (Principal vai pra Usuario.Telefone)
    public string? TelefoneCelular { get; set; }
    public string? TelefoneResidencial { get; set; }
    public ContatoEmergencia? ContatoEmergencia { get; set; }

    // Dados de saúde
    public int? AlturaCm { get; set; }
    public decimal? PesoKg { get; set; }
    public TipoSanguineo TipoSanguineo { get; set; } = TipoSanguineo.NaoInformado;
    public FatorRh FatorRh { get; set; } = FatorRh.NaoInformado;
    public List<string> Alergias { get; set; } = [];
    public List<string> MedicamentosContinuos { get; set; } = [];
    public List<string> Comorbidades { get; set; } = [];
    public List<string> Deficiencias { get; set; } = [];
    public string? PlanoSaude { get; set; }

    // Outros
    public string? Observacoes { get; set; }

    // Auditoria (sem flag Ativo — Usuario.Ativo trata acesso; ExcluidoEm trata exclusão).
    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
    public DateTime? ExcluidoEm { get; set; }
    public Guid? ExcluidoPor { get; set; }
}
