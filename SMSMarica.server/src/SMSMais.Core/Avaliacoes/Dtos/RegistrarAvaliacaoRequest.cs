namespace SMSMais.Core.Avaliacoes.Dtos;

public sealed record RegistrarAvaliacaoRequest(
    Guid SessaoId,
    int Nota,
    string? Comentario);
