using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities;

/// <summary>
/// Ocorrência reportada pelo motorista no tablet do <see cref="Veiculo"/> (botão REPORT): acidente,
/// pane, paciente passou mal ou outro. A central acompanha no painel e anda a
/// <see cref="Situacao"/> (Aberta → EmAtendimento → Encerrada). Contrato do tablet:
/// docs/modulos/tfd/deslocamento-tablet.md.
/// </summary>
public class OcorrenciaVeiculo
{
    public Guid Id { get; set; }
    public Guid VeiculoId { get; set; }

    /// <summary>Tablet que reportou. Sem FK, como em <see cref="PosicaoVeiculo"/>: o registro fica
    /// mesmo se o tablet for trocado.</summary>
    public Guid DispositivoId { get; set; }

    /// <summary>Gerado no tablet: o reenvio da fila offline devolve a mesma ocorrência em vez de
    /// duplicar.</summary>
    public Guid IdLocal { get; set; }

    public TipoOcorrenciaVeiculo Tipo { get; set; }
    public string? Descricao { get; set; }

    /// <summary>Posição no momento do REPORT; null quando o tablet ainda não tinha GPS.</summary>
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? PrecisaoM { get; set; }

    /// <summary>Rota do dia (translado) em andamento no tablet, quando houver.</summary>
    public Guid? RotaId { get; set; }

    /// <summary>Relógio do tablet (UTC).</summary>
    public DateTime OcorridaEm { get; set; }
    public DateTime RecebidaEm { get; set; }

    public SituacaoOcorrenciaVeiculo Situacao { get; set; } = SituacaoOcorrenciaVeiculo.Aberta;
    public DateTime? AtendidaEm { get; set; }
    public Guid? AtendidaPor { get; set; }
    public DateTime? EncerradaEm { get; set; }
    public Guid? EncerradaPor { get; set; }
    public string? ObservacaoCentral { get; set; }

    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }
}
