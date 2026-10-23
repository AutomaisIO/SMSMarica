using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Integracoes.Credenciais;
using SMSMais.Core.Integracoes.EsusPec;
using SMSMais.Data;
using SMSMais.Data.Entities.FaturamentoProducao;

namespace SMSMais.Core.FaturamentoProducao;

/// <summary>Resultado de uma passagem da conciliação (para log).</summary>
public sealed record ResumoReconciliacaoProfissional(int Candidatos, int Resolvidos, int NaoEncontrados, string? Interrompida);

/// <summary>
/// Rotina da madrugada que concilia o <b>CNS/CBO do profissional executante</b> a partir do e-SUS PEC
/// (ADR-0067), para o faturamento BPA de produção. A <c>solicitacao</c> do SISREG só guarda o CPF do
/// executante; aqui, de madrugada, cada CPF ainda não conciliado é resolvido no PEC (ProfissionalSelect
/// por CPF) e gravado no cache <c>faturamento_profissional_cns</c>. A geração do BPA lê só o cache —
/// quem o e-SUS não tiver fica como pendência (e é reconsultado na próxima passagem).
///
/// <para>Janela: respeita o <b>período de funcionamento configurado na credencial</b>
/// (<c>janelaInicio</c>/<c>janelaFim</c> do provedor <c>esuspec</c>, mesmas chaves da
/// <see cref="Integracoes.EsusPec.ICorrecaoTelefoneEsusService"/>). <b>Sem janela configurada, roda a
/// qualquer hora</b> — a credencial do e-SUS é dedicada e usável o dia todo. O PEC é SESSÃO ÚNICA (uma
/// por conta): se a rotina de telefone estiver na mesma credencial, uma derruba a outra e a perdedora
/// tenta de novo na volta seguinte — a conciliação precisa de uma passagem bem-sucedida só.</para>
/// </summary>
public interface IReconciliacaoProfissionalEsusService
{
    Task<ResumoReconciliacaoProfissional?> ExecutarSeNaJanelaAsync(CancellationToken ct = default);
}

