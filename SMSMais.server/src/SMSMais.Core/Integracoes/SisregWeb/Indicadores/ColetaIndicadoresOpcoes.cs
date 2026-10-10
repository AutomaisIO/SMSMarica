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
    /// Idade mínima de uma semana para a lista de faltas valer como número OFICIAL. A lista é a
    /// marcação de falta feita pela unidade executante, e as unidades apontam (e corrigem) com atraso
    /// — lida cedo, ela ainda não é o retrato final da semana.
    /// </summary>
    public int DiasParaFaltas { get; set; } = 30;

    /// <summary>
    /// De quanto em quanto tempo a lista de faltas das semanas RECENTES (mais novas que
    /// <see cref="DiasParaFaltas"/>) é relida, em minutos. <c>0</c> desliga.
    ///
    /// <para>É o que mantém a ficha do paciente em dia durante o expediente: a lista é da rede
    /// inteira, uma janela por semana (~5 janelas, 2 requisições cada), e muda conforme as unidades
    /// apontam as faltas. 50 e não 60 porque o plano roda de hora em hora — com 60, um minuto de
    /// atraso empurraria a releitura para a hora seguinte.</para>
    ///
    /// <para>Não entra no indicador de absenteísmo: esse só lê o que o coletor oficial gravou.</para>
    /// </summary>
    public int MinutosParaRelerFaltasRecentes { get; set; } = 50;

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

    /// <summary>
    /// Espera depois de o SISREG recusar o login, em minutos. Curta: em 09/10/2026, na troca de endereço
    /// do SISREG, o login voltou sozinho em meia hora. Fica só na memória — um restart tenta de novo.
    /// </summary>
    public int MinutosDeEsperaNoLoginRecusado { get; set; } = 15;
}
