using SMSMarica.Core.Common.Dtos;

namespace SMSMarica.Core.Unidades.Dtos;

public sealed record AtualizarUnidadeRequest(
    string Nome,
    EnderecoDto? Endereco,
    string? Telefone,
    double? Latitude,
    double? Longitude);
