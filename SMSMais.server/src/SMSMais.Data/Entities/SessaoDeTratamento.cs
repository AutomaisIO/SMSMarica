using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities;

/// <summary>
/// Sessão programada de um tratamento. Nasce da agenda do atendimento (dias da semana + N sessões
/// ou contínuo), só com a data — o horário de busca vem da rota. Vira um translado físico quando
/// for alocada em uma RotaDiaria. Campos de realização (opção B) guardam o que aconteceu de fato —
/// podem divergir do planejado.
/// </summary>
public class SessaoDeTratamento
{
    public Guid Id { get; set; }
    public Guid TratamentoId { get; set; }

    // Planejamento
    public DateOnly DataPrevista { get; set; }
    public TimeOnly? HoraPrevistaBusca { get; set; }
    public TimeOnly? HoraPrevistaRetorno { get; set; }

    public StatusSessao Status { get; set; } = StatusSessao.Pendente;

    // Realização (preenchidos quando o gestor confirma)
    public DateTime? RealizadaEm { get; set; }
    public Guid? ConfirmadaPorUsuarioId { get; set; }

    /// <summary>Texto livre de antes da lista de acompanhantes — só histórico. Quem vai numa
    /// viagem agora é <see cref="Acompanhantes"/>.</summary>
    public string? NomeAcompanhante { get; set; }
    public string? ParentescoAcompanhante { get; set; }

    // Confirmação prévia de acompanhante (FT2) — antes do dia, pelo paciente (WhatsApp/App)
    public bool? AcompanhanteEsperado { get; set; }
    public DateTime? AcompanhanteConfirmadoEm { get; set; }
    public CanalConfirmacao? AcompanhanteCanal { get; set; }

    // Ida
    public Guid? MotoristaIdaId { get; set; }
    public Guid? VeiculoIdaId { get; set; }
    public TimeOnly? HoraSaidaResidencia { get; set; }
    public TimeOnly? HoraChegadaUnidade { get; set; }

    // Volta
    public Guid? MotoristaVoltaId { get; set; }
    public Guid? VeiculoVoltaId { get; set; }
    public TimeOnly? HoraSaidaUnidade { get; set; }
    public TimeOnly? HoraChegadaResidencia { get; set; }

    public string? MotivoNaoRealizacao { get; set; }
    public string? Observacoes { get; set; }

    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }

    public Tratamento? Tratamento { get; set; }

    /// <summary>Quem vai acompanhar nesta viagem (da lista do paciente, até o limite do atendimento).</summary>
    public List<SessaoAcompanhante> Acompanhantes { get; set; } = [];
}
