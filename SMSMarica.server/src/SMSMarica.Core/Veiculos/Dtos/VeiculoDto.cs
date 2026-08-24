using SMSMais.Data.Entities.Enums;

namespace SMSMarica.Core.Veiculos.Dtos;

public sealed record VeiculoDto(
    Guid Id,
    string Placa,
    string Modelo,
    string Fabricante,
    string Cor,
    TipoVeiculo Tipo,
    bool Ativo,
    DateTime CriadoEm,
    IReadOnlyList<FileiraDto> Fileiras);

public sealed record VeiculoListItemDto(
    Guid Id,
    string Placa,
    string Modelo,
    string Fabricante,
    string Cor,
    TipoVeiculo Tipo,
    bool Ativo);

public sealed record FileiraDto(
    Guid Id,
    int Ordem,
    int QuantidadeAssentos,
    IReadOnlyList<AssentoDto> Assentos);

public sealed record AssentoDto(
    Guid Id,
    int Numero,
    TipoAssento Tipo,
    bool Bloqueado);
