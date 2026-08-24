using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Ser.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities.Ser;

namespace SMSMarica.Core.Ser;

/// <summary>
/// Lê o catálogo do SER <b>da nossa base</b> — nenhuma requisição ao SER.
///
/// <para>É o que torna a tela de nova solicitação offline e instantânea: o formulário é montado
/// daqui, e o SER só é procurado quando o pedido for de fato enviado. Quem enche estas tabelas é
/// <see cref="ISerCatalogoSyncService"/>, sob demanda.</para>
/// </summary>
public interface ISerCatalogoService
{
    Task<SerCatalogoFormularioDto> ObterFormularioAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<SerCampoDinamicoDto>> ObterCamposAsync(
        TipoRecursoSer tipo, string recurso, bool ambulatorioEstadual,
        CancellationToken cancellationToken);

    /// <summary>
    /// Os CID daquele recurso que casam o termo, do espelho — sem tocar no SER.
    ///
    /// <para>Devolve <c>null</c> quando o recurso ainda não tem lista copiada; aí quem chama cai
    /// no autocomplete ao vivo. É a diferença entre "não achei CID com esse termo" e "ainda não
    /// sei quais CID este recurso aceita", e confundir as duas ofereceria uma lista vazia com
    /// cara de resposta.</para>
    /// </summary>
    Task<SerCidSugestoesDto?> BuscarCidsAsync(
        TipoRecursoSer tipo, string recurso, bool ambulatorioEstadual, string termo,
        CancellationToken cancellationToken);
}

public sealed class SerCatalogoService(
    SmsMaisDbContext db,
    Background.ISerCatalogoSyncFila fila) : ISerCatalogoService
{
    public async Task<SerCatalogoFormularioDto> ObterFormularioAsync(
        CancellationToken cancellationToken)
    {
        var listas = await db.SerCatalogoListas
            .AsNoTracking()
            .OrderBy(x => x.Lista).ThenBy(x => x.Ordem)
            .ToListAsync(cancellationToken);

        var recursos = await db.SerCatalogoRecursos
            .AsNoTracking()
            .OrderBy(x => x.Tipo).ThenBy(x => x.Rotulo)
            .Select(x => new SerCatalogoRecursoDto(
                x.Tipo, x.AmbulatorioEstadual, x.Valor, x.Rotulo, x.CamposLidos))
            .ToListAsync(cancellationToken);

        // A data mais ANTIGA, não a mais nova: o catálogo só está tão atualizado quanto o item
        // mais velho dele. A média ou o máximo esconderiam um pedaço parado há meses.
        DateTime? sincronizado = recursos.Count == 0
            ? null
            : await db.SerCatalogoRecursos.MinAsync(x => (DateTime?)x.SincronizadoEm, cancellationToken);

        var cids = await db.SerCatalogoCids.CountAsync(cancellationToken);
        var semCid = await db.SerCatalogoRecursos
            .CountAsync(r => r.CidListaId == null, cancellationToken);

        return new SerCatalogoFormularioDto(
            Lista(listas, "ambulatorio_estadual"),
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

    public async Task<IReadOnlyList<SerCampoDinamicoDto>> ObterCamposAsync(
        TipoRecursoSer tipo, string recurso, bool ambulatorioEstadual,
        CancellationToken cancellationToken)
    {
        // O RAMO entra no filtro: o mesmo recurso pede formulários diferentes conforme a resposta
        // a "É ambulatório estadual?" (o 1000 pede 9 campos no "Não" e 3 no "Sim"). Sem ele, a
        // tela mostraria o formulário do outro ramo e o pedido voltaria recusado.
        var campos = await db.SerCatalogoCampos
            .AsNoTracking()
            .Where(c => c.Recurso!.Tipo == tipo
                        && c.Recurso.AmbulatorioEstadual == ambulatorioEstadual
                        && c.Recurso.Valor == recurso)
            .OrderBy(c => c.Ordem)
            .ToListAsync(cancellationToken);

        return [.. campos.Select(c => new SerCampoDinamicoDto(
            c.Numero, c.Campo, c.Rotulo, c.Tipo, c.Obrigatorio, Opcoes(c.OpcoesJson)))];
    }

    /// <summary>Mesmo teto do SER (500 por busca), para a tela se comportar igual dos dois
    /// lados — inclusive no aviso de lista cortada.</summary>
    private const int TetoDeSugestoes = 500;

    public async Task<SerCidSugestoesDto?> BuscarCidsAsync(
        TipoRecursoSer tipo, string recurso, bool ambulatorioEstadual, string termo,
        CancellationToken cancellationToken)
    {
        var listaId = await db.SerCatalogoRecursos
            .AsNoTracking()
            .Where(r => r.Tipo == tipo
                        && r.AmbulatorioEstadual == ambulatorioEstadual
                        && r.Valor == recurso)
            .Select(r => r.CidListaId)
            .FirstOrDefaultAsync(cancellationToken);

        if (listaId is null) return null;

        // A MESMA régua da cópia (SerCidBusca): normalizar de um jeito aqui e de outro lá faria a
        // tela não achar nada, sem erro nenhum.
        var busca = SerCidBusca.Normalizar(termo);

        var itens = await db.SerCatalogoCids
            .AsNoTracking()
            .Where(c => c.ListaId == listaId && c.Busca.Contains(busca))
            .OrderBy(c => c.Codigo)
            .Take(TetoDeSugestoes)
            .Select(c => new SerCidDto(c.Codigo, c.Descricao, c.Texto))
            .ToListAsync(cancellationToken);

        return new SerCidSugestoesDto(itens, itens.Count >= TetoDeSugestoes);
    }

    private static IReadOnlyList<SerOpcaoDto> Lista(List<SerCatalogoLista> todas, string nome) =>
        [.. todas.Where(x => x.Lista == nome).Select(x => new SerOpcaoDto(x.Valor, x.Rotulo))];

    private static IReadOnlyList<SerOpcaoDto>? Opcoes(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<List<SerOpcaoDto>>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
