namespace SMSMais.Data.Entities.Pep;

/// <summary>
/// Agenda do sincronismo contínuo (ADR-0024). Uma linha por base (<c>IaFonte</c>): o
/// scheduler enfileira execuções incrementais quando <see cref="ProximoRunEm"/> vence.
/// Estado de runtime (backoff, falhas consecutivas) mora aqui, não no cadastro da fonte.
/// </summary>
public class PepSincronizacaoAgenda
{
    /// <summary>Base (IaFonte) agendada. Chave primária.</summary>
    public Guid FonteId { get; set; }

    /// <summary>Liga/desliga o sincronismo contínuo desta base.</summary>
    public bool Ativo { get; set; }

    /// <summary>Intervalo entre execuções agendadas, em minutos.</summary>
    public int IntervaloMinutos { get; set; } = 30;

    /// <summary>
    /// Janela de execução opcional em hora LOCAL de Brasília (ex.: 06:00–23:00). Nulas =
    /// roda o dia inteiro. Início &gt; fim significa janela cruzando a meia-noite.
    /// </summary>
    public TimeOnly? JanelaInicioLocal { get; set; }

    public TimeOnly? JanelaFimLocal { get; set; }

    /// <summary>Idade máxima da marca de médicos antes de forçar re-scan integral (horas).</summary>
    public int MedicoRescanHoras { get; set; } = 24;

    /// <summary>Falhas consecutivas de execuções agendadas — alimenta o backoff exponencial.</summary>
    public int FalhasConsecutivas { get; set; }

    /// <summary>Próximo disparo elegível (UTC). Null = elegível já.</summary>
    public DateTime? ProximoRunEm { get; set; }

    /// <summary>Pausa administrativa até este instante (UTC), sem desligar a agenda.</summary>
    public DateTime? PausadoAte { get; set; }

    public DateTime AtualizadoEm { get; set; }
}
