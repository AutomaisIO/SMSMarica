namespace SMSMais.Core.Integracoes.SisregWeb.MapeamentoLote;

/// <summary>
/// Parâmetros do motor que sincroniza o mapeamento (profissionais + procedimentos) de <b>todas</b>
/// as unidades configuradas de uma vez (seção <c>Sisreg:MapeamentoLote</c> do appsettings).
///
/// <para><b>O ritmo não é estético.</b> O mapeamento e a varredura de agenda dividem o mesmo
/// orçamento anti-robô do SISREG (mesmo IP de saída, ver ADR-0040 e <c>docs/sisreg-egress.md</c>):
/// o CAPTCHA aparece por volta de 700 requisições por operador. Por isso o lote roda as unidades
/// <b>em sequência</b> (nunca em paralelo) e espaça as requisições para caber em
/// <see cref="RequisicoesPorHora"/> — o teto que o operador pediu no #118.</para>
/// </summary>
public sealed class SisregMapeamentoLoteOpcoes
{
    public const string Secao = "Sisreg:MapeamentoLote";

    /// <summary>Teto de requisições ao SISREG por hora durante o lote. Default 500.</summary>
    public int RequisicoesPorHora { get; set; } = 500;

    /// <summary>Intervalo do tick do scheduler diário, em segundos.</summary>
    public int TickSegundos { get; set; } = 60;

    /// <summary>Intervalo mínimo entre requisições, derivado de <see cref="RequisicoesPorHora"/>.</summary>
    public TimeSpan IntervaloMinimoRequisicao =>
        TimeSpan.FromSeconds(3600.0 / Math.Max(1, RequisicoesPorHora));
}
