namespace SMSMarica.Data.Entities;

/// <summary>
/// Configuração global (singleton) do cabeçalho e rodapé institucional dos
/// laudos. Apenas uma linha existe — identificada pela PK fixa
/// <see cref="IdSingleton"/>. O conteúdo é HTML editado no painel (mesma stack
/// TipTap do corpo do laudo) e renderizado no PDF pelo <c>LaudoPdfRenderer</c>.
/// Substitui, em runtime, os textos estáticos de <c>appsettings:Laudos:Pdf</c>.
/// </summary>
public class LaudoConfiguracao
{
    /// <summary>PK fixa do registro único (padrão singleton).</summary>
    public static readonly Guid IdSingleton = new("00000000-0000-0000-0000-00000000ffff");

    public Guid Id { get; set; } = IdSingleton;

    /// <summary>Documento ProseMirror/TipTap do cabeçalho (re-editável).</summary>
    public string CabecalhoJson { get; set; } = "{}";

    /// <summary>HTML sanitizado do cabeçalho (renderizado no topo de cada página do PDF).</summary>
    public string CabecalhoHtml { get; set; } = string.Empty;

    /// <summary>Documento ProseMirror/TipTap do rodapé (re-editável).</summary>
    public string RodapeJson { get; set; } = "{}";

    /// <summary>HTML sanitizado do rodapé (renderizado no fim de cada página do PDF).</summary>
    public string RodapeHtml { get; set; } = string.Empty;

    public Guid? AtualizadoPorUsuarioId { get; set; }
    public Usuario? AtualizadoPorUsuario { get; set; }

    public DateTime? AtualizadoEm { get; set; }
}
