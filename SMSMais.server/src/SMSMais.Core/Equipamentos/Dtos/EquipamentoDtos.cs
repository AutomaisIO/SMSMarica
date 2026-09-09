using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Equipamentos.Dtos;

public sealed record EquipamentoDto(
    Guid Id,
    string Nome,
    Guid UnidadeId,
    string UnidadeNome,
    ModalidadeDicom ModalidadeDicom,
    string? IdentificadorDicom,
    int DescricaoMaxCaracteres,
    bool Ativo,
    DateTime CriadoEm);

public sealed record EquipamentoListItemDto(
    Guid Id,
    string Nome,
    Guid UnidadeId,
    string UnidadeNome,
    ModalidadeDicom ModalidadeDicom,
    string? IdentificadorDicom,
    int DescricaoMaxCaracteres,
    bool Ativo);

public sealed record CadastrarEquipamentoRequest(
    string Nome,
    Guid UnidadeId,
    ModalidadeDicom ModalidadeDicom,
    string? IdentificadorDicom,
    /// <summary>Quantos caracteres da descrição este aparelho aguenta na worklist. Omitido = 64,
    /// o teto do DICOM. Só baixar quando um console concreto falhar (ver o Fuji FDR-3000AWS).</summary>
    int DescricaoMaxCaracteres = Equipamento.DescricaoMaxPadrao);

public sealed record AtualizarEquipamentoRequest(
    string Nome,
    Guid UnidadeId,
    ModalidadeDicom ModalidadeDicom,
    string? IdentificadorDicom,
    bool Ativo,
    int DescricaoMaxCaracteres = Equipamento.DescricaoMaxPadrao);
