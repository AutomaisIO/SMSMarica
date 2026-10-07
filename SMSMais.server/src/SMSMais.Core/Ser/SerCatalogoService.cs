using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Ser.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities.Ser;

namespace SMSMais.Core.Ser;

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

    /// <summary>
    /// Campos dinâmicos da linha do espelho — pelo <b>id nosso</b>, que é o que a origem do
    /// catálogo canônico guarda. Não passa pelo número do combo (posição, muda quando a SES
    /// renumera — Regulacao.Catalogo.IdentidadePorNome).
    /// </summary>
    Task<IReadOnlyList<SerCampoDinamicoDto>> ObterCamposDoRecursoAsync(
        Guid recursoId, CancellationToken cancellationToken);

    /// <summary><see cref="BuscarCidsAsync"/> pelo id nosso da linha do espelho.</summary>
    Task<SerCidSugestoesDto?> BuscarCidsDoRecursoAsync(
        Guid recursoId, string termo, CancellationToken cancellationToken);
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

        var todos = await db.SerCatalogoRecursos
            .AsNoTracking()
            .Select(x => new
            {
                x.Tipo, x.AmbulatorioEstadual, x.Valor, x.Rotulo, x.CamposLidos, x.CidListaId, x.SincronizadoEm,
            })
            .ToListAsync(cancellationToken);

        // Só o que estava no combo na ÚLTIMA listagem de cada (tipo, ramo): recurso que a SES
        // retirou continua no espelho (identidade é o nome, a linha não é reaproveitada) com a data
        // do dia em que saiu — oferecê-lo, ou contá-lo, seria mostrar o que o SER não tem mais.
        var porCombo = todos
            .GroupBy(x => (x.Tipo, x.AmbulatorioEstadual))
            .Select(g =>
            {
                var ultima = g.Max(x => x.SincronizadoEm);
                return (Ultima: ultima, Linhas: g.Where(x => x.SincronizadoEm >= ultima.AddHours(-1)).ToList());
            })
            .ToList();
        var atuais = porCombo.SelectMany(c => c.Linhas).ToList();

        var recursos = atuais
            .OrderBy(x => x.Tipo).ThenBy(x => x.Rotulo)
            .Select(x => new SerCatalogoRecursoDto(
                x.Tipo, x.AmbulatorioEstadual, x.Valor, x.Rotulo, x.CamposLidos))
            .ToList();

        // A cópia do combo mais ATRASADO, não a mais nova: o catálogo só está tão atualizado
        // quanto o pedaço mais velho dele. É a mesma régua da cópia diária (CatalogosRegulacaoFrescor).
        DateTime? sincronizado = porCombo.Count == 0 ? null : porCombo.Min(c => c.Ultima);

        var cids = await db.SerCatalogoCids.CountAsync(cancellationToken);
        var semCid = atuais.Count(r => r.CidListaId == null);

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

        return listaId is null ? null : await BuscarNaListaAsync(listaId.Value, termo, cancellationToken);
    }

    public async Task<IReadOnlyList<SerCampoDinamicoDto>> ObterCamposDoRecursoAsync(
        Guid recursoId, CancellationToken cancellationToken)
    {
        var campos = await db.SerCatalogoCampos
            .AsNoTracking()
            .Where(c => c.RecursoId == recursoId)
            .OrderBy(c => c.Ordem)
            .ToListAsync(cancellationToken);

        return [.. campos.Select(c => new SerCampoDinamicoDto(
            c.Numero, c.Campo, c.Rotulo, c.Tipo, c.Obrigatorio, Opcoes(c.OpcoesJson)))];
    }

    public async Task<SerCidSugestoesDto?> BuscarCidsDoRecursoAsync(
        Guid recursoId, string termo, CancellationToken cancellationToken)
    {
        var listaId = await db.SerCatalogoRecursos
            .AsNoTracking()
            .Where(r => r.Id == recursoId)
            .Select(r => r.CidListaId)
            .FirstOrDefaultAsync(cancellationToken);

        return listaId is null ? null : await BuscarNaListaAsync(listaId.Value, termo, cancellationToken);
    }

    private async Task<SerCidSugestoesDto> BuscarNaListaAsync(
        Guid listaId, string termo, CancellationToken cancellationToken)
    {

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
