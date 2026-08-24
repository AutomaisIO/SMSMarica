using SMSMais.Data.Entities.Enums;

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
    // Visão da lista relativa à unidade de referência (a ativa da sessão), quando há UMA só:
    // false/padrão = EXECUTANTE (o que a unidade realiza — o que a recepção quer ver);
    // true = SOLICITANTE (o que a unidade pediu a outra). Sem unidade de referência única
    // (acesso global sem ativa, ou visão do conjunto) o recorte é ignorado — não há "solicitante
    // vs executante" bem-definido. Ver ticket #84.
    bool VisaoSolicitante = false,
    int Limite = 50,
    // Página 1-based da listagem (paginação offset). Tamanho da página = Limite.
    int Pagina = 1);

/// <summary>Recortes que o painel de início abre na listagem de solicitações.</summary>
public enum RecortePainel
{
    /// <summary>O paciente avisou que não vem e a equipe ainda não agiu (ADR-0034).</summary>
    Cancelados = 1,

    /// <summary>Sem resposta do paciente, com o exame próximo.</summary>
    Aguardando = 2,
}
