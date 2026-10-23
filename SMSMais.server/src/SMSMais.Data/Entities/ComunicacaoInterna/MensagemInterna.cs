namespace SMSMais.Data.Entities.ComunicacaoInterna;

/// <summary>
/// Mensagem da Comunicação interna (ADR-0079). Não se edita nem se apaga — "fica tudo registrado".
/// Texto e/ou UM anexo (vários arquivos = várias mensagens). O binário do anexo vive no
/// armazenamento de objeto (<c>IArmazenamentoArquivos</c>); aqui fica só a chave e os metadados.
/// </summary>
public sealed class MensagemInterna
{
    public Guid Id { get; set; }
    public Guid ConversaId { get; set; }
    public Guid AutorId { get; set; }

    /// <summary>Unidade de onde a pessoa falou (fotografia do momento: mostra "Ana · UPA").</summary>
    public Guid? AutorUnidadeId { get; set; }

    /// <summary>Texto livre; selo corporativo viaja como <c>:selo:&lt;chave&gt;:</c> (o front desenha).</summary>
    public string? Texto { get; set; }

    public string? AnexoNome { get; set; }
    public string? AnexoMime { get; set; }
    public long? AnexoTamanhoBytes { get; set; }
    public string? AnexoChave { get; set; }

    /// <summary>Áudio gravado no painel (não um arquivo de áudio anexado).</summary>
    public bool AnexoEhAudio { get; set; }
    public int? AnexoDuracaoSegundos { get; set; }

    public DateTime EnviadaEm { get; set; }

    public ConversaInterna? Conversa { get; set; }
    public Usuario? Autor { get; set; }
    public Unidade? AutorUnidade { get; set; }
}
