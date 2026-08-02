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
    // Busca livre: nome, CPF, CNS (via hub FHIR) ou nº do pedido/accession/código.
    string? Busca = null,
    // Recorte do PAINEL DE INÍCIO — o "ver todos" de uma raia (ADR-0033 §4). Existe para que o
    // link do painel caia na tela que já existe em vez de criar listagem nova, e é filtro de
    // SERVIDOR porque filtrar a página no cliente devolveria um subconjunto arbitrário.
    RecortePainel? Painel = null,
    int Limite = 50);

/// <summary>Recortes que o painel de início abre na listagem de solicitações.</summary>
public enum RecortePainel
{
    /// <summary>O paciente avisou que não vem e a equipe ainda não agiu (ADR-0034).</summary>
    Cancelados = 1,

    /// <summary>Sem resposta do paciente, com o exame próximo.</summary>
    Aguardando = 2,
}
