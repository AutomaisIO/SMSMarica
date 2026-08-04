using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMarica.Data;
using SMSMarica.Data.Entities.Pep;

namespace SMSMarica.Core.Integracoes.Pep.Falhas;

/// <summary>
/// Sink durável de falhas de um run de importação. As threads da estratégia chamam
/// <see cref="Registrar"/> (não-bloqueante: só enfileira); um consumidor único drena o canal
/// e grava em lotes num <see cref="SmsMaricaDbContext"/> próprio (criado pela factory — seguro
/// para concorrência, sem compartilhar o context do orquestrador). As escritas saem na hora,
/// então a trilha sobrevive a crash/órfã. Ao final, <see cref="DisposeAsync"/> drena o resto.
/// </summary>
public interface IRegistradorFalhasPep : IAsyncDisposable
{
    /// <summary>Enfileira uma falha para persistência durável (não bloqueia a importação).</summary>
    void Registrar(long cdPaciente, string mensagem);
}

/// <summary>
/// Implementação por run: instanciada com o contexto da execução (id/fonte/slug) e descartada
/// ao fim. Não é singleton de DI — o orquestrador cria uma por importação.
/// </summary>
public sealed class RegistradorFalhasPep : IRegistradorFalhasPep
{
    private const int TamanhoLote = 50;

    private readonly IDbContextFactory<SmsMaricaDbContext> _factory;
    private readonly ILogger _logger;
    private readonly Guid _execucaoId;
    private readonly Guid _fonteId;
    private readonly string _fonteSlug;
    private readonly Channel<PepSincronizacaoFalha> _canal;
    private readonly Task _consumidor;

    public RegistradorFalhasPep(
        IDbContextFactory<SmsMaricaDbContext> factory, ILogger logger,
        Guid execucaoId, Guid fonteId, string fonteSlug)
    {
        _factory = factory;
        _logger = logger;
        _execucaoId = execucaoId;
        _fonteId = fonteId;
        _fonteSlug = fonteSlug;
        _canal = Channel.CreateUnbounded<PepSincronizacaoFalha>(
            new UnboundedChannelOptions { SingleReader = true });
        _consumidor = Task.Run(ConsumirAsync);
    }

    /// <summary>
    /// Teto de falhas persistidas POR EXECUÇÃO. A trilha existe para alimentar o reimport
    /// direcionado; um run com mais falhas que isso está sistemicamente quebrado, e gravar o
    /// resto só incha a tabela — o incidente de 04/08 depositou 1,43 milhão de linhas idênticas
    /// aqui. O excedente é contado e logado, não gravado.
    /// </summary>
    private const int MaxPorExecucao = 20_000;

    private int _registradas;
    private long _suprimidas;

    public void Registrar(long cdPaciente, string mensagem)
    {
        if (Interlocked.Increment(ref _registradas) > MaxPorExecucao)
        {
            if (Interlocked.Increment(ref _suprimidas) == 1)
            {
                _logger.LogWarning(
                    "Trilha de falhas da execução {Execucao} atingiu o teto de {Max} — as demais serão contadas, não gravadas.",
                    _execucaoId, MaxPorExecucao);
            }
            return;
        }

        _canal.Writer.TryWrite(new PepSincronizacaoFalha
        {
            Id = Guid.CreateVersion7(),
            ExecucaoId = _execucaoId,
            FonteId = _fonteId,
            FonteSlug = _fonteSlug,
            CdPaciente = cdPaciente,
            Mensagem = Truncar(mensagem, 2000),
            CriadoEm = DateTime.UtcNow,
        });
    }

    private async Task ConsumirAsync()
    {
        var lote = new List<PepSincronizacaoFalha>(TamanhoLote);
        try
        {
            while (await _canal.Reader.WaitToReadAsync())
            {
                lote.Clear();
                while (lote.Count < TamanhoLote && _canal.Reader.TryRead(out var f))
                    lote.Add(f);
                if (lote.Count > 0)
                    await GravarLoteAsync(lote);
            }
        }
        catch (Exception ex)
        {
            // Nunca derruba a importação por causa do registro de falhas — só loga.
            _logger.LogError(ex, "Consumidor de falhas PEP da execução {Execucao} encerrou com erro.", _execucaoId);
        }
    }

    private async Task GravarLoteAsync(IReadOnlyList<PepSincronizacaoFalha> lote)
    {
        try
        {
            await using var db = await _factory.CreateDbContextAsync();
            db.PepSincronizacaoFalhas.AddRange(lote);
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao persistir {Qtd} falhas da execução {Execucao}.", lote.Count, _execucaoId);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _canal.Writer.TryComplete();
        try { await _consumidor; }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao drenar falhas PEP da execução {Execucao}.", _execucaoId); }
    }

    private static string Truncar(string s, int max) => s.Length <= max ? s : s[..max];
}
