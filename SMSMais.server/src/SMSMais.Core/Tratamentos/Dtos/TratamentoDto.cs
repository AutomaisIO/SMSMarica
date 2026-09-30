using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Tratamentos.Dtos;

public sealed record TratamentoDto(
    Guid Id,
    Guid PacienteId,
    string PacienteNome,
    Guid UnidadeAtendimentoId,
    string UnidadeAtendimentoNome,
    string? UnidadeAtendimentoCidade,
    Guid? TipoTratamentoId,
    string? TipoTratamentoNome,
    /// <summary>Tempo médio do TIPO (o atendimento não tem tempo próprio).</summary>
    int? TempoMedioMinutos,
    string Descricao,
    string? Observacoes,
    AgendaDto Agenda,
    NecessidadesDto Necessidades,
    RegraAcompanhantesDto Acompanhantes,
    bool Ativo,
    DateTime CriadoEm,
    DateTime? EncerradoEm,
    IReadOnlyList<SessaoDto> Sessoes);

public sealed record TratamentoListItemDto(
    Guid Id,
    Guid PacienteId,
    string PacienteNome,
    Guid UnidadeAtendimentoId,
    string UnidadeAtendimentoNome,
    string? TipoTratamentoNome,
    string Descricao,
    int? TempoMedioMinutos,
    int DiasSemanaMascara,
    bool Continuo,
    DateOnly? ProximaSessao,
    int TotalSessoes,
    int SessoesRealizadas,
    bool Ativo);

public sealed record AgendaDto(
    DateOnly DataInicio,
    int DiasSemanaMascara,
    int? QuantidadeSessoes,
    bool Continuo,
    DateOnly? SessoesGeradasAte);

public sealed record NecessidadesDto(
    MobilidadeTransporte Mobilidade,
    bool DificuldadeVeiculoAlto,
    bool Isolamento,
    bool UsaOxigenio,
    bool NecessitaAjuda,
    string? AjudaDescricao);

public sealed record RegraAcompanhantesDto(
    int Quantidade,
    string? JustificativaSegundo,
    string? LiberadoPorNome,
    DateTime? LiberadoEm);

/// <summary>Prévia da agenda: as datas que seriam geradas e até quando (contínuo).</summary>
public sealed record PreviaAgendaDto(
    IReadOnlyList<DateOnly> Datas,
    DateOnly? GeradasAte);

/// <summary>Acompanhante escolhido para uma viagem.</summary>
public sealed record AcompanhanteDaSessaoDto(
    Guid Id,
    string Nome,
    ParentescoAcompanhante? Parentesco);

public sealed record SessaoDto(
    Guid Id,
    Guid TratamentoId,
    DateOnly DataPrevista,
    TimeOnly? HoraPrevistaBusca,
    TimeOnly? HoraPrevistaRetorno,
    StatusSessao Status,
    DateTime? RealizadaEm,
    /// <summary>Texto livre de antes da lista de acompanhantes — só histórico.</summary>
    string? NomeAcompanhante,
    string? ParentescoAcompanhante,
    IReadOnlyList<AcompanhanteDaSessaoDto> Acompanhantes,
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

public sealed record TipoTratamentoDto(Guid Id, string Nome, string Codigo, int? TempoMedioMinutos, bool Ativo);

/// <summary>Uma viagem do Transporte de Pacientes vista pelo próprio paciente (app do cidadão).</summary>
public sealed record ViagemTransporteDto(
    Guid SessaoId,
    DateOnly Data,
    string Destino,
    string? Cidade,
    string? TipoTratamento,
    StatusSessao Status,
    /// <summary>Horário de busca, quando a rota já definiu; nulo = "informado na véspera".</summary>
    TimeOnly? HoraBusca,
    IReadOnlyList<string> Acompanhantes,
    int LimiteAcompanhantes);
