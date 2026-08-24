namespace SMSMais.Data.Entities;

public class Tratamento
{
    public Guid Id { get; set; }
    public Guid PacienteId { get; set; }
    public Guid UnidadeId { get; set; }
    public Guid? TipoTratamentoId { get; set; }

    public string Descricao { get; set; } = string.Empty;
    public string? CodigoSusLiberacao { get; set; }
    public string? Observacoes { get; set; }

    /// <summary>Horário previsto padrão da busca — herdado por sessões sem horário próprio.</summary>
    public TimeOnly? HoraPrevistaBusca { get; set; }

    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; }
    public DateTime? EncerradoEm { get; set; }

    // PacienteId aponta para fhir.patient (hub FHIR) — sem FK/navegação local.
    public Unidade? Unidade { get; set; }
    public TipoTratamento? TipoTratamento { get; set; }
    public Periodicidade? Periodicidade { get; set; }
    public List<SessaoDeTratamento> Sessoes { get; set; } = [];
}
