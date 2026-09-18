namespace SMSMais.Data.Entities.Notificacoes;

/// <summary>
/// Singleton (Id fixo) com as tarifas usadas para ESTIMAR o custo Meta das mensagens enviadas e o
/// mapa template → categoria de cobrança. Editado no painel (Mensageria → Regras); nunca seedado
/// em migration (regra 9 do CLAUDE.md: nada institucional em migration). Sem linha = sem
/// estimativa.
///
/// <para>Preços em USD por mensagem de template entregue, na categoria de cobrança da Meta
/// (utility / marketing / authentication). Mensagem de sessão (texto livre na janela de 24h) e
/// utility dentro de janela aberta não custam.</para>
/// </summary>
public class MensageriaConfiguracao
{
    public static readonly Guid IdFixo = new("00000000-0000-0000-0000-00000000006d");

    public Guid Id { get; set; } = IdFixo;

    public decimal? TarifaUtilityUsd { get; set; }
    public decimal? TarifaMarketingUsd { get; set; }
    public decimal? TarifaAuthenticationUsd { get; set; }

    /// <summary>JSON <c>{"nome_do_template":"utility"|"marketing"|"authentication"}</c>.
    /// Template sem entrada é tratado como utility (é o caso de todos os nossos, menos o OTP).</summary>
    public string? TemplatesCategoriasJson { get; set; }

    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
}
