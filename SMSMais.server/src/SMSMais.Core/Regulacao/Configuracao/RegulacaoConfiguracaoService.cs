using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Core.Regulacao.Configuracao.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities.Regulacao;

namespace SMSMais.Core.Regulacao.Configuracao;

public interface IRegulacaoConfiguracaoService
{
    /// <summary>Configuração completa. Cria a linha default na primeira vez que é chamada.</summary>
    Task<RegulacaoConfiguracaoDto> ObterAsync(CancellationToken ct);

    /// <summary>Subconjunto para o wizard — sem os parâmetros de operação.</summary>
    Task<RegulacaoConfiguracaoFluxoDto> ObterFluxoAsync(CancellationToken ct);

    Task<RegulacaoConfiguracaoDto> AtualizarAsync(
        AtualizarRegulacaoConfiguracaoRequest req, CancellationToken ct);

    /// <summary>A entidade em si, para quem precisa dos valores sem montar DTO (a busca usa).</summary>
    Task<RegulacaoConfiguracao> ObterEntidadeAsync(CancellationToken ct);

    /// <summary>
    /// Liga (data) ou desliga (<c>null</c>) o corte dos rascunhos por sistema. Fica fora do
    /// <c>PUT</c> da configuração de propósito: é consequência da migração, não um campo que
    /// alguém digita junto com o rótulo da fila.
    /// </summary>
    Task DefinirRascunhosLegadosMigradosAsync(DateTime? em, CancellationToken ct);
}

