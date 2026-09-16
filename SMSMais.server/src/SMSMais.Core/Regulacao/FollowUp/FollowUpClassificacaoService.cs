using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Regulacao.Configuracao;
using SMSMais.Data;
using SMSMais.Data.Entities.Regulacao;

namespace SMSMais.Core.Regulacao.FollowUp;

/// <summary>Resultado de classificar um texto de FollowUP: a categoria e o hash das regras
/// que a produziram (o que permite reclassificar quando as regras mudarem).</summary>
public sealed record ClassificacaoFollowUp(string Categoria, string RegrasHash);

public sealed record ResultadoClassificacaoLote(int Ser, int Sernit)
{
    public int Total => Ser + Sernit;
}

/// <summary>
/// Classificação de FollowUP <b>persistida</b>: aplica o <see cref="ClassificadorFollowUp"/> (regex
/// versionadas na configuração da regulação) ao texto do evento e grava categoria + hash das
/// regras em <c>ser_evento</c>/<c>sernit_evento</c>.
///
/// <para>Dois caminhos usam isto: o sincronizador, na captura (evento novo já nasce
/// classificado), e o worker em lote, que cobre o passado e reclassifica quando alguém edita as
/// regras pela tela — o hash gravado no evento é o que diz se ele está em dia.</para>
/// </summary>
public interface IFollowUpClassificacaoService
{
    /// <summary>Classifica um texto com as regras atuais. Barato para chamar em laço: as regras
    /// ficam em cache (na configuração, 30 s) e o parse do JSON é feito uma vez por instância.</summary>
    Task<ClassificacaoFollowUp> ClassificarAsync(string? texto, CancellationToken ct);

    /// <summary>Classifica até <paramref name="lote"/> FollowUPs de cada sistema que ainda não têm
    /// categoria ou foram classificados por regras diferentes das atuais. Devolve quantos tocou;
    /// zero significa que não há mais nada pendente.</summary>
    Task<ResultadoClassificacaoLote> ClassificarPendentesAsync(int lote, CancellationToken ct);

    /// <summary>Quantos FollowUPs ainda esperam classificação (ou reclassificação) — para a tela
    /// de configuração mostrar que a edição das regras está sendo aplicada.</summary>
    Task<ResultadoClassificacaoLote> ContarPendentesAsync(CancellationToken ct);
}

public sealed class FollowUpClassificacaoService(
    SmsMaisDbContext db,
    IRegulacaoConfiguracaoService configuracao,
    ILogger<FollowUpClassificacaoService> logger) : IFollowUpClassificacaoService
{
    private string? _hashCarregado;
    private IReadOnlyList<RegraFollowUp> _regras = [];

    public async Task<ClassificacaoFollowUp> ClassificarAsync(string? texto, CancellationToken ct)
    {
        var hash = await CarregarRegrasAsync(ct);
        var r = ClassificadorFollowUp.Classificar(texto, _regras);
        return new ClassificacaoFollowUp(r.Categoria, hash);
    }

    public async Task<ResultadoClassificacaoLote> ClassificarPendentesAsync(int lote, CancellationToken ct)
    {
        var hash = await CarregarRegrasAsync(ct);

        // Os dois sistemas no mesmo SaveChanges: um lote é uma unidade de trabalho pequena e
        // reversível; não há motivo para duas transações.
        var ser = await db.SerEventos
            .Where(e => e.TipoEvento == TipoEventoExterno.FollowUp
                        && (e.FollowUpRegrasHash == null || e.FollowUpRegrasHash != hash))
            .OrderBy(e => e.Id)
            .Take(lote)
            .ToListAsync(ct);

        foreach (var e in ser)
        {
            e.FollowUpCategoria = ClassificadorFollowUp.Classificar(e.Observacao, _regras).Categoria;
            e.FollowUpRegrasHash = hash;
        }

        var sernit = await db.SernitEventos
            .Where(e => e.TipoEvento == TipoEventoExterno.FollowUp
                        && (e.FollowUpRegrasHash == null || e.FollowUpRegrasHash != hash))
            .OrderBy(e => e.Id)
            .Take(lote)
            .ToListAsync(ct);

        foreach (var e in sernit)
        {
            e.FollowUpCategoria = ClassificadorFollowUp.Classificar(e.Observacao, _regras).Categoria;
            e.FollowUpRegrasHash = hash;
        }

        if (ser.Count + sernit.Count > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogDebug("FollowUP: lote classificado — SER {Ser}, SERNIT {Sernit} (regras {Hash}).",
                ser.Count, sernit.Count, hash);
        }

        return new ResultadoClassificacaoLote(ser.Count, sernit.Count);
    }

    public async Task<ResultadoClassificacaoLote> ContarPendentesAsync(CancellationToken ct)
    {
        var hash = await CarregarRegrasAsync(ct);
        var ser = await db.SerEventos.CountAsync(
            e => e.TipoEvento == TipoEventoExterno.FollowUp
                 && (e.FollowUpRegrasHash == null || e.FollowUpRegrasHash != hash), ct);
        var sernit = await db.SernitEventos.CountAsync(
            e => e.TipoEvento == TipoEventoExterno.FollowUp
                 && (e.FollowUpRegrasHash == null || e.FollowUpRegrasHash != hash), ct);
        return new ResultadoClassificacaoLote(ser, sernit);
    }

    /// <summary>Carrega (ou reaproveita) as regras e devolve o hash delas. Reparseia só quando o
    /// JSON gravado mudou — o sincronizador chama isto centenas de vezes por rodada.</summary>
    private async Task<string> CarregarRegrasAsync(CancellationToken ct)
    {
        var config = await configuracao.ObterEntidadeAsync(ct);
        var hash = ClassificadorEventoRegulacao.HashDasRegras(config.RegrasFollowupJson);
        if (hash != _hashCarregado)
        {
            _regras = ClassificadorFollowUp.Ler(config.RegrasFollowupJson);
            _hashCarregado = hash;
        }

        return hash;
    }
}
