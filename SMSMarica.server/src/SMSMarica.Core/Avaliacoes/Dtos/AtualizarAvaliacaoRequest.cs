namespace SMSMarica.Core.Avaliacoes.Dtos;

public sealed record AtualizarAvaliacaoRequest(
    int Nota,
    string? Comentario);
