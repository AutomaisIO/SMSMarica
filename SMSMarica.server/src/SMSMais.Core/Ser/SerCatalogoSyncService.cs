using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Data;
using SMSMais.Data.Entities.Ser;

namespace SMSMais.Core.Ser;

/// <summary>Resultado de uma sincronização do catálogo.</summary>
public sealed record SerCatalogoSyncResultadoDto(
    int Recursos, int Campos, int Listas, int Falhas, int DuracaoSegundos,
    /// <summary>Quantos CID foram copiados nesta rodada (somando as listas novas).</summary>
    int Cids = 0);

/// <summary>
/// Copia o catálogo do SER para a nossa base — recursos, campos dinâmicos e as listas do bloco
/// fixo.
///
/// <para><b>Por que existe:</b> a tela de nova solicitação lia tudo ao vivo, o que custava três
/// idas ao SER por preenchimento e deixava a tela inútil quando o SER estava fora. Com o catálogo
/// aqui, o formulário é montado <b>offline</b>; o SER só é procurado na hora de enviar.</para>
///
/// <para><b>É uma varredura longa</b> — uma ida ao SER por recurso, 203 na medição de 08/08/2026,
/// algo perto de 15 minutos. Por isso ela é retomável: cada recurso é gravado assim que lido e
/// marcado com <see cref="SerCatalogoRecurso.CamposLidos"/>. Se cair no meio, a próxima rodada
/// continua de onde parou em vez de recomeçar.</para>
///
/// <para><b>Somente leitura no SER.</b> Só abre a aba Editar e troca combos.</para>
/// </summary>
public interface ISerCatalogoSyncService
{
    Task<SerCatalogoSyncResultadoDto> SincronizarAsync(
        bool refazerTudo, CancellationToken cancellationToken);
}

