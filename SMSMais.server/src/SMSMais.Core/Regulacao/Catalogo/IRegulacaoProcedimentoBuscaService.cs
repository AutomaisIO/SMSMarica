using SMSMais.Core.Regulacao.Catalogo.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Regulacao.Catalogo;

/// <summary>
/// Busca de procedimento por texto livre para a abertura de solicitação: junta a busca lexical
/// com a semântica e devolve, junto, a oferta interna (unidades de Maricá com escala no SISREG)
/// e a existência externa (SER / SER-AE / SERNIT).
/// </summary>
public interface IRegulacaoProcedimentoBuscaService
{
    Task<RegulacaoBuscaResultadoDto> BuscarAsync(
        string termo, TipoProcedimentoRegulacao? tipo, int limite, CancellationToken ct);

    Task<RegulacaoProcedimentoDetalheDto> ObterAsync(Guid procedimentoId, CancellationToken ct);
}
