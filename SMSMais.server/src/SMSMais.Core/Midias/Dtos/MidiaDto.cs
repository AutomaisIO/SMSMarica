namespace SMSMais.Core.Midias.Dtos;

/// <summary>Metadados de uma mídia (sem o binário). Retornado no upload.</summary>
public sealed record MidiaDto(
    Guid Id,
    string NomeArquivo,
    string MimeType,
    long TamanhoBytes,
    int? Largura,
    int? Altura,
    string? Categoria,
    DateTime CriadoEm)
{
    /// <summary>URL relativa para servir o binário (usada como <c>src</c> no editor/HTML).</summary>
    public string Url => $"/midias/{Id}";
}

/// <summary>Binário cru de uma mídia, para servir no endpoint GET.</summary>
public sealed record MidiaConteudo(byte[] Conteudo, string MimeType, string NomeArquivo);
