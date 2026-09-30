namespace SMSMais.Core.Integracoes.SisregWeb.Indicadores;

/// <summary>
/// Parâmetros do coletor dos Indicadores de Regulação (seção <c>Sisreg:Indicadores</c>). Ligar e
/// desligar NÃO mora aqui: mora no banco, com tela (<see cref="ColetaIndicadoresConfig"/>).
/// </summary>
public sealed class ColetaIndicadoresOpcoes
{
    public const string Secao = "Sisreg:Indicadores";

    /// <summary>Um passo (uma requisição) por tick. 30 s dá no máximo 120 por hora, abaixo do teto.</summary>
    public int TickSegundos { get; set; } = 30;

    /// <summary>
    /// Teto do PRÓPRIO coletor numa janela rolante de 60 minutos. O orçamento anti-robô é do
    /// operador e todos os motores gastam dele; o coletor é trabalho de fundo e fica bem abaixo.
    /// </summary>
    public int TetoPorHora { get; set; } = 150;

    /// <summary>Folga exigida no orçamento GLOBAL (todos os motores) para o coletor tocar.</summary>
    public int OrcamentoMinimo { get; set; } = 120;

    /// <summary>
    /// Faixa em que o coletor pode rodar (Brasília). Fora dela está a varredura das agendas
    /// (18:00–01:20), que é quem mais gasta sessão; a releitura da fila (03:00) é respeitada pelo
    /// estado vivo dela.
    /// </summary>
    public string HoraInicio { get; set; } = "01:20";

    public string HoraFim { get; set; } = "18:00";

    /// <summary>
    /// Idade mínima de uma semana para a lista de faltas ser lida. A lista encolhe enquanto as
    /// unidades confirmam a chegada com atraso — ler cedo mostraria como falta quem foi atendido.
    /// </summary>
    public int DiasParaFaltas { get; set; } = 30;

    /// <summary>Quantos meses fechados para trás o coletor mantém em dia. O passado mais antigo já foi carregado.</summary>
    public int MesesRecentes { get; set; } = 3;

    /// <summary>A PPI de um mês é lida a partir deste dia do mês seguinte (a "usada" ainda se mexe nos primeiros dias).</summary>
    public int DiaDaPpi { get; set; } = 5;

    /// <summary>
    /// Devolvidas/negadas de um mês de solicitação continuam aparecendo por meses. Uma unidade×mês já
    /// lida é relida quando a última leitura passa desta idade (dentro de <see cref="MesesRecentes"/>).
    /// </summary>
    public int DiasParaReler { get; set; } = 25;

    /// <summary>
    /// Páginas da amostra de motivos por mês (20 linhas cada). Seis dão ~120 cancelamentos por mês — o
    /// suficiente para a distribuição por categoria — a 6 requisições por mês, contra ~120 para ler o mês
    /// inteiro.
    /// </summary>
    public int PaginasDaAmostra { get; set; } = 6;

    /// <summary>Tentativas de um item antes de ele ficar em falha até alguém re-armar pela tela.</summary>
    public int MaximoTentativas { get; set; } = 6;

    /// <summary>
    /// Trava de encolhimento: uma releitura que troca uma janela por menos que esta fração do que
    /// havia é recusada. Protege contra resposta truncada que por acaso passou pelas outras provas.
    /// </summary>
    public double FracaoMinimaNaSubstituicao { get; set; } = 0.7;

    /// <summary>Pausa depois de CAPTCHA: o SISREG bloqueia o operador por cerca de um dia.</summary>
    public int HorasDePausaNoCaptcha { get; set; } = 24;
}
