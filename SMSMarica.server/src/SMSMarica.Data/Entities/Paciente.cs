using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Data.Entities;

public class Paciente
{
    public Guid Id { get; set; }

    // Identificação
    public string NomeCompleto { get; set; } = string.Empty;
    public string Cpf { get; set; } = string.Empty;
    public string? Cns { get; set; }
    public string? Rg { get; set; }
    public DateOnly? DataNascimento { get; set; }
    public Sexo Sexo { get; set; } = Sexo.NaoInformado;
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

    // Endereço (nullable até ser preenchido)
    public Endereco? Endereco { get; set; }
    public Gps? GpsResidencia { get; set; }

    // Contatos
    public string? TelefonePrincipal { get; set; }
    public string? TelefoneCelular { get; set; }
    public string? TelefoneResidencial { get; set; }
    public string? Email { get; set; }
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
    public string? FotoBase64 { get; set; }

    // Controle
    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }
}
