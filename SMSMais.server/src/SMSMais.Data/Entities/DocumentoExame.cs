using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities;

/// <summary>
/// Documento (PDF) anexado a uma solicitação de exame pela ponte QR → PWA
/// (https://arquivos.smsmarica.online). O cidadão/atendente digitaliza o papel
/// no celular; o PWA monta um único PDF e o envia via token de upload anônimo.
/// O binário NÃO mora no banco — fica no armazenamento de arquivos
/// (<c>IArmazenamentoArquivos</c>); aqui guardamos só os metadados +
/// <see cref="ChaveArmazenamento"/>. Nasce <see cref="StatusDocumentoExame.Pendente"/>
/// e vira <see cref="StatusDocumentoExame.Salvo"/> quando o médico confirma.
/// Auditoria conforme ADR-0006 (<see cref="CriadoPor"/> é NULL no upload anônimo).
/// </summary>
public class DocumentoExame
{
    public Guid Id { get; set; }

    /// <summary>Vínculo com o exame de imagem (FK smsmarica.exame_imagem).</summary>
    public Guid ExameImagemId { get; set; }
    public ExameImagem? ExameImagem { get; set; }

    /// <summary>Token de upload que originou o documento (auditoria/proveniência). Null se criado por outro fluxo.</summary>
    public Guid? AnexoUploadTokenId { get; set; }
    public AnexoUploadToken? AnexoUploadToken { get; set; }

    /// <summary>Nome dado pelo cidadão/médico (ex.: "Resultado da biópsia").</summary>
    public string Nome { get; set; } = string.Empty;

    /// <summary>Descrição livre opcional.</summary>
    public string? Descricao { get; set; }

    /// <summary>Content-Type do binário (hoje sempre <c>application/pdf</c>).</summary>
    public string MimeType { get; set; } = "application/pdf";

    public long TamanhoBytes { get; set; }

    /// <summary>SHA-256 do conteúdo (hex). Permite deduplicar uploads idênticos por solicitação.</summary>
    public string HashSha256 { get; set; } = string.Empty;

    /// <summary>Chave no armazenamento de arquivos (ex.: <c>exames/2026/06/{guid}.pdf</c>).</summary>
    public string ChaveArmazenamento { get; set; } = string.Empty;

    public StatusDocumentoExame Status { get; set; } = StatusDocumentoExame.Pendente;

    /// <summary>Origem do documento (ex.: <c>pwa-scanner</c>). Null quando desconhecida.</summary>
    public string? Origem { get; set; }

    /// <summary>Número de páginas do PDF (informado pelo PWA quando disponível).</summary>
    public int? Paginas { get; set; }

    // ---- Auditoria ADR-0006 ----
    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
    public DateTime? ExcluidoEm { get; set; }
    public Guid? ExcluidoPor { get; set; }

    /// <summary>Concorrência otimista (PG xmin).</summary>
    public uint RowVersion { get; set; }
}
