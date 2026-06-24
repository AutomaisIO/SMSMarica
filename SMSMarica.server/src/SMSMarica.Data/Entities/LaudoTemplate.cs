namespace SMSMarica.Data.Entities;

/// <summary>
/// Template editável de laudo. Usado como ponto de partida — o médico carrega
/// no editor e edita à vontade. Editar o template NÃO retroage em laudos já
/// criados (o conteúdo é copiado no momento da criação do laudo).
/// </summary>
public class LaudoTemplate
{
    public Guid Id { get; set; }

    public string Nome { get; set; } = string.Empty;

    /// <summary>String livre criada pelo usuário (ex.: "Mamografia", "Ultrassom").</summary>
    public string Categoria { get; set; } = string.Empty;

    public string? Descricao { get; set; }

    /// <summary>Documento ProseMirror/TipTap nativo (re-editável).</summary>
    public string ConteudoJson { get; set; } = "{}";

    /// <summary>HTML pré-renderizado (sanitizado) — seed para o editor e fallback.</summary>
    public string ConteudoHtml { get; set; } = string.Empty;

    /// <summary>
    /// Definição opcional do questionário estruturado (seções → itens, cada item
    /// com texto e contribuição BI-RADS). Quando preenchido, o template ganha
    /// checklist + cálculo automático; quando null, é template de texto livre.
    /// JSON livre (jsonb) — schema versionado em <c>versaoSchema</c>.
    /// </summary>
    public string? EstruturaJson { get; set; }

    public Guid CriadoPorUsuarioId { get; set; }
    public Usuario? CriadoPorUsuario { get; set; }

    public Guid? AtualizadoPorUsuarioId { get; set; }
    public Usuario? AtualizadoPorUsuario { get; set; }

    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }

    /// <summary>Soft-delete: false esconde das listagens de "Novo laudo" mas mantém laudos vinculados.</summary>
    public bool Ativo { get; set; } = true;
}
