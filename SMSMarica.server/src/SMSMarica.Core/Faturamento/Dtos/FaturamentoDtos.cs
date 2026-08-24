using SMSMais.Data.Entities.Enums;

namespace SMSMarica.Core.Faturamento.Dtos;

/// <summary>Dimensão de agrupamento dos relatórios de faturamento.</summary>
public enum DimensaoFaturamento
{
    Paciente = 1,
    Motorista = 2,
    Veiculo = 3,
    TipoTratamento = 4,
    Unidade = 5,
}

public sealed record RegistroFaturamentoDto(
    Guid Id,
    Guid SessaoId,
    Guid PacienteId,
    string PacienteNome,
    Guid? MotoristaId,
    Guid? VeiculoId,
    Guid? TipoTratamentoId,
    Guid UnidadeId,
    string UnidadeNome,
    int Competencia,
    DateOnly Data,
    decimal KmComPaciente,
    decimal Unidades,
    decimal ValorUnitario,
    decimal ValorTotal,
    string? CodigoSigtap,
    StatusFaturamento Status);

public sealed record ResumoFaturamentoItemDto(
    string ChaveId,
    string Descricao,
    int QtdRegistros,
    decimal TotalKm,
    decimal TotalUnidades,
    decimal TotalValor);

public sealed record ResumoFaturamentoDto(
    DimensaoFaturamento Dimensao,
    int? Competencia,
    DateOnly? De,
    DateOnly? Ate,
    IReadOnlyList<ResumoFaturamentoItemDto> Itens,
    decimal TotalGeralUnidades,
    decimal TotalGeralValor);

public sealed record TfdConfigFaturamentoDto(decimal ValorPor50Km, int KmPorUnidade, string? CodigoSigtap, bool Ativo);

public sealed record AtualizarTfdConfigFaturamentoRequest(decimal ValorPor50Km, int KmPorUnidade, string? CodigoSigtap, bool Ativo);
