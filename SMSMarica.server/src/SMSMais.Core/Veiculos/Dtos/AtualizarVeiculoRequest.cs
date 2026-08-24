using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Veiculos.Dtos;

public sealed record AtualizarVeiculoRequest(
    string Placa,
    string Modelo,
    string Fabricante,
    string Cor,
    TipoVeiculo Tipo);
