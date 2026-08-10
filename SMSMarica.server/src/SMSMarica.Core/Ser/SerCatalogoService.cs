using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Ser.Dtos;
using SMSMarica.Data;
using SMSMarica.Data.Entities.Ser;

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
}

public sealed class SerCatalogoService(
    SmsMaricaDbContext db,
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

        return new SerCatalogoFormularioDto(
            Lista(listas, "ambulatorio_estadual"),
            Lista(listas, "classificacao_risco"),
            Lista(listas, "medico"),
            recursos,
            sincronizado,
            recursos.Count(r => !r.CamposLidos),
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
