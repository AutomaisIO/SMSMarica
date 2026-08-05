using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMarica.Data;
using SMSMarica.Data.Entities.Enums;
using SMSMarica.Data.Entities.Pep;

namespace SMSMarica.Core.Integracoes.Pep.Divergencias;

/// <summary>Dados de uma divergência detectada no upsert canônico (imutável, sem I/O).</summary>
public sealed record DivergenciaDetectada(
    long CdPaciente,
    string Cpf,
    TipoDivergenciaIdentidade Tipo,
    string ValorOrigem,
    string ValorHub,
    string? NomeOrigem,
    string? NomeHub,
    string? PatientIdHub,
    /// <summary>Código do paciente na origem, como texto — ver <c>PepDivergenciaIdentidade.CodigoOrigem</c>.</summary>
    string? CodigoOrigem = null);

/// <summary>
/// Sink durável de divergências de identidade de um run (mesmo padrão do
/// <see cref="Falhas.IRegistradorFalhasPep"/>): a estratégia só enfileira — não bloqueia a
/// importação nem faz I/O na thread do upsert. Um consumidor único drena e faz UPSERT em
/// lote num contexto próprio, então a trilha sobrevive a crash.
/// </summary>
public interface IRegistradorDivergenciasPep : IAsyncDisposable
{
    /// <summary>Enfileira uma divergência (idempotente por fonte+CPF+tipo — re-detecção soma ocorrência).</summary>
    void Registrar(DivergenciaDetectada divergencia);
}

/// <inheritdoc cref="IRegistradorDivergenciasPep"/>
public sealed class RegistradorDivergenciasPep : IRegistradorDivergenciasPep
{
    private const int TamanhoLote = 50;

    private readonly IDbContextFactory<SmsMaricaDbContext> _factory;
    private readonly ILogger _logger;
    private readonly Guid _execucaoId;
    private readonly Guid _fonteId;
    private readonly string _fonteSlug;
    private readonly Channel<DivergenciaDetectada> _canal;
    private readonly Task _consumidor;

    public RegistradorDivergenciasPep(
        IDbContextFactory<SmsMaricaDbContext> factory, ILogger logger,
        Guid execucaoId, Guid fonteId, string fonteSlug)
    {
        _factory = factory;
        _logger = logger;
        _execucaoId = execucaoId;
        _fonteId = fonteId;
        _fonteSlug = fonteSlug;
        _canal = Channel.CreateUnbounded<DivergenciaDetectada>(new UnboundedChannelOptions { SingleReader = true });
        _consumidor = Task.Run(ConsumirAsync);
    }

    public void Registrar(DivergenciaDetectada divergencia) => _canal.Writer.TryWrite(divergencia);

    private async Task ConsumirAsync()
    {
        var lote = new List<DivergenciaDetectada>(TamanhoLote);
        try
        {
            while (await _canal.Reader.WaitToReadAsync())
            {
                lote.Clear();
                while (lote.Count < TamanhoLote && _canal.Reader.TryRead(out var d))
                    lote.Add(d);
                if (lote.Count > 0) await GravarLoteAsync(lote);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Consumidor de divergências PEP da execução {Execucao} encerrou com erro.", _execucaoId);
        }
    }

    private async Task GravarLoteAsync(IReadOnlyList<DivergenciaDetectada> lote)
    {
        try
        {
            await using var db = await _factory.CreateDbContextAsync();
            var agora = DateTime.UtcNow;

            // Dedupe intra-lote (o mesmo CPF pode aparecer 2x num chunk com cds redundantes —
            // o Salux tem 5.656 cadastros redundantes por CPF).
            var porChave = lote
                .GroupBy(d => (d.Cpf, d.Tipo))
                .Select(g => g.Last())
                .ToList();

            var cpfs = porChave.Select(d => d.Cpf).ToList();
            var existentes = await db.PepDivergenciasIdentidade
                .Where(x => x.FonteId == _fonteId && cpfs.Contains(x.Cpf))
                .ToListAsync();

            foreach (var d in porChave)
            {
                var atual = existentes.FirstOrDefault(x => x.Cpf == d.Cpf && x.Tipo == d.Tipo);
                if (atual is null)
                {
                    db.PepDivergenciasIdentidade.Add(new PepDivergenciaIdentidade
                    {
                        Id = Guid.CreateVersion7(),
                        ExecucaoId = _execucaoId,
                        FonteId = _fonteId,
                        FonteSlug = _fonteSlug,
                        CdPaciente = d.CdPaciente,
                        CodigoOrigem = d.CodigoOrigem,
                        Cpf = d.Cpf,
                        Tipo = d.Tipo,
                        ValorOrigem = d.ValorOrigem,
                        ValorHub = d.ValorHub,
                        NomeOrigem = Truncar(d.NomeOrigem, 200),
                        NomeHub = Truncar(d.NomeHub, 200),
                        PatientIdHub = d.PatientIdHub,
                        Status = StatusDivergenciaIdentidade.Pendente,
                        Veredicto = VeredictoDivergenciaIdentidade.Indefinido,
                        Ocorrencias = 1,
                        CriadoEm = agora,
                        AtualizadoEm = agora,
                    });
                    continue;
                }

                // Re-detecção: soma ocorrência e atualiza o retrato atual dos valores.
                atual.ExecucaoId = _execucaoId;
                atual.CdPaciente = d.CdPaciente;
                atual.CodigoOrigem = d.CodigoOrigem;
                atual.Ocorrencias++;
                atual.AtualizadoEm = agora;
                atual.PatientIdHub = d.PatientIdHub ?? atual.PatientIdHub;
                atual.NomeOrigem = Truncar(d.NomeOrigem, 200) ?? atual.NomeOrigem;
                atual.NomeHub = Truncar(d.NomeHub, 200) ?? atual.NomeHub;

                // Os valores mudaram desde a arbitragem anterior → o veredicto velho não vale mais.
                if (atual.ValorOrigem != d.ValorOrigem || atual.ValorHub != d.ValorHub)
                {
                    atual.ValorOrigem = d.ValorOrigem;
                    atual.ValorHub = d.ValorHub;
                    atual.Status = StatusDivergenciaIdentidade.Pendente;
                    atual.Veredicto = VeredictoDivergenciaIdentidade.Indefinido;
                    atual.ValorCorreto = null;
                    atual.VeredictoMotor = null;
                    atual.Detalhe = null;
                    atual.VerificadoEm = null;
                }
            }

            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Nunca derruba a importação por causa do registro — mas isto é grave o bastante
            // para não ficar só em Debug: divergência não registrada é divergência invisível.
            _logger.LogError(ex, "Falha ao persistir {Qtd} divergências da execução {Execucao}.", lote.Count, _execucaoId);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _canal.Writer.TryComplete();
        try { await _consumidor; }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao drenar divergências PEP da execução {Execucao}.", _execucaoId); }
    }

    private static string? Truncar(string? s, int max) =>
        s is null ? null : s.Length <= max ? s : s[..max];
}
