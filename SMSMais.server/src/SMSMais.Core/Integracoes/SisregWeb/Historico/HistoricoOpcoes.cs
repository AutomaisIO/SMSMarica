namespace SMSMais.Core.Integracoes.SisregWeb.Historico;

/// <summary>
/// Parâmetros do motor que traz o passado da agenda (seção <c>Sisreg:Historico</c>).
/// </summary>
public sealed class HistoricoOpcoes
{
    public const string Secao = "Sisreg:Historico";

    /// <summary>Intervalo entre tentativas. Uma fatia por tick, e o tick é curto porque o motor
    /// desiste sozinho sempre que há outro trabalho vivo ou orçamento apertado.</summary>
    public int TickSegundos { get; set; } = 90;

    /// <summary>
    /// Teto do SISREG para o intervalo de exportação. É <b>const</b> porque o disparo manual do
    /// passado (fora do scheduler, logo sem <c>IOptions</c> em mãos) precisa da mesma medida — e
    /// duas fatias de tamanhos diferentes deixariam vãos ou sobreposições na cobertura.
    /// </summary>
    public const int DiasPorFatiaPadrao = 31;

    /// <summary>
    /// Tamanho da fatia. <b>31 é o teto do SISREG</b>, que recusa exportação com intervalo maior —
    /// não é escolha de desempenho.
    /// </summary>
    public int DiasPorFatia { get; set; } = DiasPorFatiaPadrao;

    /// <summary>
    /// Fatias consecutivas vazias que encerram a unidade. <b>Seis ≈ meio ano.</b>
    ///
    /// <para>O TXT traz a unidade inteira (todos os profissionais e procedimentos de uma vez), então
    /// três meses seco já seria conclusivo. Seis é folga deliberada: custa três requisições a mais
    /// por unidade e protege contra o caso de uma agenda que teve um hiato longo — férias coletivas,
    /// reforma, troca de sistema — e voltaria a ter movimento antes disso.</para>
    /// </summary>
    public int FatiasVaziasParaConcluir { get; set; } = 6;

    /// <summary>
    /// Folga de orçamento exigida para o motor tocar. Alta de propósito: o histórico é trabalho de
    /// fundo e não pode ser o motivo de um operador humano encontrar CAPTCHA.
    /// </summary>
    public int OrcamentoMinimo { get; set; } = 120;
}
