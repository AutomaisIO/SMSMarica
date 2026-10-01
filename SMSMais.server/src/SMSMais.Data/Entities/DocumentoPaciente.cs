using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities;

/// <summary>
/// Documento do acervo do paciente — o que fica <b>perene</b> no cadastro, em "Exames anexados",
/// independente de onde entrou (painel, solicitação, WhatsApp, app do paciente).
///
/// <para>Cada fluxo de origem guarda a <b>própria</b> cópia do arquivo (a caixinha da regulação
/// apaga o conteúdo quando o anexo é retirado; o acervo não pode sumir junto). O acervo deduplica
/// por <see cref="HashSha256"/> dentro do paciente: anexar o mesmo PDF em três solicitações deixa
/// um documento só no cadastro.</para>
///
/// <para>O binário mora no armazenamento de arquivos (Spaces); aqui só a chave.</para>
/// </summary>
public class DocumentoPaciente
{
    public Guid Id { get; set; }

    /// <summary>Paciente (id do hub FHIR — <c>fhir.patient</c>).</summary>
    public Guid PacienteId { get; set; }

    /// <summary>Nome do documento dado por quem anexou (ex.: "Resultado da biópsia").</summary>
    public string Titulo { get; set; } = string.Empty;

    public string? Descricao { get; set; }

    /// <summary>Nome original do arquivo (para o download).</summary>
    public string NomeArquivo { get; set; } = string.Empty;

    /// <summary>Content-Type: <c>application/pdf</c> ou imagem (<c>image/jpeg</c>, <c>image/png</c>…).</summary>
    public string MimeType { get; set; } = string.Empty;

    public long TamanhoBytes { get; set; }

    /// <summary>SHA-256 (hex minúsculo). Dedup por paciente.</summary>
    public string HashSha256 { get; set; } = string.Empty;

    public string ChaveArmazenamento { get; set; } = string.Empty;

    public OrigemDocumentoPaciente Origem { get; set; }

    /// <summary>De onde veio, em texto curto (ex.: <c>regulacao:{id}</c>, <c>whatsapp:{mensagemId}</c>).</summary>
    public string? OrigemReferencia { get; set; }

    public SituacaoDocumentoPaciente Situacao { get; set; } = SituacaoDocumentoPaciente.Aceito;

    public DateTime? AceitoEm { get; set; }
    public Guid? AceitoPor { get; set; }

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
