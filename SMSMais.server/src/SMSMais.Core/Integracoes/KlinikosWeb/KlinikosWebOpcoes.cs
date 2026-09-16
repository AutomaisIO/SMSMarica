namespace SMSMais.Core.Integracoes.KlinikosWeb;

/// <summary>
/// Configuração do conector web do Klinikos (seção <c>KlinikosWeb</c> no appsettings/ambiente).
/// Tudo INERTE por padrão: a escrita no hub e os schedulers só ligam por opção explícita — o
/// deploy não muda nada por si só.
/// </summary>
public sealed class KlinikosWebOpcoes
{
    public const string Secao = "KlinikosWeb";

    /// <summary>
    /// Trava mestra da ESCRITA no hub. Enquanto <c>false</c> (padrão), <c>GravarEspinhaAsync</c> e
    /// <c>BackfillAsync</c> recusam gravar — mesmo chamados pelo endpoint. Ligar é decisão de
    /// operação, depois do teste de paridade aprovado.
    /// </summary>
    public bool EscritaHabilitada { get; set; }

    /// <summary>Schedulers de fundo (via rápida / fechados / 526 noturno / drenador). Padrão: desligados.</summary>
    public bool SchedulersHabilitados { get; set; }
}
