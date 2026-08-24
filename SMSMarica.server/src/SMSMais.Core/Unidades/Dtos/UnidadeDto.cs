using SMSMais.Core.Common.Dtos;

namespace SMSMais.Core.Unidades.Dtos;

public sealed record UnidadeDto(
    Guid Id,
    string Nome,
    string? Cnes,
    EnderecoDto? Endereco,
    string? Telefone,
    double? Latitude,
    double? Longitude,
    bool Ativo,
    DateTime CriadoEm);

public sealed record UnidadeListItemDto(
    Guid Id,
    string Nome,
    string? Cidade,
    string? Uf,
    bool Ativo);
