namespace SMSMais.Data.Entities.EsusSg;

/// <summary>Natureza da falha — separa "não fechou a conta" de "quebrou".</summary>
public enum TipoFalhaEsusSg
{
    /// <summary>O mês de agendados (ou a fila) não fechou lido = declarado.</summary>
    ContagemNaoFechou = 1,

    ErroFila = 2,
    ErroAgendados = 3,

    /// <summary>Falha de login/sessão no ESUS.</summary>
    ErroSessao = 4,

    /// <summary>A trava de somente-leitura recusou a chamada — <b>é bug do motor</b>.</summary>
    EscritaBloqueada = 5,
}

/// <summary>Uma falha registrada na rodada do ESUS SG — para nada ficar incompleto em silêncio.</summary>
public class EsusSgVarreduraFalha
{
    public Guid Id { get; set; }

    public Guid ExecucaoId { get; set; }
    public EsusSgVarreduraExecucao? Execucao { get; set; }

    public TipoFalhaEsusSg Tipo { get; set; }

    public TipoRecursoEsusSg? TipoRecurso { get; set; }

    /// <summary>Mês de agendados envolvido (primeiro dia), quando for o caso.</summary>
    public DateOnly? Mes { get; set; }

    public string Mensagem { get; set; } = string.Empty;

    /// <summary>Detalhe técnico para diagnóstico. Não vai para a tela.</summary>
    public string? Detalhe { get; set; }

    public DateTime CriadoEm { get; set; }
}
