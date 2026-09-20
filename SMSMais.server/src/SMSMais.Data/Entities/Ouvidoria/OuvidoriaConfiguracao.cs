namespace SMSMais.Data.Entities.Ouvidoria;

/// <summary>
/// Configuração singleton do módulo Ouvidoria: prazos (D-4), arquivamento automático e
/// notificação. Linha única com Id fixo, criada sob demanda pelo service com estes defaults
/// (nunca em migration — ADR-0043: nada institucional em migration).
/// </summary>
public sealed class OuvidoriaConfiguracao
{
    /// <summary>Id fixo da linha singleton.</summary>
    public static readonly Guid IdSingleton = new("77777777-0000-0000-0000-000000000002");

    public Guid Id { get; set; } = IdSingleton;

    /// <summary>Prazo de resposta ao cidadão (Lei 13.460 art. 16: 30 dias).</summary>
    public int PrazoCidadaoDias { get; set; } = 30;

    /// <summary>Dias acrescidos na única prorrogação permitida.</summary>
    public int ProrrogacaoDias { get; set; } = 30;

    /// <summary>Prazo da área para prioridade Normal.</summary>
    public int PrazoAreaDias { get; set; } = 20;

    /// <summary>Prazo da área para prioridade Alta.</summary>
    public int PrazoAreaAltaDias { get; set; } = 10;

    /// <summary>Prazo da área para prioridade Urgente, em dias úteis (seg–sex).</summary>
    public int PrazoAreaUrgenteDiasUteis { get; set; } = 2;

    /// <summary>Dias que o cidadão tem para complementar antes do arquivamento automático.</summary>
    public int ComplementacaoDias { get; set; } = 20;

    /// <summary>Dias após a resposta conclusiva sem recurso até a conclusão automática.</summary>
    public int ArquivamentoAutomaticoDias { get; set; } = 30;

    public bool NotificarPorWhatsApp { get; set; } = true;

    /// <summary>
    /// Mensagem enviada com o protocolo no registro. <c>{protocolo}</c> e <c>{prazo}</c> são
    /// substituídos. Nulo = texto padrão do notificador.
    /// </summary>
    public string? TextoRecibo { get; set; }

    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
}
