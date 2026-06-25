namespace SMSMarica.Data.Entities.Enums;

/// <summary>
/// Ciclo de vida de um <see cref="DocumentoExame"/> anexado pela ponte QR → PWA.
/// O valor inteiro é estável (persistido) — não renumerar.
/// </summary>
public enum StatusDocumentoExame
{
    /// <summary>Recém-enviado pelo PWA; aguardando revisão/confirmação do médico.</summary>
    Pendente = 1,

    /// <summary>Revisado e confirmado pelo médico — vale como anexo da anamnese e entra no histórico.</summary>
    Salvo = 2,
}
