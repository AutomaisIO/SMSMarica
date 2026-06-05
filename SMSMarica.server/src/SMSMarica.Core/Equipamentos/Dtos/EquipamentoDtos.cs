using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Equipamentos.Dtos;

public sealed record EquipamentoDto(
    Guid Id,
    string Nome,
    Guid UnidadeId,
    string UnidadeNome,
    ModalidadeDicom ModalidadeDicom,
    string? IdentificadorDicom,
    bool Ativo,
    DateTime CriadoEm);

public sealed record EquipamentoListItemDto(
    Guid Id,
    string Nome,
    Guid UnidadeId,
    string UnidadeNome,
    ModalidadeDicom ModalidadeDicom,
    bool Ativo);

public sealed record CadastrarEquipamentoRequest(
    string Nome,
    Guid UnidadeId,
    ModalidadeDicom ModalidadeDicom,
    string? IdentificadorDicom);

public sealed record AtualizarEquipamentoRequest(
    string Nome,
    Guid UnidadeId,
    ModalidadeDicom ModalidadeDicom,
    string? IdentificadorDicom,
    bool Ativo);
