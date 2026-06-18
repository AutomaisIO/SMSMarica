using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Data.Entities;

/// <summary>
/// Sessão programada de um tratamento. Nasce no cadastro do tratamento
/// (data prevista + horário previsto de busca). Vira um translado físico
/// quando for alocada em uma RotaDiaria. Campos de realização (opção B)
/// guardam o que aconteceu de fato — podem divergir do planejado.
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
}
