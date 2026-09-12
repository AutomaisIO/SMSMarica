namespace SMSMais.Core.Alertas;

public sealed class AlertaPlataformaOptions
{
    public const string SecaoConfig = "AlertaPlataforma";

    /// <summary>Template da Meta usado quando a janela de 24h do destinatário está fechada.</summary>
    public string Template { get; set; } = "erro_plataforma";

    public string Idioma { get; set; } = "pt_BR";

    /// <summary>
    /// O que vai em cada variável do template, na ordem ({{1}}, {{2}}…). Valores aceitos:
    /// <c>resumo</c> (origem — título: detalhe), <c>origem</c>, <c>titulo</c>, <c>detalhe</c>,
    /// <c>descricao</c> (título: detalhe), <c>quando</c> (dd/MM HH:mm de Brasília),
    /// <c>ocorrencias</c>. Se o template aprovado tiver menos variáveis que isto, a sobra é
    /// cortada; se tiver mais, as que faltam saem como "-".
    ///
    /// <para>O <c>erro_plataforma</c> aprovado em 12/09/2026 tem UMA variável:
    /// "A plataforma apresentou o seguinte erro: ⚠️ *{{1}}* Verifique os possíveis impactos desse
    /// aviso." — por isso o padrão é só <c>resumo</c>.</para>
    /// </summary>
    public string[] Parametros { get; set; } = ["resumo"];

    /// <summary>
    /// Teto de avisos enviados por dia (Brasília). Template é mensagem paga: um erro que escapa
    /// do freio não pode virar conta. Passou do teto, o aviso é contado e registrado, não enviado.
    /// </summary>
    public int TetoDiario { get; set; } = 60;
}
