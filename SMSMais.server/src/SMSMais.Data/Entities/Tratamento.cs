namespace SMSMais.Data.Entities;

public class Tratamento
{
    public Guid Id { get; set; }
    public Guid PacienteId { get; set; }

    /// <summary>Destino do transporte — a unidade de atendimento onde o tratamento acontece.
    /// A coordenada dela é o ponto final da rota da van.</summary>
    public Guid UnidadeAtendimentoId { get; set; }

    public Guid? TipoTratamentoId { get; set; }

    public string Descricao { get; set; } = string.Empty;
    public string? CodigoSusLiberacao { get; set; }
    public string? Observacoes { get; set; }

    /// <summary>Horário previsto padrão da busca — herdado por sessões sem horário próprio.</summary>
    public TimeOnly? HoraPrevistaBusca { get; set; }

    /// <summary>Tempo médio que o paciente fica no tratamento, em minutos (da chegada à
    /// liberação). Base para prever a volta no cálculo da rota. Nulo só em linha legada.</summary>
    public int? TempoMedioMinutos { get; set; }

    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; }
    public DateTime? EncerradoEm { get; set; }

    // PacienteId aponta para fhir.patient (hub FHIR) — sem FK/navegação local.
    public UnidadeAtendimento? UnidadeAtendimento { get; set; }
    public TipoTratamento? TipoTratamento { get; set; }
    public Periodicidade? Periodicidade { get; set; }
    public List<SessaoDeTratamento> Sessoes { get; set; } = [];
}
