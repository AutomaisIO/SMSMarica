using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities.Notificacoes;

/// <summary>Em que pé está a guia de um agendamento na fila de busca.</summary>
public enum SituacaoGuiaAgendamento
{
    /// <summary>Na fila: ainda não veio. O baixador do sistema a pega no ritmo configurado.</summary>
    Pendente = 1,

    /// <summary>Baixada e guardada no acervo do paciente (<see cref="GuiaAgendamento.DocumentoPacienteId"/>).</summary>
    Obtida = 2,

    /// <summary>Desistiu depois de esgotar as tentativas — o erro fica em <see cref="GuiaAgendamento.UltimoErro"/>.</summary>
    Falhou = 3,

    /// <summary>Saiu da fila sem buscar: o agendamento deixou de valer (remarcado, desmarcado) ou o
    /// atendimento passou antes de a guia vir.</summary>
    Descartada = 4,
}

/// <summary>
/// A guia de um agendamento regulado (ADR-0076) — e, ao mesmo tempo, a <b>fila</b> de busca: a linha
/// nasce vazia, sem arquivo, quando o agendamento aparece, e o baixador do sistema a preenche no
/// ritmo configurado (<see cref="GuiaFilaConfiguracao"/>). "Quantas faltam" é contar as pendentes.
///
/// <para>Âncora por sistema: <see cref="AgendamentoExternoId"/> no SER, no SERNIT e no ESUS (o
/// agendamento já é pedido × data, ADR-0072); <see cref="SolicitacaoId"/> + <see cref="DataAtendimento"/>
/// no SISREG (a remarcação muda a data da mesma solicitação). Exatamente uma das duas.</para>
///
/// <para>Uma vez obtida, a guia mora UMA vez no acervo do paciente; a tela do pedido, o cartão do
/// agendamento no app e a lista de documentos apontam para o mesmo <see cref="DocumentoPaciente"/>.
/// No SER e no SERNIT emitir gasta uma via no sistema de destino — não contamos vias aqui, e a guia
/// não é emitida de novo.</para>
/// </summary>
public class GuiaAgendamento
{
    public Guid Id { get; set; }

    public SistemaRegulacao Sistema { get; set; }

    /// <summary>SER/SERNIT/ESUS SG.</summary>
    public Guid? AgendamentoExternoId { get; set; }

    /// <summary>SISREG (a espinha).</summary>
    public Guid? SolicitacaoId { get; set; }

    /// <summary>Paciente (fhir.patient) — sem FK, como o resto do lado clínico.</summary>
    public Guid PacienteId { get; set; }

    /// <summary>Dia do atendimento (Brasília). Ordena a fila: o mais perto sai primeiro.</summary>
    public DateOnly DataAtendimento { get; set; }

    public SituacaoGuiaAgendamento Situacao { get; set; } = SituacaoGuiaAgendamento.Pendente;

    /// <summary>O PDF no acervo do paciente — preenchido quando <see cref="SituacaoGuiaAgendamento.Obtida"/>.</summary>
    public Guid? DocumentoPacienteId { get; set; }

    public int Tentativas { get; set; }
    public DateTime? UltimaTentativaEm { get; set; }

    /// <summary>Antes disto o baixador não tenta de novo (espera crescente entre falhas).</summary>
    public DateTime? ProximaTentativaEm { get; set; }

    public string? UltimoErro { get; set; }
    public DateTime? ObtidaEm { get; set; }

    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }

    /// <summary>Concorrência otimista (PG xmin).</summary>
    public uint RowVersion { get; set; }
}

/// <summary>
/// O ritmo da fila de guias de UM sistema de regulação — o mesmo formato para todos (ADR-0076):
/// ligado, quantas por hora e em que janela do dia. Uma linha por sistema; sem linha, vale o padrão
/// de <see cref="GuiaFilaConfiguracao.Padrao"/>.
/// </summary>
public class GuiaFilaConfiguracao
{
    public SistemaRegulacao Sistema { get; set; }

    public bool Habilitada { get; set; } = true;

    /// <summary>Guias por hora. O baixador espera <c>3600 / PorHora</c> segundos entre uma e outra.</summary>
    public int PorHora { get; set; }

    /// <summary>Início da janela (Brasília). Janela que vira o dia (22:00–06:00) vale.</summary>
    public TimeOnly JanelaInicio { get; set; }

    /// <summary>Fim da janela (Brasília). Igual ao início = o dia inteiro.</summary>
    public TimeOnly JanelaFim { get; set; }

    /// <summary>A fila do sistema está parada até aqui (ex.: o SISREG pediu CAPTCHA). Quem pausa é o
    /// baixador; passa sozinho quando vence.</summary>
    public DateTime? PausadaAte { get; set; }
    public string? MotivoPausa { get; set; }

    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }

    /// <summary>
    /// Padrão de quem nunca configurou: o dia inteiro. SER/SERNIT/ESUS a 60 por hora (cada guia custa
    /// 1–2 requisições, e a busca já espera a varredura do sistema). SISREG mais devagar, a 30 por
    /// hora: a credencial é a dedicada do sincronismo, mas o orçamento anti-robô é dividido com
    /// varredura, importação, escalas e indicadores — e a fila ainda cede a eles e ao orçamento.
    /// </summary>
    public static GuiaFilaConfiguracao Padrao(SistemaRegulacao sistema) => new()
    {
        Sistema = sistema,
        Habilitada = true,
        PorHora = sistema == SistemaRegulacao.Sisreg ? 30 : 60,
        JanelaInicio = new(0, 0),
        JanelaFim = new(0, 0),
    };
}
