namespace SMSMarica.Data.Entities;

public class Unidade
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public Endereco? Endereco { get; set; }
    public string? Telefone { get; set; }
    public Gps? Gps { get; set; }

    /// <summary>Destino fora de Maricá (TFD). Derivável da cidade; explícito para filtros.</summary>
    public bool Externa { get; set; }
    public string? CodigoIbgeCidade { get; set; }

    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }
}
