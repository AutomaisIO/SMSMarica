using SMSMais.Data.Entities.Enums;

namespace SMSMarica.Core.Tratamentos.Dtos;

public sealed record TratamentoDto(
    Guid Id,
    Guid PacienteId,
    string PacienteNome,
    Guid UnidadeId,
    string UnidadeNome,
    Guid? TipoTratamentoId,
    string? TipoTratamentoNome,
    string Descricao,
    string? CodigoSusLiberacao,
    string? Observacoes,
    TimeOnly? HoraPrevistaBusca,
    bool Ativo,
    DateTime CriadoEm,
    DateTime? EncerradoEm,
    PeriodicidadeDto? Periodicidade,
    IReadOnlyList<SessaoDto> Sessoes);

public sealed record TratamentoListItemDto(
    Guid Id,
    Guid PacienteId,
    string PacienteNome,
    Guid UnidadeId,
    string UnidadeNome,
    string? TipoTratamentoNome,
    string Descricao,
    DateOnly? ProximaSessao,
    int TotalSessoes,
    int SessoesRealizadas,
    bool Ativo);

public sealed record PeriodicidadeDto(
    Guid Id,
    TipoPeriodicidade Tipo,
    int? IntervaloDias,
    int? DiasSemanaMascara,
    DateOnly DataInicio,
    int QuantidadeSessoes);

public sealed record SessaoDto(
    Guid Id,
    Guid TratamentoId,
    DateOnly DataPrevista,
    TimeOnly? HoraPrevistaBusca,
    TimeOnly? HoraPrevistaRetorno,
    StatusSessao Status,
    DateTime? RealizadaEm,
    string? NomeAcompanhante,
    string? ParentescoAcompanhante,
    Guid? MotoristaIdaId,
    Guid? VeiculoIdaId,
    TimeOnly? HoraSaidaResidencia,
    TimeOnly? HoraChegadaUnidade,
    Guid? MotoristaVoltaId,
    Guid? VeiculoVoltaId,
    TimeOnly? HoraSaidaUnidade,
    TimeOnly? HoraChegadaResidencia,
    string? MotivoNaoRealizacao,
    string? Observacoes,
    Guid? AlocadaEmRotaId,
    DateOnly? AlocadaNaData,
    int? FileiraAssentoAlocado,
    int? NumeroAssentoAlocado);

public sealed record TipoTratamentoDto(Guid Id, string Nome, string Codigo, bool Ativo);
