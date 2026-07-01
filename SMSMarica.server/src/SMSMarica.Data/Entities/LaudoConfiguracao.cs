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

    // ---- Regras para INICIAR o laudo (assinar segue regra própria, não-configurável) ----

    /// <summary>
    /// Permite iniciar um laudo SEM o exame estar associado a um pedido. Padrão FALSE
    /// (exige associação). <b>Não afeta a assinatura</b>: assinar sem associação é sempre
    /// proibido (sem paciente confiável).
    /// </summary>
    public bool PermitirLaudarSemAssociacao { get; set; }

    /// <summary>
    /// Permite iniciar um laudo SEM a anamnese da solicitação estar preenchida. Padrão
    /// FALSE (exige anamnese feita).
    /// </summary>
    public bool PermitirLaudarSemAnamnese { get; set; }

    /// <summary>
    /// Validade (em dias) do link público de download enviado ao paciente (ex.: WhatsApp).
    /// Após esse prazo — ou após o 1º download — o link expira. Padrão 7 dias.
    /// </summary>
    public int DownloadLinkValidadeDias { get; set; } = 7;

    public Guid? AtualizadoPorUsuarioId { get; set; }
    public Usuario? AtualizadoPorUsuario { get; set; }

    public DateTime? AtualizadoEm { get; set; }
}
