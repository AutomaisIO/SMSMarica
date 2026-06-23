namespace SMSMarica.Data.Entities;

/// <summary>
/// Arquivo binário genérico guardado no banco (bytea). Pensado para imagens
/// reutilizáveis por qualquer feature — hoje cabeçalho/rodapé de laudo, amanhã
/// logo do app, anexos, foto de unidade, etc. Servido por
/// <c>GET /midias/{id}</c> (anônimo: é conteúdo institucional, não PII).
/// </summary>
public class Midia
{
    public Guid Id { get; set; }

    public string NomeArquivo { get; set; } = string.Empty;

    /// <summary>Content-Type (ex.: <c>image/png</c>, <c>image/jpeg</c>, <c>image/svg+xml</c>).</summary>
    public string MimeType { get; set; } = string.Empty;

    public byte[] Conteudo { get; set; } = [];

    public long TamanhoBytes { get; set; }

    /// <summary>Largura em px (quando imagem; opcional).</summary>
    public int? Largura { get; set; }

    /// <summary>Altura em px (quando imagem; opcional).</summary>
    public int? Altura { get; set; }

    /// <summary>
    /// Tag livre para agrupar/filtrar por uso (ex.: <c>laudo-cabecalho</c>).
    /// Não impõe FK — é só organização.
    /// </summary>
    public string? Categoria { get; set; }

    /// <summary>SHA-256 do conteúdo (hex). Permite deduplicar uploads idênticos.</summary>
    public string HashSha256 { get; set; } = string.Empty;

    public Guid? CriadoPorUsuarioId { get; set; }
    public Usuario? CriadoPorUsuario { get; set; }

    public DateTime CriadoEm { get; set; }
}
