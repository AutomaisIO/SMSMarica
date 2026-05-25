using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.SolicitacoesExame.Dtos;

public sealed record FiltroSolicitacoesDto(
    StatusSolicitacaoExame? Status = null,
    Guid? PacienteId = null,
    Guid? UnidadeId = null,
    Guid? TipoExameId = null,
    DateOnly? DataInicial = null,
    DateOnly? DataFinal = null,
    string? AccessionNumber = null,
    int Limite = 50);
