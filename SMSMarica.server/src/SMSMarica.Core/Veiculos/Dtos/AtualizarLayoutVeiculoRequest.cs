using SMSMais.Data.Entities.Enums;

namespace SMSMarica.Core.Veiculos.Dtos;

public sealed record AtualizarLayoutVeiculoRequest(
    IReadOnlyList<AtualizarLayoutFileiraRequest> Fileiras);

public sealed record AtualizarLayoutFileiraRequest(
    int Ordem,
    IReadOnlyList<AtualizarLayoutAssentoRequest> Assentos);

public sealed record AtualizarLayoutAssentoRequest(
    int Numero,
    TipoAssento Tipo,
    bool Bloqueado = false);
