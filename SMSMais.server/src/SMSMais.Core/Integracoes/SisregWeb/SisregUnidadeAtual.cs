using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Data;
using SMSMais.Data.Entities;

namespace SMSMais.Core.Integracoes.SisregWeb;

/// <summary>
/// Resolve a unidade selecionada para os fluxos do SISREG que <b>não admitem</b> a visão
/// "todas as unidades".
///
/// <para><b>Por que a unidade é obrigatória aqui:</b> a credencial do SISREG é de um operador
/// que enxerga uma unidade só, e o mapeamento/varredura são sempre no contexto dessa unidade.
/// Com "todas" selecionado não há como decidir contra qual operador autenticar — em vez de
/// escolher uma por conta própria (e mapear a agenda errada), recusamos e pedimos a escolha.</para>
/// </summary>
public interface ISisregUnidadeAtual
{
    /// <summary>Unidade selecionada no header. Lança <see cref="ValidacaoException"/> se não houver UMA.</summary>
    Task<Unidade> ObterObrigatoriaAsync(CancellationToken cancellationToken = default);
}

public sealed class SisregUnidadeAtual(
    SmsMaisDbContext db,
    IUsuarioAtualAccessor usuarioAtual) : ISisregUnidadeAtual
{
    public const string CodigoUnidadeObrigatoria = "sisreg.unidade_obrigatoria";

    public async Task<Unidade> ObterObrigatoriaAsync(CancellationToken cancellationToken = default)
    {
        var unidadeId = usuarioAtual.UnidadeAtivaId
            ?? throw new ValidacaoException(
                CodigoUnidadeObrigatoria,
                "Selecione UMA unidade no topo da tela. Os fluxos do SISREG (credencial e "
                + "mapeamento) trabalham sempre no contexto de uma única unidade.");

        return await db.Unidades
                   .AsNoTracking()
                   .FirstOrDefaultAsync(u => u.Id == unidadeId, cancellationToken)
               ?? throw new NaoEncontradoException("Unidade", unidadeId);
    }
}
