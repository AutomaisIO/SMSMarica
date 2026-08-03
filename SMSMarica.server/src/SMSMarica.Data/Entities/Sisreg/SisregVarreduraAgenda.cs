namespace SMSMarica.Data.Entities.Sisreg;

/// <summary>
/// Configuração do SISREG de UMA unidade. Uma linha por unidade, com dois assuntos: a agenda do
/// motor diário de varredura e o gatilho de confirmação ao paciente
/// (<see cref="EnviarConfirmacao"/>, que vale para <b>toda</b> importação da unidade, inclusive
/// upload de arquivo — por isso a linha pode existir numa unidade que nunca varre).
///
/// <para>O scheduler dispara a varredura quando <see cref="ProximoRunEm"/> vence, varrendo apenas
/// as combinações profissional × procedimento habilitadas no mapeamento daquela unidade.</para>
///
/// <para><b>Por que a hora é configurável e a janela é estreita:</b> o SISREG mantém <b>uma
/// sessão por operador</b> — o login do motor derruba o atendente da recepção que estiver usando
/// a mesma credencial, e vice-versa. Por isso a varredura roda de madrugada, e a validação recusa
/// hora fora da janela configurada em <c>Sisreg:Varredura</c>.</para>
///
/// <para><b>Cursor:</b> quando o SISREG dispara o CAPTCHA anti-robô no meio da varredura, a
/// execução para como parcial e guarda em qual par (profissional, procedimento) parou. O run do
/// dia seguinte retoma dali — mas só se a janela de datas ainda for a mesma, porque janela vencida
/// é passado e refazer o passado é gastar orçamento com dado que não muda mais.</para>
/// </summary>
public class SisregVarreduraAgenda
{
    /// <summary>Unidade varrida. Chave primária — uma agenda por unidade.</summary>
    public Guid UnidadeId { get; set; }

    /// <summary>Liga/desliga a varredura diária desta unidade.</summary>
    public bool Ativo { get; set; }

    /// <summary>
    /// Gatilho mestre da unidade: ao importar uma solicitação, avisar o paciente por WhatsApp?
    /// Vale para <b>toda</b> importação da unidade — varredura e upload de arquivo.
    ///
    /// <para>Combina com <c>SisregProcedimentoSigtap.EnviarConfirmacao</c> por "E": desligar aqui
    /// corta tudo; ligado aqui, cada procedimento ainda pode vetar o seu.</para>
    ///
    /// <para><b>Nasce ligado</b>, e unidade SEM linha nesta tabela também envia — o default
    /// preserva o comportamento que roda em produção.</para>
    /// </summary>
    public bool EnviarConfirmacao { get; set; } = true;

    /// <summary>Hora do disparo diário, em hora LOCAL de Brasília.</summary>
    public TimeOnly HoraLocal { get; set; } = new(4, 30);

    /// <summary>
    /// Quantos dias à frente varrer: <c>dataInicial = hoje</c>, <c>dataFinal = hoje + N</c>.
    /// Para trás não se varre — a cada dia se puxa o que ainda "tem para fazer".
    /// Limitado a 30 porque o SISREG recusa intervalo maior que 31 dias.
    /// </summary>
    public int DiasAFrente { get; set; } = 21;

    /// <summary>Falhas consecutivas de execuções agendadas — alimenta o backoff.</summary>
    public int FalhasConsecutivas { get; set; }

    /// <summary>
    /// Próximo disparo elegível (UTC). <b>NULL = NÃO elegível</b> — o inverso da agenda do PEP,
    /// e de propósito: numa agenda "diária às HH:mm", tratar null como "elegível já" faria o
    /// primeiro boot após um deploy disparar a varredura no meio da tarde, derrubando a sessão
    /// do atendente. Quem liga a agenda calcula o próximo horário na hora de salvar.
    /// </summary>
    public DateTime? ProximoRunEm { get; set; }

    /// <summary>Pausa administrativa até este instante (UTC), sem desligar a agenda.
    /// O CAPTCHA empurra a unidade para cá, porque relogar não resolve.</summary>
    public DateTime? PausadoAte { get; set; }

    public DateTime? UltimaExecucaoEm { get; set; }

    /// <summary>Última execução desta unidade. Sem FK — o rastreio não pode travar a agenda.</summary>
    public Guid? UltimaExecucaoId { get; set; }

    /// <summary>Último par (profissional, procedimento) CONCLUÍDO antes de a varredura parar.</summary>
    public string? CursorProfissionalCpf { get; set; }

    public string? CursorProcedimentoCodigo { get; set; }

    /// <summary>Fim da janela de datas do run parcial. O cursor só vale enquanto for &gt;= hoje.</summary>
    public DateOnly? CursorJanelaFim { get; set; }

    public DateTime AtualizadoEm { get; set; }
}
