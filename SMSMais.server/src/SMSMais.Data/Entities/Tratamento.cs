using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities;

/// <summary>
/// Atendimento do Transporte de Pacientes (na tela: "Atendimento"; no código e no banco segue
/// <c>tratamento</c> — renomear tabela não vale o risco). Liga o paciente a um destino, diz em que
/// dias da semana ele vai e guarda a condição dele para a viagem.
/// </summary>
public class Tratamento
{
    public Guid Id { get; set; }
    public Guid PacienteId { get; set; }

    /// <summary>Destino do transporte — a unidade de atendimento onde o tratamento acontece.
    /// A coordenada dela é o ponto final da rota da van.</summary>
    public Guid UnidadeAtendimentoId { get; set; }

    /// <summary>Tipo do tratamento. Obrigatório em atendimento novo: é dele que vem o tempo médio.
    /// Nulo só em linha legada.</summary>
    public Guid? TipoTratamentoId { get; set; }

    public string Descricao { get; set; } = string.Empty;
    public string? Observacoes { get; set; }

    // ---- Agenda: dias da semana + N sessões OU contínuo

    /// <summary>Primeiro dia a considerar na geração das sessões.</summary>
    public DateOnly DataInicio { get; set; }

    /// <summary>Dias da semana em que há sessão: bit 0 = domingo … bit 6 = sábado (a mesma
    /// convenção do <see cref="DayOfWeek"/>). Faixa 1–127.</summary>
    public int DiasSemanaMascara { get; set; }

    /// <summary>Total de sessões da agenda. Nulo quando <see cref="Continuo"/>.</summary>
    public int? QuantidadeSessoes { get; set; }

    /// <summary>Sem fim previsto: as sessões são geradas de mês em mês (até o fim do mês seguinte),
    /// nunca "para sempre" — o atendimento encerrado ou o óbito param a renovação.</summary>
    public bool Continuo { get; set; }

    /// <summary>Até que dia as sessões já foram geradas. É o horizonte que a renovação do contínuo
    /// estende; no modo N, a data da última sessão gerada.</summary>
    public DateOnly? SessoesGeradasAte { get; set; }

    // ---- Condição do paciente para a viagem (por enquanto só registro; o gerador não lê)

    public MobilidadeTransporte Mobilidade { get; set; } = MobilidadeTransporte.Independente;

    /// <summary>Tem dificuldade para subir em veículo alto (van, micro-ônibus).</summary>
    public bool DificuldadeVeiculoAlto { get; set; }

    /// <summary>Imunodeficiente: viaja só com o próprio acompanhante — veículo exclusivo.</summary>
    public bool Isolamento { get; set; }

    public bool UsaOxigenio { get; set; }

    public bool NecessitaAjuda { get; set; }

    /// <summary>Que ajuda (ex.: "precisa de apoio para descer a escada de casa").</summary>
    public string? AjudaDescricao { get; set; }

    // ---- Acompanhantes

    /// <summary>Quantos acompanhantes podem ir em cada viagem: 1 por direito; 2 só com liberação
    /// explícita (justificativa + quem liberou).</summary>
    public int QuantidadeAcompanhantes { get; set; } = 1;

    public string? SegundoAcompanhanteJustificativa { get; set; }
    public Guid? SegundoAcompanhanteLiberadoPor { get; set; }
    public DateTime? SegundoAcompanhanteLiberadoEm { get; set; }

    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; }
    public DateTime? EncerradoEm { get; set; }

    // PacienteId aponta para fhir.patient (hub FHIR) — sem FK/navegação local.
    public UnidadeAtendimento? UnidadeAtendimento { get; set; }
    public TipoTratamento? TipoTratamento { get; set; }
    public List<SessaoDeTratamento> Sessoes { get; set; } = [];
}
