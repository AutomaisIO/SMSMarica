namespace SMSMais.Data.Entities.Sernit;

/// <summary>Onde o rascunho está no caminho até o SERNIT.</summary>
public enum StatusRascunhoSernit
{
    Rascunho = 1,
    Pronto = 2,
    Enviado = 3,
    Falhou = 4,
}

/// <summary>
/// Um pedido montado na NOSSA base, pronto para ser enviado ao SERNIT quando autorizado. Espelho
/// do <c>SerSolicitacaoRascunho</c>, <b>sem "ambulatório estadual"</b> (o SERNIT não tem esse gate).
/// Os valores ficam num JSON só porque o conjunto de campos muda por recurso.
/// </summary>
public class SernitSolicitacaoRascunho
{
    public Guid Id { get; set; }

    public StatusRascunhoSernit Status { get; set; } = StatusRascunhoSernit.Rascunho;

    public TipoRecursoSernit? Tipo { get; set; }

    /// <summary>Valor do recurso no SERNIT (o <c>value</c> do combo).</summary>
    public string? RecursoValor { get; set; }

    /// <summary>Rótulo do recurso no momento da escolha — guardado para a lista continuar legível.</summary>
    public string? RecursoRotulo { get; set; }

    public string? Cns { get; set; }
    public string? PacienteNome { get; set; }

    /// <summary>Hipótese diagnóstica (`form0:procedimento`), duplicada para a lista.</summary>
    public string? Hipotese { get; set; }

    /// <summary>Todos os valores do formulário, com os nomes JSF do SERNIT como chave.</summary>
    public string CamposJson { get; set; } = "{}";

    /// <summary>Número que o SERNIT devolveu, quando o envio deu certo.</summary>
    public string? IdSernitGerado { get; set; }

    public string? MensagemErro { get; set; }

    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public string? CriadoPorNome { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public DateTime? EnviadoEm { get; set; }

    public ICollection<SernitRascunhoAnexo> Anexos { get; set; } = [];
}

/// <summary>Arquivo anexado ao rascunho (binário em <c>smsmarica.midia</c>; aqui só a referência).
/// Sobe para o SERNIT só na hora do envio.</summary>
public class SernitRascunhoAnexo
{
    public Guid Id { get; set; }

    public Guid RascunhoId { get; set; }
    public SernitSolicitacaoRascunho? Rascunho { get; set; }

    /// <summary>FK para <c>smsmarica.midia</c>.</summary>
    public Guid MidiaId { get; set; }

    public string NomeArquivo { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long Tamanho { get; set; }

    public DateTime? EnviadoEm { get; set; }
    public DateTime CriadoEm { get; set; }
}
