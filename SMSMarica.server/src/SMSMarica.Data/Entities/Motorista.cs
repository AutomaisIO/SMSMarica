using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Data.Entities;

/// <summary>
/// Motorista de translado. Carrega identidade inline (Fatia 4 do refator
/// FHIR — motorista não é entidade clínica FHIR, então não há candidato
/// como <c>fhir.patient</c>/<c>fhir.practitioner</c> para acomodar essas
/// colunas). O vínculo de login fica em <c>Usuario.MotoristaId</c>.
/// </summary>
public class Motorista
{
    public Guid Id { get; set; }

    // Identidade inline (Fatia 4 — movida de Usuario).
    public string NomeCompleto { get; set; } = string.Empty;
    public string Cpf { get; set; } = string.Empty;
    public string? Rg { get; set; }
    public DateOnly? DataNascimento { get; set; }
    public Sexo? Sexo { get; set; }
    public string? Telefone { get; set; }
    public Endereco? Endereco { get; set; }
    public string? FotoBase64 { get; set; }

    /// <summary>Número da CNH (apenas dígitos).</summary>
    public string Cnh { get; set; } = string.Empty;

    // Auditoria
    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
    public DateTime? ExcluidoEm { get; set; }
    public Guid? ExcluidoPor { get; set; }
}
