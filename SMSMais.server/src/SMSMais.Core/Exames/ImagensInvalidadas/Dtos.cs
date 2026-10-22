namespace SMSMais.Core.Exames.ImagensInvalidadas;

/// <summary>Uma imagem retirada do que vai ao paciente (invalidação vigente).</summary>
/// <param name="PodeRestaurar">
/// O usuário que pergunta pode devolver a imagem: quem a retirou ou quem tem acesso global.
/// </param>
public sealed record ImagemInvalidadaDto(
    string SopInstanceUID,
    string? Motivo,
    DateTime InvalidadaEm,
    Guid InvalidadaPorId,
    string InvalidadaPorNome,
    bool PodeRestaurar);

/// <summary>O que o visualizador precisa do estudo: o que está fora e se o usuário pode tirar mais.</summary>
/// <param name="PodeInvalidar">Quem lauda (Laudos/Edição) ou quem tem acesso global.</param>
public sealed record ImagensInvalidadasDoEstudoDto(
    bool PodeInvalidar,
    IReadOnlyList<ImagemInvalidadaDto> Imagens);

/// <summary>Corpo do "retirar imagem". Motivo é opcional.</summary>
public sealed record InvalidarImagemRequest(string? Motivo);
