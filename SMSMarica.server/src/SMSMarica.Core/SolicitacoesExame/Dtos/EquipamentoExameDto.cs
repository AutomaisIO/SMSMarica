namespace SMSMarica.Core.SolicitacoesExame.Dtos;

/// <summary>
/// Equipamento elegível para executar um exame (unidade executante + modalidade do tipo).
/// Alimenta a seleção da estação na autorização da recepção.
/// </summary>
/// <param name="AeTitle">Identificador DICOM da estação — exibido para conferência com o aparelho.</param>
/// <param name="Selecionado">Já é a estação gravada neste exame.</param>
public sealed record EquipamentoExameDto(
    Guid Id,
    string Nome,
    string AeTitle,
    bool Selecionado);
