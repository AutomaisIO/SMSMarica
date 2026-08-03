namespace SMSMarica.Core.Integracoes.SisregWeb.Varredura;

/// <summary>
/// Parâmetros do motor de varredura (seção <c>Sisreg:Varredura</c> do appsettings).
///
/// <para><b>Os defaults não são estéticos.</b> Eles saem de medição no SISREG real, registrada em
/// <c>Automais.SISREG/docs/APRENDIZADOS.md</c>: o CAPTCHA anti-robô aparece por volta de 700
/// requisições, e uma varredura do CDT custou 352. Mexer nestes números sem entender isso é a
/// forma mais fácil de bloquear o operador da unidade — e só um humano no navegador destrava.</para>
/// </summary>
public sealed class VarreduraSisregOpcoes
{
    public const string Secao = "Sisreg:Varredura";

    /// <summary>
    /// Teto de requisições por execução. O limite observado é ~700 por operador; 500 deixa folga
    /// para o CADSUS (que sai pela mesma sessão) e para o operador humano usar o SISREG no dia.
    ///
    /// <para><b>Nota sobre a incerteza:</b> não sabemos se o limite é por operador ou por IP — o
    /// laboratório nunca isolou as duas variáveis. Trabalhamos com "por operador" porque relogar
    /// (sessão nova, mesmo IP) não limpa o bloqueio, e o destravamento é amarrado ao operador. Se
    /// for por IP, o sintoma será <c>SISREG_VARREDURA_CAPTCHA</c> em unidades diferentes na mesma
    /// noite — aí a correção é espaçar as horas e/ou acrescentar um teto global.</para>
    /// </summary>
    public int TetoPorExecucao { get; set; } = 500;

    /// <summary>Pausa entre requisições. Mesmo valor do script Python que rodou sem bloquear.</summary>
    public int PausaMs { get; set; } = 350;

    /// <summary>
    /// Janela em que o motor pode rodar (hora local de Brasília). <b>Sessão única por operador:</b>
    /// varrer às 10h com a credencial da unidade derruba o atendente da recepção.
    /// </summary>
    public TimeOnly JanelaInicioLocal { get; set; } = new(22, 0);

    public TimeOnly JanelaFimLocal { get; set; } = new(6, 0);

    /// <summary>Quanto pausar a unidade quando o CAPTCHA aparece. Relogar não resolve — só um
    /// humano abrindo o SISREG no navegador com aquele operador.</summary>
    public int CaptchaPausaHoras { get; set; } = 24;

    /// <summary>Freio contra loop de paginação mal parseada.</summary>
    public int MaxPaginasPorCombinacao { get; set; } = 40;

    /// <summary>Intervalo do tick do scheduler, em segundos.</summary>
    public int TickSegundos { get; set; } = 60;

    /// <summary>Teto de dias à frente. O SISREG recusa intervalo maior que 31 dias.</summary>
    public const int MaxDiasAFrente = 30;
}
