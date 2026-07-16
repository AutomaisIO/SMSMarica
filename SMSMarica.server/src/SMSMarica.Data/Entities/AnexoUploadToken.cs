namespace SMSMarica.Data.Entities;

/// <summary>
/// Token de upload de exame da ponte QR → PWA. Criado a partir de uma sessão
/// autenticada do médico (tela de Anamnese); codificado num QR code que o
/// cidadão lê no celular para abrir o PWA já autorizado, sem login. Escopo de
/// 1 solicitação de exame, TTL curto (configurável), revogável e multi-uso
/// dentro da validade (cada "Novo Documento" gera um <see cref="DocumentoExame"/>).
/// É a trilha de auditoria do canal anônimo de upload.
/// </summary>
public class AnexoUploadToken
{
    public Guid Id { get; set; }

    /// <summary>Segredo aleatório URL-safe (base64url, 32+ bytes). Único.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>Exame de imagem ao qual os uploads ficam vinculados (FK).</summary>
    public Guid ExameImagemId { get; set; }
    public ExameImagem? ExameImagem { get; set; }

    /// <summary>Aponta para fhir.patient (hub FHIR), denormalizado — sem FK/navegação local.</summary>
    public Guid PatientId { get; set; }

    /// <summary>Nome do paciente, denormalizado para exibir no PWA sem nova chamada ao hub.</summary>
    public string PacienteNome { get; set; } = string.Empty;

    /// <summary>Usuário (médico) que gerou o token.</summary>
    public Guid? CriadoPor { get; set; }

    public DateTime CriadoEm { get; set; }

    /// <summary>Instante de expiração (CriadoEm + TTL).</summary>
    public DateTime ExpiraEm { get; set; }

    /// <summary>Quando o token foi revogado manualmente (ex.: médico fechou o modal). Null = ativo.</summary>
    public DateTime? RevogadoEm { get; set; }

    /// <summary>Último upload feito com este token (atualizado a cada uso).</summary>
    public DateTime? UltimoUsoEm { get; set; }
}
