namespace SMSMais.Core.TiposTratamento.Dtos;

public sealed record TipoTratamentoDto(Guid Id, string Nome, string Codigo, int? TempoMedioMinutos, bool Ativo, DateTime CriadoEm);

public sealed record TipoTratamentoListItemDto(Guid Id, string Nome, string Codigo, int? TempoMedioMinutos, bool Ativo);

/// <summary><paramref name="TempoMedioMinutos"/>: quanto o paciente fica no tratamento, da chegada à
/// liberação (1 min a 24 h). Vale para todo atendimento deste tipo.</summary>
public sealed record CadastrarTipoTratamentoRequest(string Nome, string Codigo, int? TempoMedioMinutos);

public sealed record AtualizarTipoTratamentoRequest(string Nome, string Codigo, int? TempoMedioMinutos, bool Ativo);
