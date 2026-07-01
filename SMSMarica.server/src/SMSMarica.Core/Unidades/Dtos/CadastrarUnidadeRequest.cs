using SMSMarica.Core.Common.Dtos;

namespace SMSMarica.Core.Unidades.Dtos;

public sealed record CadastrarUnidadeRequest(
    string Nome,
    string? Cnes,
    EnderecoDto? Endereco,
    string? Telefone,
    double? Latitude,
    double? Longitude);
