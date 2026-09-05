using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Sernit.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities.Sernit;

namespace SMSMais.Core.Sernit;

public sealed record SernitCatalogoSyncResultadoDto(
    int Recursos, int Campos, int Listas, int Falhas, int DuracaoSegundos, int Cids = 0);

/// <summary>
/// Copia o catálogo do SERNIT para a nossa base — recursos, campos dinâmicos e listas do bloco
/// fixo. Espelho do <c>SerCatalogoSyncService</c>, <b>sem o ramo "ambulatório estadual"</b> (o
/// SERNIT não o tem): uma passada por tipo, não por (ramo × tipo). Varredura longa e retomável
/// (recurso já lido fica marcado). SOMENTE LEITURA no SERNIT.
/// </summary>
public interface ISernitCatalogoSyncService
{
    Task<SernitCatalogoSyncResultadoDto> SincronizarAsync(bool refazerTudo, CancellationToken cancellationToken);
}

public sealed class SernitCatalogoSyncService(
    SmsMaisDbContext db,
    ISernitNovaSolicitacaoService leitor,
    Regulacao.Catalogo.IRegulacaoCatalogoService catalogoRegulacao,
    ILogger<SernitCatalogoSyncService> logger) : ISernitCatalogoSyncService
{
    private static readonly (string Codigo, TipoRecursoSernit Tipo)[] Tipos =
    [
        ("CONSULTA", TipoRecursoSernit.Consulta),
        ("EXAME", TipoRecursoSernit.Exame),
    ];

    public async Task<SernitCatalogoSyncResultadoDto> SincronizarAsync(
        bool refazerTudo, CancellationToken cancellationToken)
    {
        var inicio = DateTime.UtcNow;
        var agora = inicio;
        int recursos = 0, campos = 0, falhas = 0;

        // ---- bloco fixo (sem "ambulatório estadual") ----
        var form = await leitor.ObterFormularioAsync(cancellationToken);
        var listas =
            await SalvarListaAsync("classificacao_risco", form.ClassificacoesRisco, agora, cancellationToken)
            + await SalvarListaAsync("medico", form.Medicos, agora, cancellationToken);

        logger.LogInformation("SERNIT/catálogo: {Qtd} itens das listas fixas.", listas);

        // ---- recursos e campos, por tipo ----
        foreach (var (codigo, tipo) in Tipos)
        {
            var doSernit = await leitor.ListarRecursosAsync(codigo, cancellationToken);
            recursos += await SalvarRecursosAsync(tipo, doSernit, agora, cancellationToken);

            var pendentes = await db.SernitCatalogoRecursos
                .Where(r => r.Tipo == tipo && (refazerTudo || !r.CamposLidos))
                .OrderBy(r => r.Valor)
                .ToListAsync(cancellationToken);

            logger.LogInformation(
                "SERNIT/catálogo: {Tipo} — {Total} recursos, {Pendentes} a ler campos.",
                tipo, doSernit.Count, pendentes.Count);

            foreach (var recurso in pendentes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var lidos = await leitor.ObterCamposDinamicosAsync(codigo, recurso.Valor, cancellationToken);
                    campos += await SalvarCamposAsync(recurso, lidos, cancellationToken);

                    recurso.CamposLidos = true;
                    recurso.SincronizadoEm = DateTime.UtcNow;
                    await db.SaveChangesAsync(cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    falhas++;
                    logger.LogWarning(ex, "SERNIT/catálogo: falhou ao ler campos de {Valor} ({Rotulo}).",
                        recurso.Valor, recurso.Rotulo);
                }
            }
        }

        // ---- listas de CID ----
        var cids = 0;
        try
        {
            cids = await SincronizarCidsAsync(refazerTudo, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            falhas++;
            logger.LogWarning(ex, "SERNIT/catálogo: a cópia das listas de CID falhou.");
        }

        var duracao = (int)(DateTime.UtcNow - inicio).TotalSeconds;
        logger.LogInformation(
            "SERNIT/catálogo: {Recursos} recursos, {Campos} campos, {Listas} itens de lista, "
            + "{Cids} CID, {Falhas} falhas em {Seg}s.",
            recursos, campos, listas, cids, falhas, duracao);


        // O catálogo canônico da Regulação (ADR-0052) se alimenta deste espelho. Roda depois,
        // e num try/catch que só loga: o sync de origem é o que importa aqui e não pode falhar
        // porque o canônico teve problema (o provedor de embeddings é externo e cai).
        try
        {
            await catalogoRegulacao.SincronizarAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "SERNIT/catálogo: sincronismo do catálogo canônico da regulação falhou.");
        }

        return new SernitCatalogoSyncResultadoDto(recursos, campos, listas, falhas, duracao, cids);
    }

    private async Task<int> SincronizarCidsAsync(bool refazerTudo, CancellationToken cancellationToken)
    {
        var semLista = await db.SernitCatalogoRecursos.CountAsync(r => r.CidListaId == null, cancellationToken);
        if (!refazerTudo && semLista == 0)
        {
            logger.LogInformation("SERNIT/cid: todos os recursos já têm lista de CID — nada a medir.");
            return 0;
        }

        // passada 1: assinatura de cada recurso
        var medidos = 0;
        await foreach (var a in leitor.MedirAssinaturasCidAsync(cancellationToken))
        {
            var tipo = string.Equals(a.Tipo, "EXAME", StringComparison.OrdinalIgnoreCase)
                ? TipoRecursoSernit.Exame
                : TipoRecursoSernit.Consulta;

            var recurso = await db.SernitCatalogoRecursos
                .FirstOrDefaultAsync(r => r.Tipo == tipo && r.Valor == a.Recurso, cancellationToken);
            if (recurso is null) continue;

            recurso.CidAssinatura = a.Assinatura;
            await db.SaveChangesAsync(cancellationToken);
            medidos++;
        }

        // passada 2: uma cópia por assinatura que ainda não tem lista
        var listas = await db.SernitCatalogoCidListas.ToDictionaryAsync(l => l.Assinatura, cancellationToken);
        var pendentes = await db.SernitCatalogoRecursos
            .Where(r => r.CidAssinatura != null && (refazerTudo || r.CidListaId == null))
            .ToListAsync(cancellationToken);

        var copiados = 0;
        foreach (var grupo in pendentes.GroupBy(r => r.CidAssinatura!))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!listas.TryGetValue(grupo.Key, out var lista))
            {
                var porta = grupo.First();
                var itens = await leitor.CopiarListaCidAsync(
                    porta.Tipo == TipoRecursoSernit.Exame ? "EXAME" : "CONSULTA", porta.Valor, cancellationToken);

                lista = await SalvarListaCidAsync(grupo.Key, itens, cancellationToken);
                listas[grupo.Key] = lista;
                copiados += itens.Count;
            }

            foreach (var r in grupo) r.CidListaId = lista.Id;
            await db.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation(
            "SERNIT/cid: {Medidos} recursos medidos, {Listas} lista(s), {Copiados} CID copiados.",
            medidos, listas.Count, copiados);
        return copiados;
    }

    private async Task<SernitCatalogoCidLista> SalvarListaCidAsync(
        string assinatura, IReadOnlyList<SernitCidDto> itens, CancellationToken cancellationToken)
    {
        var lista = await db.SernitCatalogoCidListas
            .FirstOrDefaultAsync(l => l.Assinatura == assinatura, cancellationToken);

        if (lista is null)
        {
            lista = new SernitCatalogoCidLista { Id = Guid.NewGuid(), Assinatura = assinatura };
            db.SernitCatalogoCidListas.Add(lista);
        }
        else
        {
            db.SernitCatalogoCids.RemoveRange(
                await db.SernitCatalogoCids.Where(c => c.ListaId == lista.Id).ToListAsync(cancellationToken));
        }

        lista.Quantidade = itens.Count;
        lista.SincronizadoEm = DateTime.UtcNow;

        foreach (var c in itens.DistinctBy(x => x.Codigo, StringComparer.Ordinal))
        {
            db.SernitCatalogoCids.Add(new SernitCatalogoCid
            {
                Id = Guid.NewGuid(),
                ListaId = lista.Id,
                Codigo = Truncar(c.Codigo, 10),
                Descricao = Truncar(c.Descricao, 400),
                Texto = Truncar(c.Texto, 420),
                Busca = Truncar(SernitCidBusca.De(c.Codigo, c.Descricao), 420),
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return lista;
    }

    private async Task<int> SalvarListaAsync(
        string lista, IReadOnlyList<SernitOpcaoDto> opcoes, DateTime agora, CancellationToken cancellationToken)
    {
        var existentes = await db.SernitCatalogoListas
            .Where(x => x.Lista == lista)
            .ToDictionaryAsync(x => x.Valor, cancellationToken);

        var ordem = 0;
        foreach (var o in opcoes.DistinctBy(x => x.Valor))
        {
            if (existentes.TryGetValue(o.Valor, out var atual))
            {
                atual.Rotulo = o.Rotulo;
                atual.Ordem = ordem++;
                atual.SincronizadoEm = agora;
                continue;
            }

            db.SernitCatalogoListas.Add(new SernitCatalogoLista
            {
                Id = Guid.NewGuid(),
                Lista = lista,
                Valor = o.Valor,
                Rotulo = o.Rotulo,
                Ordem = ordem++,
                SincronizadoEm = agora,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return opcoes.DistinctBy(x => x.Valor).Count();
    }

    private async Task<int> SalvarRecursosAsync(
        TipoRecursoSernit tipo, IReadOnlyList<SernitOpcaoDto> doSernit, DateTime agora,
        CancellationToken cancellationToken)
    {
        var existentes = await db.SernitCatalogoRecursos
            .Where(x => x.Tipo == tipo)
            .ToDictionaryAsync(x => x.Valor, cancellationToken);

        foreach (var o in doSernit.DistinctBy(x => x.Valor))
        {
            if (existentes.TryGetValue(o.Valor, out var atual))
            {
                atual.Rotulo = o.Rotulo;
                atual.SincronizadoEm = agora;
                continue;
            }

            db.SernitCatalogoRecursos.Add(new SernitCatalogoRecurso
            {
                Id = Guid.NewGuid(),
                Tipo = tipo,
                Valor = o.Valor,
                Rotulo = o.Rotulo,
                SincronizadoEm = agora,
                CamposLidos = false,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return doSernit.DistinctBy(x => x.Valor).Count();
    }

    private async Task<int> SalvarCamposAsync(
        SernitCatalogoRecurso recurso, IReadOnlyList<SernitCampoDinamicoDto> lidos, CancellationToken cancellationToken)
    {
        var atuais = await db.SernitCatalogoCampos
            .Where(c => c.RecursoId == recurso.Id)
            .ToListAsync(cancellationToken);
        db.SernitCatalogoCampos.RemoveRange(atuais);

        var ordem = 0;
        foreach (var c in lidos.DistinctBy(x => x.Numero))
        {
            db.SernitCatalogoCampos.Add(new SernitCatalogoCampo
            {
                Id = Guid.NewGuid(),
                RecursoId = recurso.Id,
                Numero = c.Numero,
                Campo = c.Campo,
                Rotulo = Truncar(c.Rotulo, 500),
                Tipo = c.Tipo,
                Obrigatorio = c.Obrigatorio,
                OpcoesJson = c.Opcoes is { Count: > 0 } ? JsonSerializer.Serialize(c.Opcoes) : null,
                Ordem = ordem++,
            });
        }

        return lidos.DistinctBy(x => x.Numero).Count();
    }

    private static string Truncar(string texto, int max) => texto.Length <= max ? texto : texto[..max];
}
