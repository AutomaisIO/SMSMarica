namespace Automais.Zap.Data.Entities;

/// <summary>
/// A tabela de rota. O <see cref="PhoneNumberId"/> é a chave que a Meta manda em
/// <c>entry[].changes[].value.metadata.phone_number_id</c> — é por ele, e só por ele,
/// que o relay decide para onde entregar.
/// </summary>
public sealed class Numero
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Identificador do número na Meta. Único no relay inteiro.</summary>
    public required string PhoneNumberId { get; set; }

    public Guid DestinoId { get; set; }
    public Destino? Destino { get; set; }

    /// <summary>WABA a que o número pertence. Informativo — o roteamento não usa.</summary>
    public string? WabaId { get; set; }

    /// <summary>Número em formato humano, como a Meta exibe. Informativo.</summary>
    public string? DisplayPhoneNumber { get; set; }

    /// <summary>Rótulo livre para a tela. Ex.: "Central de Atendimento".</summary>
    public string? Rotulo { get; set; }

    public bool Ativo { get; set; } = true;

    public DateTimeOffset CriadoEm { get; set; }
    public DateTimeOffset? AtualizadoEm { get; set; }
}
