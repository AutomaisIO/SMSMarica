using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities;

/// <summary>
/// Cada coisa que o tablet registrou no translado: início, embarque, ausência, desembarque e
/// conclusão. Só se acrescenta. O que conta é o transporte feito: o desembarque do paciente no
/// destino marca a viagem como realizada, a ausência na coleta como não realizada, e o painel corrige.
/// Chega pela fila sem rede do tablet; <see cref="IdLocal"/> impede duplicar.
/// </summary>
public class EventoViagem
{
    public Guid Id { get; set; }
    public Guid IdLocal { get; set; }
    public Guid RotaId { get; set; }
    public Guid VeiculoId { get; set; }
    public Guid DispositivoId { get; set; }

    /// <summary>Quem estava ao volante (o motorista conferido no tablet).</summary>
    public Guid? MotoristaId { get; set; }

    public TipoEventoViagem Tipo { get; set; }

    /// <summary>Sessão do paciente, nas marcações de pessoa (embarque, ausência, desembarque).</summary>
    public Guid? SessaoId { get; set; }
    public PessoaViagem? Pessoa { get; set; }

    /// <summary>Qual acompanhante da sessão: a posição dele na lista (1 ou 2) e, quando há
    /// cadastro, o id.</summary>
    public int? AcompanhanteOrdem { get; set; }
    public Guid? AcompanhanteId { get; set; }

    /// <summary>Marcação da volta (embarque na unidade, chegada em casa) — e não da ida.</summary>
    public bool Volta { get; set; }

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? PrecisaoM { get; set; }

    /// <summary>Hora no tablet (UTC) e de chegada ao servidor.</summary>
    public DateTime OcorridoEm { get; set; }
    public DateTime RecebidoEm { get; set; }

    /// <summary>Se mudou a rota ou a sessão. Não muda quando a central já corrigiu a viagem no painel
    /// (vale o painel), a sessão foi cancelada ou a rota já tinha acabado — o motivo fica ao lado.</summary>
    public bool Aplicado { get; set; }
    public string? MotivoNaoAplicado { get; set; }
}
