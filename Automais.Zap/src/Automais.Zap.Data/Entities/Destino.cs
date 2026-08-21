namespace Automais.Zap.Data.Entities;

/// <summary>
/// Uma aplicação que recebe eventos — na prática, uma instância de município.
/// O relay não sabe nada sobre ela além de para onde entregar.
/// </summary>
public sealed class Destino
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Nome do cliente, só para a tela. Ex.: "Saúde Maricá".</summary>
    public required string Nome { get; set; }

    /// <summary>URL completa do webhook da aplicação, incluindo o caminho.</summary>
    public required string UrlWebhook { get; set; }

    /// <summary>
    /// Desligar aqui corta o recebimento de TODOS os números deste destino.
    /// É a alavanca de suspensão do canal — não precisa de cooperação da instância.
    /// </summary>
    public bool Ativo { get; set; } = true;

    public string? Observacao { get; set; }

    public DateTimeOffset CriadoEm { get; set; }
    public DateTimeOffset? AtualizadoEm { get; set; }

    public ICollection<Numero> Numeros { get; set; } = [];
}