public sealed class ReconciliacaoProfissionalEsusService(
    SmsMaisDbContext db,
    IIntegracaoCredencialService credenciais,
    ILogger<ReconciliacaoProfissionalEsusService> logger) : IReconciliacaoProfissionalEsusService
{
    private const string Provedor = "esuspec";
    private const int MaximoPorPassagem = 200; // executantes são poucos; teto só por segurança

    public async Task<ResumoReconciliacaoProfissional?> ExecutarSeNaJanelaAsync(CancellationToken ct = default)
    {
        IntegracaoCredencialContexto cred;
        try { cred = await credenciais.ObterContextoAsync(Provedor, ct); }
        catch (ValidacaoException) { return null; } // e-SUS não configurado / desativado
        if (string.IsNullOrWhiteSpace(cred.ClientId) || string.IsNullOrWhiteSpace(cred.ClientSecret)) return null;

        var (baseUrl, acessoId, inicio, fim) = LerParametros(cred.ParametrosJson);
        if (!DentroDaJanela(inicio, fim, AgoraBrasilia())) return null;

        var alvo = await CandidatosAsync(ct);
        if (alvo.Count == 0) return null;

        logger.LogInformation("Conciliação de profissionais (BPA) pelo e-SUS: {Qtd} CPF(s) a resolver.", alvo.Count);

        int resolvidos = 0, naoEncontrados = 0;
        string? interrompida = null;
        using var pec = new EsusPecCliente(baseUrl);
        try
        {
            // De madrugada ninguém está usando a conta: forçar é seguro (é o motivo da janela).
            await pec.LoginAsync(cred.ClientId, cred.ClientSecret, forcar: true, ct);
            await pec.SelecionarAcessoAsync(acessoId, ct);

            foreach (var (cpf, cnes) in alvo)
            {
                if (!DentroDaJanela(inicio, fim, AgoraBrasilia())) { interrompida = "fim da janela"; break; }
                try
                {
                    var prof = await pec.BuscarProfissionalAsync(cpf, cnes, ct);
                    if (prof?.Cns is not { Length: > 0 } cns) { naoEncontrados++; continue; }
                    db.FaturamentoProfissionaisCns.Add(new FaturamentoProfissionalCns
                    {
                        Cpf = cpf, Cns = cns, Cbo = prof.Cbo, Nome = prof.Nome,
                        Origem = "PEC", AtualizadoEm = DateTime.UtcNow,
                    });
                    await db.SaveChangesAsync(ct);
                    resolvidos++;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    db.ChangeTracker.Clear();
                    logger.LogWarning(ex, "Conciliação de profissionais (BPA): falha no CPF (resolvido na próxima madrugada).");
                }
            }
        }
        catch (ErroEsusPec e)
        {
            interrompida = e.Message;
            logger.LogWarning("Conciliação de profissionais (BPA): sessão no e-SUS falhou — {Erro}", e.Message);
        }
        finally
        {
            await pec.LogoutAsync(CancellationToken.None);
        }

        var resumo = new ResumoReconciliacaoProfissional(alvo.Count, resolvidos, naoEncontrados, interrompida);
        logger.LogInformation("Conciliação de profissionais (BPA) pelo e-SUS: {@Resumo}", resumo);
        return resumo;
    }

    /// <summary>CPFs de executante (de solicitações com comparecimento) que ainda não estão no cache,
    /// com o CNES onde executam (para o CBO sair da lotação certa).</summary>
    private async Task<IReadOnlyList<(string Cpf, string? Cnes)>> CandidatosAsync(CancellationToken ct)
    {
        var pares = await db.Solicitacoes.AsNoTracking()
            .Where(s => s.ChegadaConfirmadaSisreg == true && s.ExcluidoEm == null
                && s.ProfissionalExecutanteCpf != null && s.ProfissionalExecutanteCpf != "")
            .Select(s => new { s.ProfissionalExecutanteCpf, Cnes = s.UnidadeExecutante!.Cnes })
            .Distinct()
            .ToListAsync(ct);

        var jaCache = (await db.FaturamentoProfissionaisCns.AsNoTracking().Select(p => p.Cpf).ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);

        var alvo = new Dictionary<string, string?>();
        foreach (var p in pares)
        {
            var cpf = SoDigitos(p.ProfissionalExecutanteCpf);
            if (cpf.Length != 11 || jaCache.Contains(cpf)) continue;
            if (!alvo.ContainsKey(cpf)) alvo[cpf] = p.Cnes; // primeiro CNES visto como preferido
            if (alvo.Count >= MaximoPorPassagem) break;
        }
        return [.. alvo.Select(kv => (kv.Key, kv.Value))];
    }

    private static string SoDigitos(string? v) => new((v ?? string.Empty).Where(char.IsDigit).ToArray());
    private static TimeOnly AgoraBrasilia() => TimeOnly.FromDateTime(FusoBrasilia.ParaExibicao(DateTime.UtcNow));

    /// <summary>Dentro do período configurado. <b>Sem janela (null/null) = sempre</b> — a credencial é
    /// usável o dia todo; a restrição só vale quando o operador configura um período.</summary>
    internal static bool DentroDaJanela(TimeOnly? inicio, TimeOnly? fim, TimeOnly agora)
    {
        if (inicio is not { } i || fim is not { } f) return true;
        return i <= f ? agora >= i && agora < f : agora >= i || agora < f;
    }

    /// <summary>Lê base/acesso e o período de funcionamento CONFIGURADO (janelaInicio/janelaFim). Janela
    /// ausente → (null, null) = roda a qualquer hora.</summary>
    private static (string? BaseUrl, string? AcessoId, TimeOnly? Inicio, TimeOnly? Fim) LerParametros(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return (null, null, null, null);
        try
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            string? Texto(string k) =>
                r.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String && v.GetString() is { Length: > 0 } s ? s : null;
            TimeOnly? Hora(string k) => TimeOnly.TryParseExact(Texto(k), "HH:mm", out var t) ? t : null;
            return (Texto("baseUrl"), Texto("acessoId"), Hora("janelaInicio"), Hora("janelaFim"));
        }
        catch (JsonException)
        {
            return (null, null, null, null);
        }
    }
}

/// <summary>
/// Acorda de 15 em 15 minutos; só entra na janela da madrugada, quando a credencial do e-SUS está
/// ativa e há CPF de executante ainda não conciliado — fora disso não faz nem login no PEC.
/// </summary>
public sealed class ReconciliacaoProfissionalEsusWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<ReconciliacaoProfissionalEsusWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromMinutes(3), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IReconciliacaoProfissionalEsusService>()
                    .ExecutarSeNaJanelaAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha na conciliação de profissionais (BPA) pelo e-SUS — tenta na próxima volta.");
            }

            try { await Task.Delay(Intervalo, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }
}
