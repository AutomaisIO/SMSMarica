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
    /// Idade a partir da qual uma execução ainda "rodando" é considerada <b>abandonada</b>.
    ///
    /// <para>O estado vivo mora em memória e o banco não: um restart deixa a linha em
    /// <c>EmExecucao</c> para sempre, e o motor esperaria por ela indefinidamente. A detecção é por
    /// IDADE e não por "tem algo vivo agora" — essa segunda versão causou, em 06/09/2026, um laço
    /// que gastou 57 requisições em uma hora repetindo a mesma fatia: a execução é criada antes de
    /// o runner registrar-se como viva, então o tick seguinte a lia como órfã e disparava outra,
    /// que matava a anterior.</para>
    ///
    /// <para>45 minutos é o dobro largo de uma fatia real (~20 min medidos no CDT). Errar para mais
    /// custa esperar; errar para menos custa o laço.</para>
    /// </summary>
    public int MinutosParaAbandonada { get; set; } = 45;

    /// <summary>
    /// Espera mínima antes de repetir uma fatia que falhou. Impede que um erro determinístico —
    /// unidade sem permissão, procedimento que o SISREG recusa — vire uma repetição a cada tick.
    /// </summary>
    public int MinutosEntreTentativas { get; set; } = 15;

    /// <summary>
    /// Tentativas na MESMA fatia antes de o motor desistir e se desligar.
    ///
    /// <para>Sem teto, uma janela que falha sempre queima orçamento para sempre. Desligar é
    /// preferível a insistir: o operador vê parado, olha a última execução e decide.</para>
    /// </summary>
    public int TentativasPorFatia { get; set; } = 3;

    /// <summary>
    /// Folga de orçamento exigida para o motor tocar. Alta de propósito: o histórico é trabalho de
    /// fundo e não pode ser o motivo de um operador humano encontrar CAPTCHA.
    /// </summary>
    public int OrcamentoMinimo { get; set; } = 120;
}
