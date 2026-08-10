using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMarica.Data;
using SMSMarica.Data.Entities.Ser;

namespace SMSMarica.Core.Ser;

/// <summary>Resultado de uma sincronização do catálogo.</summary>
public sealed record SerCatalogoSyncResultadoDto(
    int Recursos, int Campos, int Listas, int Falhas, int DuracaoSegundos);

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
    SmsMaricaDbContext db,
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

        // ---- recursos e seus campos ----
        foreach (var (codigo, tipo) in Tipos)
        {
            var doSer = await leitor.ListarRecursosAsync(codigo, cancellationToken);
            recursos += await SalvarRecursosAsync(tipo, doSer, agora, cancellationToken);

            // Retomada: só pede ao SER o que ainda não tem campo lido. Refazer tudo é escolha
            // explícita (o catálogo muda pouco, e cada recurso custa uma requisição).
            var pendentes = await db.SerCatalogoRecursos
                .Where(r => r.Tipo == tipo && (refazerTudo || !r.CamposLidos))
                .OrderBy(r => r.Valor)
                .ToListAsync(cancellationToken);

            logger.LogInformation(
                "SER/catálogo: {Tipo} — {Total} recursos, {Pendentes} a ler campos.",
                tipo, doSer.Count, pendentes.Count);

            foreach (var recurso in pendentes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var lidos = await leitor.ObterCamposDinamicosAsync(
                        codigo, recurso.Valor, cancellationToken);
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

        var duracao = (int)(DateTime.UtcNow - inicio).TotalSeconds;
        logger.LogInformation(
            "SER/catálogo: {Recursos} recursos, {Campos} campos, {Listas} itens de lista, "
            + "{Falhas} falhas em {Seg}s.", recursos, campos, listas, falhas, duracao);

        return new SerCatalogoSyncResultadoDto(recursos, campos, listas, falhas, duracao);
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
        foreach (var o in opcoes)
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
        return opcoes.Count;
    }

    private async Task<int> SalvarRecursosAsync(
        TipoRecursoSer tipo, IReadOnlyList<Dtos.SerOpcaoDto> doSer, DateTime agora,
        CancellationToken cancellationToken)
    {
        var existentes = await db.SerCatalogoRecursos
            .Where(x => x.Tipo == tipo)
            .ToDictionaryAsync(x => x.Valor, cancellationToken);

        foreach (var o in doSer)
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
                Valor = o.Valor,
                Rotulo = o.Rotulo,
                SincronizadoEm = agora,
                CamposLidos = false,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return doSer.Count;
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
        foreach (var c in lidos)
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

        return lidos.Count;
    }

    private static string Truncar(string texto, int max) =>
        texto.Length <= max ? texto : texto[..max];
}