public sealed class SerCatalogoSyncService(
    SmsMaisDbContext db,
    ISerNovaSolicitacaoService leitor,
    ILogger<SerCatalogoSyncService> logger) : ISerCatalogoSyncService
{
    private static readonly (string Codigo, TipoRecursoSer Tipo)[] Tipos =
    [
        ("CONSULTA", TipoRecursoSer.Consulta),
        ("EXAME", TipoRecursoSer.Exame),
    ];

    public async Task<SerCatalogoSyncResultadoDto> SincronizarAsync(
        bool refazerTudo, CancellationToken cancellationToken)
    {
        var inicio = DateTime.UtcNow;
        var agora = inicio;
        int recursos = 0, campos = 0, falhas = 0;

        // ---- bloco fixo (listas pequenas, uma ida só) ----
        var form = await leitor.ObterFormularioAsync(cancellationToken);
        var listas =
            await SalvarListaAsync("ambulatorio_estadual", form.AmbulatorioEstadual, agora, cancellationToken)
            + await SalvarListaAsync("classificacao_risco", form.ClassificacoesRisco, agora, cancellationToken)
            + await SalvarListaAsync("medico", form.Medicos, agora, cancellationToken);

        logger.LogInformation("SER/catálogo: {Qtd} itens das listas fixas.", listas);

        // ---- recursos e seus campos, RAMO A RAMO ----
        // O combo "É AMBULATÓRIO ESTADUAL?" não é um campo a mais: ele troca o catálogo inteiro.
        // Copiar só o default deixou 31 consultas (urologia, pneumologia, reumatologia…) fora da
        // base — inexistentes para quem fosse pedir — e gravou, para os recursos que existem nos
        // dois ramos, o formulário de um ramo só.
        foreach (var ramo in (bool[])[false, true])
        foreach (var (codigo, tipo) in Tipos)
        {
            var doSer = await leitor.ListarRecursosAsync(codigo, ramo, cancellationToken);
            recursos += await SalvarRecursosAsync(tipo, ramo, doSer, agora, cancellationToken);

            // Retomada: só pede ao SER o que ainda não tem campo lido. Refazer tudo é escolha
            // explícita (o catálogo muda pouco, e cada recurso custa uma requisição).
            var pendentes = await db.SerCatalogoRecursos
                .Where(r => r.Tipo == tipo && r.AmbulatorioEstadual == ramo
                            && (refazerTudo || !r.CamposLidos))
                .OrderBy(r => r.Valor)
                .ToListAsync(cancellationToken);

            logger.LogInformation(
                "SER/catálogo: {Tipo} ambulatório estadual={Ramo} — {Total} recursos, "
                + "{Pendentes} a ler campos.",
                tipo, ramo ? "Sim" : "Não", doSer.Count, pendentes.Count);

            foreach (var recurso in pendentes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var lidos = await leitor.ObterCamposDinamicosAsync(
                        codigo, recurso.Valor, ramo, cancellationToken);
                    campos += await SalvarCamposAsync(recurso, lidos, cancellationToken);

                    recurso.CamposLidos = true;
                    recurso.SincronizadoEm = DateTime.UtcNow;
                    // Grava recurso a recurso: uma queda no meio de 200 leituras não pode jogar
                    // fora o que já foi lido.
                    await db.SaveChangesAsync(cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    falhas++;
                    logger.LogWarning(
                        ex, "SER/catálogo: falhou ao ler campos de {Valor} ({Rotulo}).",
                        recurso.Valor, recurso.Rotulo);
                }
            }
        }

        // ---- listas de CID da Hipótese ----
        var cids = 0;
        try
        {
            cids = await SincronizarCidsAsync(refazerTudo, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            falhas++;
            // Não derruba a rodada: recursos e campos já copiados valem por si, e a tela cai no
            // autocomplete ao vivo enquanto não houver lista.
            logger.LogWarning(ex, "SER/catálogo: a cópia das listas de CID falhou.");
        }

        var duracao = (int)(DateTime.UtcNow - inicio).TotalSeconds;
        logger.LogInformation(
            "SER/catálogo: {Recursos} recursos, {Campos} campos, {Listas} itens de lista, "
            + "{Cids} CID, {Falhas} falhas em {Seg}s.",
            recursos, campos, listas, cids, falhas, duracao);

        return new SerCatalogoSyncResultadoDto(recursos, campos, listas, falhas, duracao, cids);
    }

    // ------------------------------------------------------------------ listas de CID

    /// <summary>
    /// Copia as listas de CID que a Hipótese aceita, em DUAS passadas.
    ///
    /// <para><b>Por que duas:</b> medir a assinatura dos 422 recursos é uma conversa Seam só
    /// (~3 min); copiar uma lista inteira reabre a aba e varre 260 prefixos. Intercalar as duas
    /// coisas destruiria o estado da conversa da medição, e as assinaturas seguintes sairiam do
    /// recurso errado — em silêncio, como sempre neste sistema. Então mede-se tudo primeiro,
    /// gravando a assinatura em cada recurso, e só depois se copia uma lista por assinatura
    /// nova.</para>
    ///
    /// <para>Medido em 20/08/2026: os 422 recursos produzem <b>duas</b> listas — 14.226 CID (o
    /// CID-10 inteiro) para 390 deles e 136 para os 32 oncológicos. Por isso a segunda passada
    /// custa duas varreduras, não 422.</para>
    /// </summary>
    private async Task<int> SincronizarCidsAsync(bool refazerTudo, CancellationToken cancellationToken)
    {
        var semLista = await db.SerCatalogoRecursos
            .CountAsync(r => r.CidListaId == null, cancellationToken);

        if (!refazerTudo && semLista == 0)
        {
            logger.LogInformation(
                "SER/cid: todos os recursos já têm lista de CID — nada a medir.");
            return 0;
        }

        // ---- passada 1: assinatura de cada recurso ----
        var medidos = 0;
        await foreach (var a in leitor.MedirAssinaturasCidAsync(cancellationToken))
        {
            var tipo = string.Equals(a.Tipo, "EXAME", StringComparison.OrdinalIgnoreCase)
                ? TipoRecursoSer.Exame
                : TipoRecursoSer.Consulta;

            var recurso = await db.SerCatalogoRecursos.FirstOrDefaultAsync(
                r => r.Tipo == tipo
                     && r.AmbulatorioEstadual == a.AmbulatorioEstadual
                     && r.Valor == a.Recurso,
                cancellationToken);

            // Recurso que o SER lista e a nossa cópia ainda não tem: a fase de recursos roda
            // antes, então isso só acontece se ele apareceu no meio da rodada. Fica para a
            // próxima, sem derrubar nada.
            if (recurso is null) continue;

            recurso.CidAssinatura = a.Assinatura;
            await db.SaveChangesAsync(cancellationToken);
            medidos++;
        }

        // ---- passada 2: uma cópia por assinatura que ainda não tem lista ----
        var listas = await db.SerCatalogoCidListas
            .ToDictionaryAsync(l => l.Assinatura, cancellationToken);

        var pendentes = await db.SerCatalogoRecursos
            .Where(r => r.CidAssinatura != null && (refazerTudo || r.CidListaId == null))
            .ToListAsync(cancellationToken);

        var copiados = 0;
        foreach (var grupo in pendentes.GroupBy(r => r.CidAssinatura!))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!listas.TryGetValue(grupo.Key, out var lista))
            {
                // Qualquer recurso do grupo serve de porta de entrada: por definição todos
                // devolvem a mesma lista.
                var porta = grupo.First();
                var itens = await leitor.CopiarListaCidAsync(
                    porta.Tipo == TipoRecursoSer.Exame ? "EXAME" : "CONSULTA",
                    porta.Valor, porta.AmbulatorioEstadual, cancellationToken);

                lista = await SalvarListaCidAsync(grupo.Key, itens, cancellationToken);
                listas[grupo.Key] = lista;
                copiados += itens.Count;
            }

            foreach (var r in grupo) r.CidListaId = lista.Id;
            await db.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation(
            "SER/cid: {Medidos} recursos medidos, {Listas} lista(s) distinta(s), "
            + "{Copiados} CID copiados.", medidos, listas.Count, copiados);

        return copiados;
    }

    private async Task<SerCatalogoCidLista> SalvarListaCidAsync(
        string assinatura, IReadOnlyList<Dtos.SerCidDto> itens, CancellationToken cancellationToken)
    {
        var lista = await db.SerCatalogoCidListas
            .FirstOrDefaultAsync(l => l.Assinatura == assinatura, cancellationToken);

        if (lista is null)
        {
            lista = new SerCatalogoCidLista { Id = Guid.NewGuid(), Assinatura = assinatura };
            db.SerCatalogoCidListas.Add(lista);
        }
        else
        {
            // Substituição completa: CID que saiu da lista do SER tem de sair daqui, senão a tela
            // ofereceria um código que o pedido não aceita mais.
            db.SerCatalogoCids.RemoveRange(
                await db.SerCatalogoCids.Where(c => c.ListaId == lista.Id)
                    .ToListAsync(cancellationToken));
        }

        lista.Quantidade = itens.Count;
        lista.SincronizadoEm = DateTime.UtcNow;

        foreach (var c in itens.DistinctBy(x => x.Codigo, StringComparer.Ordinal))
        {
            db.SerCatalogoCids.Add(new SerCatalogoCid
            {
                Id = Guid.NewGuid(),
                ListaId = lista.Id,
                Codigo = Truncar(c.Codigo, 10),
                Descricao = Truncar(c.Descricao, 400),
                Texto = Truncar(c.Texto, 420),
                Busca = Truncar(SerCidBusca.De(c.Codigo, c.Descricao), 420),
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return lista;
    }

    // ------------------------------------------------------------------ persistência

    private async Task<int> SalvarListaAsync(
        string lista, IReadOnlyList<Dtos.SerOpcaoDto> opcoes, DateTime agora,
        CancellationToken cancellationToken)
    {
        var existentes = await db.SerCatalogoListas
            .Where(x => x.Lista == lista)
            .ToDictionaryAsync(x => x.Valor, cancellationToken);

        var ordem = 0;
        // DEDUP DENTRO DO LOTE, não só contra o banco: o próprio SER repete opção. Medido em
        // 10/08/2026 — o combo de médicos traz 876 opções para 874 valores únicos ("DIEGO CESAR
        // BORGES" aparece duas vezes com o MESMO id). Sem isto, o segundo Add estoura o índice
        // único e a cópia inteira do catálogo morre com 409.
        foreach (var o in opcoes.DistinctBy(x => x.Valor))
        {
            if (existentes.TryGetValue(o.Valor, out var atual))
            {
                atual.Rotulo = o.Rotulo;
                atual.Ordem = ordem++;
                atual.SincronizadoEm = agora;
                continue;
            }

            db.SerCatalogoListas.Add(new SerCatalogoLista
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
        TipoRecursoSer tipo, bool ramo, IReadOnlyList<Dtos.SerOpcaoDto> doSer, DateTime agora,
        CancellationToken cancellationToken)
    {
        var existentes = await db.SerCatalogoRecursos
            .Where(x => x.Tipo == tipo && x.AmbulatorioEstadual == ramo)
            .ToDictionaryAsync(x => x.Valor, cancellationToken);

        // Mesma razão da lista: recurso repetido no combo derrubaria a cópia inteira.
        foreach (var o in doSer.DistinctBy(x => x.Valor))
        {
            if (existentes.TryGetValue(o.Valor, out var atual))
            {
                // Rótulo pode mudar sem o recurso mudar de identidade; campos não são invalidados
                // por isso — quem decide relê é o `refazerTudo`.
                atual.Rotulo = o.Rotulo;
                atual.SincronizadoEm = agora;
                continue;
            }

            db.SerCatalogoRecursos.Add(new SerCatalogoRecurso
            {
                Id = Guid.NewGuid(),
                Tipo = tipo,
                AmbulatorioEstadual = ramo,
                Valor = o.Valor,
                Rotulo = o.Rotulo,
                SincronizadoEm = agora,
                CamposLidos = false,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return doSer.DistinctBy(x => x.Valor).Count();
    }

    private async Task<int> SalvarCamposAsync(
        SerCatalogoRecurso recurso, IReadOnlyList<Dtos.SerCampoDinamicoDto> lidos,
        CancellationToken cancellationToken)
    {
        var atuais = await db.SerCatalogoCampos
            .Where(c => c.RecursoId == recurso.Id)
            .ToListAsync(cancellationToken);

        // Substituição completa: campo que sumiu do SER tem de sumir daqui, senão a tela pediria
        // dado que o SER não aceita mais — e o pedido seria recusado sem explicação.
        db.SerCatalogoCampos.RemoveRange(atuais);

        var ordem = 0;
        // Idem para os campos dinâmicos: dois `container_dinamico_id_N` com o mesmo N no mesmo
        // recurso estourariam `ux_ser_catalogo_campo`.
        foreach (var c in lidos.DistinctBy(x => x.Numero))
        {
            db.SerCatalogoCampos.Add(new SerCatalogoCampo
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

    private static string Truncar(string texto, int max) =>
        texto.Length <= max ? texto : texto[..max];
}
