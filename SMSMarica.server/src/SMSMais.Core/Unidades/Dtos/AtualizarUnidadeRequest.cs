using SMSMais.Core.Common.Dtos;

namespace SMSMais.Core.Unidades.Dtos;

public sealed record AtualizarUnidadeRequest(
    string Nome,
    string? Cnes,
    EnderecoDto? Endereco,
    string? Telefone,
    double? Latitude,
    double? Longitude);
