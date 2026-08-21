namespace Automais.Zap.Data.Entities;

/// <summary>
/// A tabela de rota. O <see cref="PhoneNumberId"/> é a chave que a Meta manda em
/// <c>entry[].changes[].value.metadata.phone_number_id</c> — é por ele que o relay decide
/// para onde entregar.
///
/// Preenchido ao sincronizar o WABA, não digitado à mão.
/// </summary>
public sealed class Numero
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Identificador do número na Meta. Único no relay inteiro.</summary>
    public required string PhoneNumberId { get; set; }

    public Guid WabaId { get; set; }
    public Waba? Waba { get; set; }

    /// <summary>Número em formato humano, como a Meta exibe.</summary>
    public string? DisplayPhoneNumber { get; set; }

    /// <summary>Nome verificado na Meta, ou rótulo do operador.</summary>
    public string? Rotulo { get; set; }

    /// <summary>
    /// Exceção ao destino do WABA. Vazio = herda. Existe para o caso de um cliente apontar
    /// dois números para instâncias diferentes (produção e homologação, por exemplo) sem que
    /// isso obrigue a refazer o modelo.
    /// </summary>
    public string? UrlDestinoOverride { get; set; }

    public bool Ativo { get; set; } = true;

    public DateTimeOffset CriadoEm { get; set; }
    public DateTimeOffset? AtualizadoEm { get; set; }
}
