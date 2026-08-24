namespace SMSMais.Core.TiposTratamento.Dtos;

public sealed record TipoTratamentoDto(Guid Id, string Nome, string Codigo, bool Ativo, DateTime CriadoEm);

public sealed record TipoTratamentoListItemDto(Guid Id, string Nome, string Codigo, bool Ativo);

public sealed record CadastrarTipoTratamentoRequest(string Nome, string Codigo);

public sealed record AtualizarTipoTratamentoRequest(string Nome, string Codigo, bool Ativo);
