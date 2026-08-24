namespace SMSMais.Data.Entities.Sisreg;

/// <summary>
/// Detalhe de UMA combinação profissional × procedimento dentro de uma varredura
/// (<see cref="SisregVarreduraExecucao"/>).
///
/// <para>Existe porque o agregado da execução responde "quanto entrou no total", mas não "quanto de
/// cada": o histórico detalhado precisa dizer, por profissional e por procedimento, quantos
/// agendamentos foram lidos, quantos viraram solicitação nova, quantos já existiam e quantos
/// caíram em pendência. Uma linha por par, gravada ao fim de cada combinação — então uma varredura
/// interrompida (parcial) preserva o detalhe do que já rodou, igual ao agregado.</para>
/// </summary>
public class SisregVarreduraExecucaoItem
{
    public Guid Id { get; set; }

    public Guid ExecucaoId { get; set; }

    /// <summary>CPF do profissional (só dígitos). Não sai na API — o DTO devolve só o nome; fica
    /// aqui para o mesmo motivo do cursor da execução: rastrear/depurar o par exato consultado.</summary>
    public string ProfissionalCpf { get; set; } = string.Empty;

    public string ProfissionalNome { get; set; } = string.Empty;

    /// <summary>Código do procedimento no SISREG (7 dígitos, ou grupo terminado em 000).</summary>
    public string ProcedimentoCodigo { get; set; } = string.Empty;

    public string ProcedimentoNome { get; set; } = string.Empty;

    /// <summary>Requisições HTTP gastas nesta combinação (inclui as páginas extras do split de teto).</summary>
    public int Requisicoes { get; set; }

    /// <summary>Agendamentos únicos lidos nesta combinação (deduplicados por nº de solicitação).</summary>
    public int RegistrosEncontrados { get; set; }

    /// <summary>Marcações honradas — solicitação criada OU já existente. <c>Validos - JaExistiam</c>
    /// é o que efetivamente entrou novo.</summary>
    public int Validos { get; set; }

    public int Invalidos { get; set; }
    public int JaExistiam { get; set; }

    /// <summary>Observação legível quando algo foge do trivial (ex.: pendências geradas). Null no
    /// caminho feliz — a contagem já fala.</summary>
    public string? Observacao { get; set; }

    public SisregVarreduraExecucao? Execucao { get; set; }
}
