namespace SMSMais.Data.Entities.Ser;

/// <summary>Onde o rascunho está no caminho até o SER.</summary>
public enum StatusRascunhoSer
{
    /// <summary>Em edição. Pode faltar campo obrigatório.</summary>
    Rascunho = 1,

    /// <summary>Conferido e completo, esperando autorização para ir ao SER.</summary>
    Pronto = 2,

    /// <summary>Foi criado no SER. <see cref="SerSolicitacaoRascunho.IdSerGerado"/> tem o número.</summary>
    Enviado = 3,

    /// <summary>Tentou ir e o SER recusou. A mensagem dele fica em
    /// <see cref="SerSolicitacaoRascunho.MensagemErro"/>.</summary>
    Falhou = 4,
}

/// <summary>
/// Um pedido montado na NOSSA base, pronto para ser enviado ao SER quando for autorizado.
///
/// <para><b>Por que existe:</b> sem isto, preencher o formulário e sair da tela perdia tudo — não
/// dava para preparar hoje e disparar amanhã, nem montar um lote. O rascunho é o que transforma
/// "a tela está pronta para disparar" em "o pedido está pronto esperando disparo".</para>
///
/// <para>Os valores ficam num JSON só (<see cref="CamposJson"/>) porque o conjunto de campos
/// MUDA por recurso — 21 formulários distintos, 163 campos únicos. Colunas fixas exigiriam
/// migration a cada mexida da SES-RJ numa especialidade.</para>
/// </summary>
public class SerSolicitacaoRascunho
{
    public Guid Id { get; set; }

    public StatusRascunhoSer Status { get; set; } = StatusRascunhoSer.Rascunho;

    public TipoRecursoSer? Tipo { get; set; }

    /// <summary>
    /// Resposta a "É AMBULATÓRIO ESTADUAL?" (<c>form0:comboSisReg</c>). Fica em coluna própria,
    /// e não dentro do JSON, porque decide QUAIS recursos existem e QUAIS campos o recurso pede —
    /// é o primeiro campo do formulário e o que dá sentido a <see cref="RecursoValor"/>.
    /// </summary>
    public bool? AmbulatorioEstadual { get; set; }

    /// <summary>Valor do recurso no SER (o <c>value</c> do combo).</summary>
    public string? RecursoValor { get; set; }

    /// <summary>Rótulo do recurso no momento em que foi escolhido — guardado junto para a lista
    /// continuar legível mesmo se o catálogo mudar depois.</summary>
    public string? RecursoRotulo { get; set; }

    /// <summary>CNS do paciente. Fora do JSON porque é o que a listagem mostra e busca.</summary>
    public string? Cns { get; set; }

    /// <summary>Nome do paciente, quando conhecido — só para a listagem ser legível.</summary>
    public string? PacienteNome { get; set; }

    /// <summary>Hipótese diagnóstica (<c>form0:procedimento</c>), também duplicada para a lista.</summary>
    public string? Hipotese { get; set; }

    /// <summary>Todos os valores do formulário, com os nomes JSF do SER como chave. É exatamente
    /// o que seria postado.</summary>
    public string CamposJson { get; set; } = "{}";

    /// <summary>Número que o SER devolveu, quando o envio deu certo.</summary>
    public string? IdSerGerado { get; set; }

    public string? MensagemErro { get; set; }

    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public string? CriadoPorNome { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public DateTime? EnviadoEm { get; set; }

    public ICollection<SerRascunhoAnexo> Anexos { get; set; } = [];
}

/// <summary>
/// Arquivo anexado ao rascunho, guardado na NOSSA base — ele sobe para o SER só na hora do envio.
///
/// <para>O binário mora em <c>smsmarica.midia</c> (dedup por SHA-256), o mesmo lugar dos anexos
/// de ticket: aqui fica só a referência.</para>
///
/// <para><b>No SER, o anexo é o passo mais delicado do envio</b> — são duas conversas Seam
/// distintas (o <c>form0</c> do pedido e o <c>formAnexar</c> do arquivo) amarradas pela mesma
/// sessão, e o arquivo sobe ANTES de o pedido ser gravado (docs/ser-criar-solicitacao.md §2.3).
/// Guardar localmente antes torna esse passo repetível: se a subida falhar no meio, o arquivo
/// continua aqui e a tentativa recomeça sem o operador reanexar nada.</para>
/// </summary>
public class SerRascunhoAnexo
{
    public Guid Id { get; set; }

    public Guid RascunhoId { get; set; }
    public SerSolicitacaoRascunho? Rascunho { get; set; }

    /// <summary>FK para <c>smsmarica.midia</c>.</summary>
    public Guid MidiaId { get; set; }

    public string NomeArquivo { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long Tamanho { get; set; }

    /// <summary>Quando ESTE arquivo chegou ao SER. Nulo = ainda só nosso.</summary>
    public DateTime? EnviadoEm { get; set; }

    public DateTime CriadoEm { get; set; }
}
