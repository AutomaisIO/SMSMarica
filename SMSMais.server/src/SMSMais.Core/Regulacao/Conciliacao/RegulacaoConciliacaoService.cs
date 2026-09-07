using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using SMSMais.Core.Regulacao.Solicitacoes;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Regulacao;

namespace SMSMais.Core.Regulacao.Conciliacao;

public interface IRegulacaoConciliacaoService
{
    /// <summary>Casa solicitações nossas com o espelho do SER pelo número externo. Devolve quantas mudaram.</summary>
    Task<int> ConciliarSerAsync(IReadOnlyCollection<string> idsSer, CancellationToken ct);

    Task<int> ConciliarSernitAsync(IReadOnlyCollection<string> idsSernit, CancellationToken ct);

    Task<int> ConciliarSisregAsync(IReadOnlyCollection<string> codigosSolicitacao, CancellationToken ct);
}

/// <summary>
/// Liga a solicitação nossa ao registro que existe no sistema de regulação, e traz de lá a
/// situação (plano 05).
///
/// <para><b>O número externo é a chave.</b> Depois que ele existe, é por ele que os dois lados se
/// reconhecem — e é isso que faz a fila da unidade continuar contando a verdade sem ninguém
/// digitar nada: a varredura já lia o SER e o SERNIT; o que faltava era o encontro.</para>
///
/// <para><b>Roda como sistema, sem usuário.</b> Por isso os eventos saem com papel
/// <see cref="PapelEventoRegulacao.Sistema"/> — a linha do tempo distingue "a varredura trouxe"
/// de "alguém mudou".</para>
///
/// <para><b>Nunca falha o lote inteiro por causa de um caso.</b> Quem chama é a varredura, e
/// derrubar uma varredura de rede inteira por uma solicitação torta seria trocar um problema
/// pequeno por um grande.</para>
/// </summary>
public sealed class RegulacaoConciliacaoService(
    SmsMaisDbContext db,
    IRegulacaoEventoService eventos,
    ILogger<RegulacaoConciliacaoService> log) : IRegulacaoConciliacaoService
{
    public Task<int> ConciliarSerAsync(IReadOnlyCollection<string> idsSer, CancellationToken ct) =>
        ConciliarAsync(
            SistemaRegulacao.Ser, idsSer,
            async (numeros, c) => await db.SerSolicitacoes.AsNoTracking()
                .Where(x => numeros.Contains(x.IdSer))
                .Select(x => new EspelhoLido(x.IdSer, x.Id, MapaSituacaoExterna.DeSer(x.Situacao)))
                .ToListAsync(c),
            (s, espelho) => s.SerSolicitacaoId = espelho,
            ct);

    public Task<int> ConciliarSernitAsync(IReadOnlyCollection<string> idsSernit, CancellationToken ct) =>
        ConciliarAsync(
            SistemaRegulacao.Sernit, idsSernit,
            async (numeros, c) => await db.SernitSolicitacoes.AsNoTracking()
                .Where(x => numeros.Contains(x.IdSernit))
                .Select(x => new EspelhoLido(x.IdSernit, x.Id, MapaSituacaoExterna.DeSernit(x.Situacao)))
                .ToListAsync(c),
            (s, espelho) => s.SernitSolicitacaoId = espelho,
            ct);

    public Task<int> ConciliarSisregAsync(
        IReadOnlyCollection<string> codigosSolicitacao, CancellationToken ct) =>
        ConciliarAsync(
            SistemaRegulacao.Sisreg, codigosSolicitacao,
            async (numeros, c) => await db.Solicitacoes.AsNoTracking()
                .Where(x => x.CodigoSolicitacao != null && numeros.Contains(x.CodigoSolicitacao))
                .Select(x => new EspelhoLido(x.CodigoSolicitacao!, x.Id, MapaSituacaoExterna.DeSisreg(x.Status)))
                .ToListAsync(c),
            (s, espelho) => s.SolicitacaoId = espelho,
            ct);

    /// <param name="LerEspelhos">Como achar, naquele sistema, os registros dos números dados.</param>
    /// <param name="Ligar">Onde guardar a FK do espelho na nossa solicitação.</param>
    private async Task<int> ConciliarAsync(
        SistemaRegulacao sistema,
        IReadOnlyCollection<string> numeros,
        Func<string[], CancellationToken, Task<List<EspelhoLido>>> lerEspelhos,
        Action<RegulacaoSolicitacao, Guid> ligar,
        CancellationToken ct)
    {
        var chaves = numeros
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (chaves.Length == 0) return 0;

        var nossas = await db.RegulacaoSolicitacoes
            .Where(s => s.SistemaDestino == sistema
                && s.NumeroExterno != null
                && chaves.Contains(s.NumeroExterno)
                && s.ExcluidoEm == null)
            .ToListAsync(ct);
        if (nossas.Count == 0) return 0;

        var espelhos = (await lerEspelhos(chaves, ct))
            .GroupBy(e => e.Numero, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var mudadas = 0;
        foreach (var s in nossas)
        {
            if (!espelhos.TryGetValue(s.NumeroExterno!, out var espelho)) continue;

            try
            {
                var mudou = false;

                // A FK é o vínculo permanente: uma vez ligada, a tela do caso alcança o espelho
                // sem procurar por número de novo.
                if (s.SolicitacaoId is null && s.SerSolicitacaoId is null && s.SernitSolicitacaoId is null)
                {
                    ligar(s, espelho.EspelhoId);
                    mudou = true;
                }

                if (espelho.Novo is { } novo
                    && MapaSituacaoExterna.DeveAplicar(s.Status, novo)
                    && MaquinaDeEstadosRegulacao.PodeTransitar(s.Status, novo, PapelEventoRegulacao.Sistema))
                {
                    var de = s.Status;
                    s.Status = novo;
                    await eventos.RegistrarAsync(
                        s.Id, TipoEventoRegulacao.SituacaoExterna, PapelEventoRegulacao.Sistema, ct,
                        de: de, para: novo,
                        detalhe: new { sistema = sistema.ToString(), numeroExterno = s.NumeroExterno });
                    mudou = true;
                }

                if (mudou)
                {
                    s.AtualizadoEm = DateTime.UtcNow;
                    mudadas++;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Um caso torto não derruba a varredura de rede inteira.
                log.LogWarning(
                    ex, "Conciliação {Sistema} falhou na solicitação {Id} (número {Numero}).",
                    sistema, s.Id, s.NumeroExterno);
            }
        }

        if (mudadas > 0) await db.SaveChangesAsync(ct);
        return mudadas;
    }

    private sealed record EspelhoLido(string Numero, Guid EspelhoId, StatusRegulacao? Novo);
}
