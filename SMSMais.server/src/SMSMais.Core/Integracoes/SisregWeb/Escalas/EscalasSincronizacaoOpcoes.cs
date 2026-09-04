namespace SMSMais.Core.Integracoes.SisregWeb.Escalas;

/// <summary>
/// Parâmetros do sincronismo de ESCALAS (seção <c>Sisreg:Escalas</c> do appsettings).
///
/// <para><b>É o motor mais barato de todos.</b> A tela <c>cons_escalas</c> aceita recorte sem
/// critério nenhum — medido em 04/09/2026: um único POST com unidade, profissional, procedimento e
/// datas vazios devolveu a rede inteira, 5,9 MB e 17.469 linhas. Compare com o lote de mapeamento
/// (~1.500 requisições) e com a varredura de agenda (uma por unidade, por fatia de dias). Por isso
/// aqui não há rodízio, TTL nem fatiamento: <b>uma requisição cobre tudo</b>.</para>
///
/// <para>Também não sofre o bloqueio de horário do <c>expo_solicitacoes</c> (08:00–15:00) — o que
/// não quer dizer que valha rodar no expediente: a sessão do SISREG é única por operador, e o
/// motor derruba quem estiver usando a mesma credencial. Daí o padrão de madrugada.</para>
/// </summary>
public sealed class EscalasSincronizacaoOpcoes
{
    public const string Secao = "Sisreg:Escalas";

    /// <summary>De quanto em quanto tempo o scheduler acorda para ver se está na hora.</summary>
    public int TickSegundos { get; set; } = 60;

    /// <summary>
    /// Janela (minutos) depois da hora alvo em que ainda vale disparar. Cobre um tick perdido sem
    /// depender de estado persistido, e impede o re-disparo no mesmo dia.
    /// </summary>
    public int JanelaDisparoMinutos { get; set; } = 10;

    /// <summary>
    /// Hora padrão do disparo diário quando ninguém configurou. Madrugada, e <b>deslocada</b> da
    /// hora padrão do lote de mapeamento (03:30): os dois motores se recusam mutuamente quando um
    /// está vivo, então colar os horários faria um deles simplesmente não rodar todo dia.
    /// </summary>
    public string HoraPadrao { get; set; } = "02:30";

    /// <summary>
    /// Requisições que precisam estar livres no orçamento anti-robô para começar. O sincronismo
    /// gasta 1 (mais o login, quando a sessão caiu), mas exigir folga evita entrar na frente de um
    /// operador humano que está a poucas requisições do CAPTCHA.
    /// </summary>
    public int OrcamentoMinimo { get; set; } = 20;
}
