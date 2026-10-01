using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.DocumentosPaciente;

/// <summary>
/// De que tipo é um item do acervo. O acervo junta o que o paciente tem guardado em lugares
/// diferentes: documentos soltos, PDFs da anamnese, laudos assinados e as imagens dos exames.
/// </summary>
public enum TipoItemAcervo
{
    /// <summary>Documento do acervo propriamente dito (<c>documento_paciente</c>).</summary>
    Documento = 1,

    /// <summary>PDF anexado na anamnese pela ponte QR (<c>documento_exame</c>, salvo).</summary>
    AnexoExame = 2,

    /// <summary>Laudo — só aparece quando ASSINADO.</summary>
    Laudo = 3,

    /// <summary>PDF das imagens de um exame de imagem (gerado a partir do PACS).</summary>
    ImagensExame = 4,
}

/// <summary>
/// Um item do acervo do paciente. <see cref="Chave"/> (<c>tipo:id</c>) é o que o front devolve
/// para abrir ou anexar o item numa solicitação.
/// </summary>
public sealed record ItemAcervoDto(
    string Chave,
    TipoItemAcervo Tipo,
    Guid Id,
    string Titulo,
    string? Descricao,
    string MimeType,
    long? TamanhoBytes,
    int? Paginas,
    DateTime Data,
    string Origem,
    SituacaoDocumentoPaciente? Situacao,
    bool Editavel);

/// <summary>Conteúdo de um item do acervo, pronto para servir ou copiar.</summary>
public sealed record ConteudoAcervo(byte[] Conteudo, string MimeType, string NomeArquivo, string Titulo, string? Descricao);

/// <summary>Documento novo no acervo — vale para todas as origens.</summary>
public sealed record NovoDocumentoPaciente(
    Guid PacienteId,
    string Titulo,
    string? Descricao,
    string NomeArquivo,
    string MimeType,
    byte[] Conteudo,
    OrigemDocumentoPaciente Origem,
    string? OrigemReferencia,
    SituacaoDocumentoPaciente Situacao);

/// <summary>Título e descrição que a equipe dá (ou corrige) num documento do acervo.</summary>
public sealed record EditarDocumentoPacienteRequest(string Titulo, string? Descricao);
