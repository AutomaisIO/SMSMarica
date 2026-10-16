namespace SMSMais.Data.Entities;

/// <summary>
/// Foto de referência do rosto do motorista (ADR-0073), separada da foto de perfil. Uma por
/// motorista: os tablets baixam e geram o vetor de identidade no próprio aparelho — o servidor não
/// compara rostos. Apagar a linha obriga o motorista a refazer o primeiro acesso.
/// </summary>
public class MotoristaReferenciaFacial
{
    public Guid Id { get; set; }
    public Guid MotoristaId { get; set; }

    /// <summary>JPEG em base64, tirado no tablet depois da prova de vida.</summary>
    public string FotoBase64 { get; set; } = string.Empty;

    /// <summary>SHA-256 (hex) dos bytes da foto: o tablet só baixa de novo quando muda.</summary>
    public string FotoHash { get; set; } = string.Empty;

    /// <summary>Tablet e veículo do primeiro acesso.</summary>
    public Guid? DispositivoId { get; set; }
    public Guid? VeiculoId { get; set; }

    /// <summary>Termo de ciência aceito no cadastro (versão + SHA-256 do texto).</summary>
    public string TermoVersao { get; set; } = string.Empty;
    public string TermoHash { get; set; } = string.Empty;

    public DateTime CadastradaEm { get; set; }
}
