namespace SMSMarica.Core.Especialidades.Dtos;

public sealed record EspecialidadeDto(
    Guid Id,
    string Nome,
    string? CodigoCbo,
    bool Ativo,
    DateTime CriadoEm);

public sealed record EspecialidadeListItemDto(
    Guid Id,
    string Nome,
    string? CodigoCbo,
    bool Ativo);

public sealed record CadastrarEspecialidadeRequest(
    string Nome,
    string? CodigoCbo);

public sealed record AtualizarEspecialidadeRequest(
    string Nome,
    string? CodigoCbo,
    bool Ativo);