/// <inheritdoc cref="IRegulacaoConfiguracaoService"/>
public sealed class RegulacaoConfiguracaoService(
    SmsMaisDbContext db,
    IMemoryCache cache,
    IUsuarioAtualAccessor usuarioAtual) : IRegulacaoConfiguracaoService
{
    private const string ChaveCache = "regulacao-configuracao";

    /// <summary>
    /// 30 segundos: a busca lê o corte a cada tecla digitada, e ir ao banco nisso seria uma
    /// consulta por keystroke. Meio minuto é curto o bastante para a calibração pela tela
    /// parecer imediata a quem está ajustando.
    /// </summary>
    private static readonly TimeSpan DuracaoCache = TimeSpan.FromSeconds(30);

    public async Task<RegulacaoConfiguracao> ObterEntidadeAsync(CancellationToken ct)
    {
        if (cache.TryGetValue<RegulacaoConfiguracao>(ChaveCache, out var emCache) && emCache is not null)
        {
            return emCache;
        }

        var config = await CarregarOuCriarAsync(ct);
        cache.Set(ChaveCache, config, DuracaoCache);
        return config;
    }

    private async Task<RegulacaoConfiguracao> CarregarOuCriarAsync(CancellationToken ct)
    {
        var existente = await db.RegulacaoConfiguracoes.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == RegulacaoConfiguracao.IdSingleton, ct);
        if (existente is not null) return existente;

        // A linha default nasce aqui, não em migration: migration é imutável e roda igual em
        // toda instância nova (CLAUDE.md, regra 9).
        var nova = new RegulacaoConfiguracao();
        db.RegulacaoConfiguracoes.Add(nova);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Duas requisições simultâneas na primeira vez: a chave primária fixa faz a segunda
            // colidir. Quem perdeu simplesmente lê o que a outra gravou.
            db.Entry(nova).State = EntityState.Detached;
            return await db.RegulacaoConfiguracoes.AsNoTracking()
                .FirstAsync(c => c.Id == RegulacaoConfiguracao.IdSingleton, ct);
        }
        db.Entry(nova).State = EntityState.Detached;
        return nova;
    }

    public async Task DefinirRascunhosLegadosMigradosAsync(DateTime? em, CancellationToken ct)
    {
        await CarregarOuCriarAsync(ct);
        var atual = await db.RegulacaoConfiguracoes
            .FirstAsync(c => c.Id == RegulacaoConfiguracao.IdSingleton, ct);

        atual.RascunhosLegadosMigradosEm = em;
        atual.AtualizadoEm = DateTime.UtcNow;
        atual.AtualizadoPor = usuarioAtual.UsuarioId;
        await db.SaveChangesAsync(ct);

        // Sem limpar o cache, as telas antigas continuariam aceitando escrita por até 30 s
        // depois de a migração ter copiado os rascunhos — e essa edição se perderia.
        cache.Remove(ChaveCache);
    }

    public async Task<RegulacaoConfiguracaoDto> ObterAsync(CancellationToken ct)
    {
        var c = await ObterEntidadeAsync(ct);

        var nome = c.AtualizadoPor is null
            ? null
            : await db.Usuarios.AsNoTracking()
                .Where(u => u.Id == c.AtualizadoPor)
                .Select(u => u.NomeCompleto)
                .FirstOrDefaultAsync(ct);

        return new RegulacaoConfiguracaoDto(
            c.PermitirExternoComInterno,
            c.PontaPodeEscolherUnidade,
            c.PontaPodeVerTodasUnidades,
            c.ExigirCpf,
            c.RotuloFila,
            c.SisregPrazoEdicaoDias,
            c.BuscaCorteDistancia,
            c.BuscaScoreSugestaoPareamento,
            JsonDocument.Parse(string.IsNullOrWhiteSpace(c.RegrasFollowupJson) ? "[]" : c.RegrasFollowupJson).RootElement.Clone(),
            c.AnexoLimiteMb,
            c.AnexoTiposPermitidos,
            c.NaoSeiPadrao,
            c.RascunhosLegadosMigradosEm,
            c.AtualizadoEm,
            nome,
            c.RowVersion);
    }

    public async Task<RegulacaoConfiguracaoFluxoDto> ObterFluxoAsync(CancellationToken ct)
    {
        var c = await ObterEntidadeAsync(ct);
        return new RegulacaoConfiguracaoFluxoDto(
            c.PermitirExternoComInterno,
            c.PontaPodeVerTodasUnidades,
            c.ExigirCpf,
            c.RotuloFila,
            c.AnexoLimiteMb,
            c.AnexoTiposPermitidos);
    }

    public async Task<RegulacaoConfiguracaoDto> AtualizarAsync(
        AtualizarRegulacaoConfiguracaoRequest req, CancellationToken ct)
    {
        await CarregarOuCriarAsync(ct);

        var atual = await db.RegulacaoConfiguracoes
            .FirstAsync(c => c.Id == RegulacaoConfiguracao.IdSingleton, ct);

        if (atual.RowVersion != req.RowVersion)
        {
            throw new ConflitoException(
                "regulacao.configuracao.desatualizada",
                "Alguém salvou esta configuração antes de você. Recarregue a tela e refaça a alteração.");
        }

        atual.PermitirExternoComInterno = req.PermitirExternoComInterno;
        atual.PontaPodeEscolherUnidade = req.PontaPodeEscolherUnidade;
        atual.PontaPodeVerTodasUnidades = req.PontaPodeVerTodasUnidades;
        atual.ExigirCpf = req.ExigirCpf;
        atual.RotuloFila = req.RotuloFila.Trim();
        atual.SisregPrazoEdicaoDias = req.SisregPrazoEdicaoDias;
        atual.BuscaCorteDistancia = req.BuscaCorteDistancia;
        atual.BuscaScoreSugestaoPareamento = req.BuscaScoreSugestaoPareamento;
        atual.AnexoLimiteMb = req.AnexoLimiteMb;
        atual.AnexoTiposPermitidos = req.AnexoTiposPermitidos;
        atual.NaoSeiPadrao = req.NaoSeiPadrao;
        if (req.RegrasFollowup is { } regras)
        {
            atual.RegrasFollowupJson = regras.GetRawText();
        }
        atual.AtualizadoEm = DateTime.UtcNow;
        atual.AtualizadoPor = usuarioAtual.UsuarioId;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflitoException(
                "regulacao.configuracao.desatualizada",
                "Alguém salvou esta configuração antes de você. Recarregue a tela e refaça a alteração.");
        }

        // Sem isto, a tela mostraria o valor novo e a busca continuaria usando o antigo por até
        // 30 s — o tipo de divergência que faz alguém concluir que a calibração "não funciona".
        cache.Remove(ChaveCache);
        return await ObterAsync(ct);
    }
}
