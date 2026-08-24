namespace SMSMais.Core.Integracoes.SisregWeb.Varredura;

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
    /// Faixa (hora local de Brasília) em que o <c>expo_solicitacoes</c> fica <b>bloqueado</b> pelo
    /// SISREG: 08:00–15:00 (ver ADR-0040 §1 — "Aplicativo bloqueado para uso de 8 as 15 horas").
    /// Fora dessa faixa o motor roda a qualquer hora — a credencial da SMS é dedicada à varredura,
    /// então não há mais a preocupação de "sessão única" derrubar atendente que motivava a antiga
    /// janela 22:00–06:00.
    /// </summary>
    public TimeOnly BloqueioInicioLocal { get; set; } = new(8, 0);

    public TimeOnly BloqueioFimLocal { get; set; } = new(15, 0);

    /// <summary>
    /// Margem de segurança ANTES do bloqueio: uma varredura não deve INICIAR perto demais das
    /// 08:00, senão uma execução longa (backfill) cruzaria o bloqueio no meio. O corte de entrada é
    /// <c>BloqueioInicio − margem</c> (07:30 com os defaults). 07:30 ainda inicia; 07:31 já não.
    /// </summary>
    public int MargemEntradaMinutos { get; set; } = 30;

    /// <summary>Hora local a partir da qual (exclusive) já não se pode INICIAR uma varredura.</summary>
    public TimeOnly CorteEntradaLocal => BloqueioInicioLocal.Add(TimeSpan.FromMinutes(-MargemEntradaMinutos));

    /// <summary>
    /// Pode INICIAR uma varredura nesta hora local? Recusa a faixa <c>(corteEntrada, bloqueioFim]</c>
    /// = (07:30, 15:00]: 07:30 ainda entra, 15:00 ainda não, depois das 15:00 volta a valer.
    /// </summary>
    public bool PodeIniciarNaHora(TimeOnly hora) =>
        !(hora > CorteEntradaLocal && hora <= BloqueioFimLocal);

    /// <summary>Quanto pausar a unidade quando o CAPTCHA aparece. Relogar não resolve — só um
    /// humano abrindo o SISREG no navegador com aquele operador.</summary>
    public int CaptchaPausaHoras { get; set; } = 24;

    /// <summary>Intervalo do tick do scheduler, em segundos.</summary>
    public int TickSegundos { get; set; } = 60;

    /// <summary>Teto de dias à frente. O SISREG recusa intervalo maior que 31 dias.</summary>
    public const int MaxDiasAFrente = 30;
}
