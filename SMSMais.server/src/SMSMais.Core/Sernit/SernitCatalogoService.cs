using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Sernit.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities.Sernit;

namespace SMSMais.Core.Sernit;

/// <summary>
/// Lê o catálogo do SERNIT <b>da nossa base</b> — nenhuma requisição ao SERNIT. Torna a nova
/// solicitação offline e instantânea; quem enche estas tabelas é <see cref="ISernitCatalogoSyncService"/>.
/// (Sem o ramo "ambulatório estadual" — o SERNIT não o tem.)
/// </summary>
public interface ISernitCatalogoService
{
    Task<SernitCatalogoFormularioDto> ObterFormularioAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<SernitCampoDinamicoDto>> ObterCamposAsync(
        TipoRecursoSernit tipo, string recurso, CancellationToken cancellationToken);

    /// <summary>Os CID daquele recurso que casam o termo, do espelho. <c>null</c> quando o recurso
    /// ainda não tem lista copiada (aí quem chama cai no autocomplete ao vivo).</summary>
    Task<SernitCidSugestoesDto?> BuscarCidsAsync(
        TipoRecursoSernit tipo, string recurso, string termo, CancellationToken cancellationToken);
}

public sealed class SernitCatalogoService(
    SmsMaisDbContext db,
    Background.ISernitCatalogoSyncFila fila) : ISernitCatalogoService
{
    private const int TetoDeSugestoes = 500;

    public async Task<SernitCatalogoFormularioDto> ObterFormularioAsync(CancellationToken cancellationToken)
    {
        var listas = await db.SernitCatalogoListas
            .AsNoTracking()
            .OrderBy(x => x.Lista).ThenBy(x => x.Ordem)
            .ToListAsync(cancellationToken);

        var recursos = await db.SernitCatalogoRecursos
            .AsNoTracking()
            .OrderBy(x => x.Tipo).ThenBy(x => x.Rotulo)
            .Select(x => new SernitCatalogoRecursoDto(x.Tipo, x.Valor, x.Rotulo, x.CamposLidos))
            .ToListAsync(cancellationToken);

        // A data mais ANTIGA: o catálogo só está tão atualizado quanto o item mais velho dele.
        DateTime? sincronizado = recursos.Count == 0
            ? null
            : await db.SernitCatalogoRecursos.MinAsync(x => (DateTime?)x.SincronizadoEm, cancellationToken);

        var cids = await db.SernitCatalogoCids.CountAsync(cancellationToken);
        var semCid = await db.SernitCatalogoRecursos.CountAsync(r => r.CidListaId == null, cancellationToken);

        return new SernitCatalogoFormularioDto(
            Lista(listas, "classificacao_risco"),
            Lista(listas, "medico"),
            recursos,
            sincronizado,
            recursos.Count(r => !r.CamposLidos),
            cids,
            semCid,
            fila.EmExecucao,
            fila.UltimoErro);
    }

    public async Task<IReadOnlyList<SernitCampoDinamicoDto>> ObterCamposAsync(
        TipoRecursoSernit tipo, string recurso, CancellationToken cancellationToken)
    {
        var campos = await db.SernitCatalogoCampos
            .AsNoTracking()
            .Where(c => c.Recurso!.Tipo == tipo && c.Recurso.Valor == recurso)
            .OrderBy(c => c.Ordem)
            .ToListAsync(cancellationToken);

        return [.. campos.Select(c => new SernitCampoDinamicoDto(
            c.Numero, c.Campo, c.Rotulo, c.Tipo, c.Obrigatorio, Opcoes(c.OpcoesJson)))];
    }

    public async Task<SernitCidSugestoesDto?> BuscarCidsAsync(
        TipoRecursoSernit tipo, string recurso, string termo, CancellationToken cancellationToken)
    {
        var listaId = await db.SernitCatalogoRecursos
            .AsNoTracking()
            .Where(r => r.Tipo == tipo && r.Valor == recurso)
            .Select(r => r.CidListaId)
            .FirstOrDefaultAsync(cancellationToken);

        if (listaId is null) return null;

        var busca = SernitCidBusca.Normalizar(termo);

        var itens = await db.SernitCatalogoCids
            .AsNoTracking()
            .Where(c => c.ListaId == listaId && c.Busca.Contains(busca))
            .OrderBy(c => c.Codigo)
            .Take(TetoDeSugestoes)
            .Select(c => new SernitCidDto(c.Codigo, c.Descricao, c.Texto))
            .ToListAsync(cancellationToken);

        return new SernitCidSugestoesDto(itens, itens.Count >= TetoDeSugestoes);
    }

    private static IReadOnlyList<SernitOpcaoDto> Lista(List<SernitCatalogoLista> todas, string nome) =>
        [.. todas.Where(x => x.Lista == nome).Select(x => new SernitOpcaoDto(x.Valor, x.Rotulo))];

    private static IReadOnlyList<SernitOpcaoDto>? Opcoes(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<List<SernitOpcaoDto>>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
