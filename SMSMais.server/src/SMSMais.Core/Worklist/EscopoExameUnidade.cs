using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SMSMais.Data;
using SMSMais.Data.Entities;

namespace SMSMais.Core.Worklist;

/// <summary>
/// Responde "esta unidade executa este exame, e como" — a régua que substituiu a flag global
/// <c>TipoExame.EnviarParaWorklist</c>.
///
/// <para>Existe como ponto único porque a mesma pergunta é feita em quatro lugares (autorização,
/// impedimento, troca de equipamento e o filtro do worker) e as respostas precisam concordar. A
/// versão global divergia por construção: um bit do município não conseguia valer <c>false</c>
/// para o Hospital Santo Antônio e <c>true</c> para o CDT ao mesmo tempo.</para>
/// </summary>
public interface IEscopoExameUnidade
{
    /// <summary>
    /// A configuração do par (tipo, unidade executante) do exame, ou <c>null</c> quando a unidade
    /// não tem o exame no escopo. Não lança: quem decide o que fazer com a ausência é o chamador.
    /// </summary>
    Task<TipoExameUnidade?> ObterAsync(ExameImagem exame, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mesma consulta a partir das chaves, para quem já as tem em mãos.
    /// </summary>
    Task<TipoExameUnidade?> ObterAsync(Guid tipoExameId, Guid unidadeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Garante que o par (tipo, unidade) existe — idempotente. Chamado pela importação: procedimento
    /// novo entra no escopo da unidade que o importou <b>automaticamente</b>, mas
    /// <b>desligado e sem equipamento</b>, porque ligar é decisão de quem conhece a operação da
    /// unidade e mandar item mal formado ao aparelho é pior do que não mandar.
    ///
    /// <para>Não sobrescreve nada: se o par já existe, devolve como está — inclusive quando alguém
    /// já o configurou. Também não faz <c>SaveChanges</c>; quem chama já está numa transação.</para>
    /// </summary>
    Task<(TipoExameUnidade Escopo, bool Criado)> GarantirAsync(
        Guid tipoExameId, Guid unidadeId, CancellationToken cancellationToken = default);
}

internal sealed class EscopoExameUnidade(SmsMaisDbContext db) : IEscopoExameUnidade
{
    /// <summary>
    /// O predicado que o worker usa dentro do <c>IQueryable</c> — vira um EXISTS no SQL. Fica aqui,
    /// e não escrito à mão no worker, para não haver duas definições de "está no escopo" capazes de
    /// divergir: o que a tela mostra e o que o worker envia têm de ser a mesma coisa.
    /// </summary>
    public static Expression<Func<ExameImagem, bool>> EnviaParaWorklist(SmsMaisDbContext db) =>
        e => e.TipoExameId != null
             && db.TiposExameUnidade.Any(a =>
                    a.TipoExameId == e.TipoExameId
                    && a.UnidadeId == e.Solicitacao!.UnidadeExecutanteId
                    && a.Ativo
                    && a.ExcluidoEm == null
                    && a.EnviarParaWorklist);

    public async Task<TipoExameUnidade?> ObterAsync(ExameImagem exame, CancellationToken cancellationToken = default)
    {
        if (exame.TipoExameId is not { } tipoExameId) return null;

        var unidadeId = exame.Solicitacao?.UnidadeExecutanteId
            ?? await db.Solicitacoes.AsNoTracking()
                .Where(s => s.Id == exame.SolicitacaoId)
                .Select(s => s.UnidadeExecutanteId)
                .FirstOrDefaultAsync(cancellationToken);

        return unidadeId == Guid.Empty
            ? null
            : await ObterAsync(tipoExameId, unidadeId, cancellationToken);
    }

    public async Task<TipoExameUnidade?> ObterAsync(
        Guid tipoExameId, Guid unidadeId, CancellationToken cancellationToken = default) =>
        await db.TiposExameUnidade.AsNoTracking()
            .Include(a => a.Equipamento)
            .FirstOrDefaultAsync(
                a => a.TipoExameId == tipoExameId
                     && a.UnidadeId == unidadeId
                     && a.Ativo
                     && a.ExcluidoEm == null,
                cancellationToken);

    public async Task<(TipoExameUnidade Escopo, bool Criado)> GarantirAsync(
        Guid tipoExameId, Guid unidadeId, CancellationToken cancellationToken = default)
    {
        // Sem AsNoTracking e sem filtrar por Ativo: um par reativado é o mesmo par, e criar outro
        // esbarraria no índice único.
        var existente = await db.TiposExameUnidade.FirstOrDefaultAsync(
            a => a.TipoExameId == tipoExameId && a.UnidadeId == unidadeId && a.ExcluidoEm == null,
            cancellationToken);

        if (existente is not null) return (existente, false);

        var novo = new TipoExameUnidade
        {
            Id = Guid.CreateVersion7(),
            TipoExameId = tipoExameId,
            UnidadeId = unidadeId,
            EnviarParaWorklist = false,
            EquipamentoId = null,
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
            // CriadoPor null de propósito: não foi pessoa nenhuma, foi a importação.
        };
        db.TiposExameUnidade.Add(novo);
        return (novo, true);
    }
}
