namespace SMSMais.Data.Entities.Sernit;

/// <summary>Natureza da falha — separa "não coube" de "quebrou".</summary>
public enum TipoFalhaSernit
{
    /// <summary><b>Fatia truncada:</b> a faixa estourou o teto de 100 do SERNIT mesmo reduzida a 1
    /// dia + 1 tipo. Não é erro do motor — é limite da tela, e significa registros não lidos.</summary>
    FatiaTruncada = 1,

    /// <summary>O histórico da solicitação não pôde ser aberto (menu sem o item — situação Alta).</summary>
    HistoricoIndisponivel = 2,

    /// <summary>Erro ao ler/parsear a grade.</summary>
    ErroGrade = 3,

    /// <summary>Erro ao ler/parsear o histórico.</summary>
    ErroHistorico = 4,

    /// <summary>Falha de sessão/autenticação no SERNIT.</summary>
    ErroSessao = 5,

    /// <summary>A trava de somente-leitura recusou a operação — <b>é bug do motor</b>, não do SERNIT.</summary>
    EscritaBloqueada = 6,
}

/// <summary>
/// Uma falha registrada durante a varredura do SERNIT — subsistema irmão do SER-RJ (ADR-0042).
///
/// <para>Existe para que <b>nada seja truncado em silêncio</b>. Cada fatia que não coube no teto de
/// 100 vira uma linha aqui com a faixa exata de datas, para reprocessar só aquele pedaço depois.</para>
/// </summary>
public class SernitVarreduraFalha
{
    public Guid Id { get; set; }

    public Guid ExecucaoId { get; set; }
    public SernitVarreduraExecucao? Execucao { get; set; }

    public TipoFalhaSernit Tipo { get; set; }

    public SituacaoSernit? Situacao { get; set; }

    public DateOnly? FatiaInicio { get; set; }
    public DateOnly? FatiaFim { get; set; }

    public TipoRecursoSernit? TipoRecurso { get; set; }

    /// <summary>ID da solicitação no SERNIT, quando a falha é de uma solicitação específica.</summary>
    public string? IdSernit { get; set; }

    public string Mensagem { get; set; } = string.Empty;

    /// <summary>Detalhe técnico (exceção, trecho de HTML) para diagnóstico. Não vai para a tela.</summary>
    public string? Detalhe { get; set; }

    public DateTime CriadoEm { get; set; }
}
