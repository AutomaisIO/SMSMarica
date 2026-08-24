using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities.Pep;

/// <summary>
/// Histórico de uma execução de importação de uma base de PEP para o hub FHIR.
/// Guarda parâmetros, status, cronometragem (total + por fase em <see cref="TemposJson"/>),
/// contadores por tipo de recurso e a lista de falhas por paciente (<see cref="FalhasJson"/>).
/// A base é referenciada por <see cref="FonteId"/> (uma <c>IaFonte</c>); o nome é
/// "carimbado" em <see cref="FonteNome"/> para o histórico sobreviver à edição da fonte.
/// </summary>
public class PepSincronizacaoExecucao
{
    public Guid Id { get; set; }

    public Guid FonteId { get; set; }
    public string FonteNome { get; set; } = string.Empty;

    public ModoSincronizacao Modo { get; set; }
    public EscopoSincronizacao Escopo { get; set; }
    public bool ApagarAntes { get; set; }
    public int? MaxMedicos { get; set; }
    public int? MaxPacientes { get; set; }

    public StatusSincronizacao Status { get; set; } = StatusSincronizacao.Pendente;

    /// <summary>Origem do disparo (operador ou scheduler). Runs antigos ficam como Manual.</summary>
    public DisparoSincronizacao Disparo { get; set; } = DisparoSincronizacao.Manual;

    public DateTime IniciadoEm { get; set; }
    public DateTime? FinalizadoEm { get; set; }
    public double? DuracaoSegundos { get; set; }

    public int Medicos { get; set; }
    public int Pacientes { get; set; }
    public int Encounters { get; set; }
    public int Conditions { get; set; }
    public int MedicationRequests { get; set; }
    public int DocumentReferences { get; set; }
    public int Observations { get; set; }
    public int Falhas { get; set; }

    /// <summary>
    /// Quantos dos <see cref="Pacientes"/> já estavam idênticos no hub e não viraram escrita.
    /// Sem este número o contador bruto engana: num ciclo incremental a maior parte do que entra
    /// é <b>releitura obrigatória</b> — internação em curso volta todo poll (ADR-0025), o que no
    /// HMCML são ~150 pacientes fixos por ciclo. Quem mudou é <c>Pacientes - PacientesInalterados</c>.
    /// </summary>
    public int PacientesInalterados { get; set; }

    /// <summary>O mesmo para <see cref="Medicos"/>, que é re-scan integral do cadastro.</summary>
    public int MedicosInalterados { get; set; }

    /// <summary>Duração por fase (JSON: ex. {"medicos":1.2,"pacientes":3.4,"atendimentos":42.1}).</summary>
    public string? TemposJson { get; set; }

    /// <summary>Falhas por paciente (JSON: lista de {cd, mensagem}).</summary>
    public string? FalhasJson { get; set; }

    public string? MensagemErro { get; set; }

    /// <summary>Usuário que disparou a importação.</summary>
    public Guid? CriadoPor { get; set; }
}
