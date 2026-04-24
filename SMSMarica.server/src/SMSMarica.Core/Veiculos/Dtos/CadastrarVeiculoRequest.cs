using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Veiculos.Dtos;

/// <summary>
/// Cadastra um veículo com o layout completo de fileiras e assentos.
/// Cada fileira define seus próprios assentos (número + tipo), de modo
/// que o cliente monta o "mapa tipo cinema" e envia tudo num único POST.
/// </summary>
public sealed record CadastrarVeiculoRequest(
    string Placa,
    string Modelo,
    string Fabricante,
    string Cor,
    TipoVeiculo Tipo,
    IReadOnlyList<FileiraInput> Fileiras);

public sealed record FileiraInput(
    int Ordem,
    IReadOnlyList<AssentoInput> Assentos);

public sealed record AssentoInput(
    int Numero,
    TipoAssento Tipo);
