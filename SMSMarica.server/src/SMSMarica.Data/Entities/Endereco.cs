namespace SMSMarica.Data.Entities;

/// <summary>
/// Endereço residencial estruturado. Persistido como owned entity
/// (colunas achatadas na tabela do dono — ex. <c>paciente</c>).
/// </summary>
public sealed class Endereco
{
    public string Cep { get; set; } = string.Empty;
    public string Logradouro { get; set; } = string.Empty;
    public string? Numero { get; set; }
    public string? Complemento { get; set; }
    public string Bairro { get; set; } = string.Empty;
    public string Cidade { get; set; } = string.Empty;
    public string Uf { get; set; } = string.Empty;
    public string? PontoReferencia { get; set; }
}
