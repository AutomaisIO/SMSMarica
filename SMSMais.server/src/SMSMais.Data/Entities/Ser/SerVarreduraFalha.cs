namespace SMSMais.Data.Entities.Ser;

/// <summary>Natureza da falha — separa "não coube" de "quebrou".</summary>
public enum TipoFalhaSer
{
    /// <summary><b>Fatia truncada:</b> a faixa estourou o teto de 100 do SER mesmo reduzida a 1
    /// dia + 1 tipo. Não é erro do motor — é limite da tela, e significa registros não lidos.</summary>
    FatiaTruncada = 1,

    /// <summary>O histórico da solicitação não pôde ser aberto (menu sem o item — situação Alta).</summary>
    HistoricoIndisponivel = 2,

    /// <summary>Erro ao ler/parsear a grade.</summary>
    ErroGrade = 3,

    /// <summary>Erro ao ler/parsear o histórico.</summary>
    ErroHistorico = 4,

    /// <summary>Falha de sessão/autenticação no SER.</summary>
    ErroSessao = 5,

    /// <summary>A trava de somente-leitura recusou a operação — <b>é bug do motor</b>, não do SER,
    /// e precisa aparecer para quem opera.</summary>
    EscritaBloqueada = 6,
}

/// <summary>
/// Uma falha registrada durante a varredura do SER — ADR-0042.
///
/// <para>Existe para que <b>nada seja truncado em silêncio</b>. Cada fatia que não coube no teto
/// de 100 vira uma linha aqui com a faixa exata de datas, de modo que dê para reprocessar só
/// aquele pedaço depois (por Recurso, por exemplo) em vez de refazer a base inteira.</para>
/// </summary>
public class SerVarreduraFalha
{
    public Guid Id { get; set; }

    public Guid ExecucaoId { get; set; }
    public SerVarreduraExecucao? Execucao { get; set; }

    public TipoFalhaSer Tipo { get; set; }

    /// <summary>Situação que estava sendo varrida quando falhou.</summary>
    public SituacaoSer? Situacao { get; set; }

    /// <summary>Faixa de <c>Data da Solicitação</c> da fatia (preenchida em
    /// <see cref="TipoFalhaSer.FatiaTruncada"/>) — é o que permite reprocessar só ela.</summary>
    public DateOnly? FatiaInicio { get; set; }
    public DateOnly? FatiaFim { get; set; }

    /// <summary>Tipo do recurso usado no fallback de fatiamento, quando houve.</summary>
    public TipoRecursoSer? TipoRecurso { get; set; }

    /// <summary>ID da solicitação no SER, quando a falha é de uma solicitação específica.</summary>
    public string? IdSer { get; set; }

    public string Mensagem { get; set; } = string.Empty;

    /// <summary>Detalhe técnico (exceção, trecho de HTML) para diagnóstico. Não vai para a tela.</summary>
    public string? Detalhe { get; set; }

    public DateTime CriadoEm { get; set; }
}
