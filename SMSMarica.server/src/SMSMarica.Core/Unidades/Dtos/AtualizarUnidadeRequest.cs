using SMSMarica.Core.Common.Dtos;

namespace SMSMarica.Core.Unidades.Dtos;

public sealed record AtualizarUnidadeRequest(
    string Nome,
    string? Cnes,
    EnderecoDto? Endereco,
    string? Telefone,
    double? Latitude,
    double? Longitude);
