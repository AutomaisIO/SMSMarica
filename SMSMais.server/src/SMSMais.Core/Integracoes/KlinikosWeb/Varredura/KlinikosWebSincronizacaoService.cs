using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Integracoes.Credenciais;

namespace SMSMais.Core.Integracoes.KlinikosWeb.Varredura;

/// <summary>
/// Leitura "como usuário" do Klinikos (ADITIVO — não substitui a estratégia SQL). Monta a
/// ESPINHA de um dia a partir dos relatórios leves (407 + 667) e o CID em lote (526), sem
/// escrever no hub. É a base compartilhada do dry-run, do teste de paridade e da escrita.
/// </summary>
public interface IKlinikosWebSincronizacaoService
{
    /// <summary>Monta a espinha de um dia (JOIN 407×667 por <c>spa_codigo</c>), sem gravar.</summary>
    Task<IReadOnlyList<EspinhaRegistro>> MontarEspinhaAsync(string provedor, DateOnly dia, CancellationToken ct);

    /// <summary>CID por boletim do dia, do relatório 526 (texto do diagnóstico), sem gravar.</summary>
    Task<IReadOnlyDictionary<string, string>> PuxarCidPorBoletimAsync(string provedor, DateOnly dia, CancellationToken ct);

    /// <summary>Contagens da espinha de um dia (sem PII).</summary>
    Task<ResumoDryRun> DryRunEspinhaAsync(string provedor, DateOnly dia, CancellationToken ct);
}

public sealed class KlinikosWebSincronizacaoService(
    IKlinikosWebSessao sessao,
    IIntegracaoCredencialService credenciais,
    ILogger<KlinikosWebSincronizacaoService> logger) : IKlinikosWebSincronizacaoService
{
    public async Task<IReadOnlyList<EspinhaRegistro>> MontarEspinhaAsync(
        string provedor, DateOnly dia, CancellationToken ct)
    {
        var instancia = await ResolverInstanciaAsync(provedor, ct);
        var d = dia.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

        // 407 — registrados no dia (boletim + prontuário + paciente + clínica).
        var xls407 = await sessao.BaixarRelatorioXlsAsync(
            provedor,
            $"rptviewXls.aspx?parRel=407&parNum=4&par1={instancia.UnidCodigo}&par2={d}&par3={d}&par4=1&Modulo=UPA",
            ct);
        var boletins = KlinikosRelatorioParser.Ler407(xls407)
            .GroupBy(b => b.SpaCodigo).ToDictionary(g => g.Key, g => g.Last(), StringComparer.Ordinal);

        // 667 — nominal por classificação (chegada + cor).
        var xls667 = await sessao.BaixarRelatorioXlsAsync(
            provedor,
            $"rptviewXls.aspx?parNomeMaquina=&parRel=667&parNum=6&par1={d}&par2={d}&par3=&par4={instancia.UnidCodigo}&par5=PAR&par6=",
            ct);
        var classif = KlinikosRelatorioParser.Ler667(xls667)
            .GroupBy(c => c.SpaCodigo).ToDictionary(g => g.Key, g => g.Last(), StringComparer.Ordinal);

        var todas = new HashSet<string>(boletins.Keys, StringComparer.Ordinal);
        todas.UnionWith(classif.Keys);

        var espinha = new List<EspinhaRegistro>(todas.Count);
        foreach (var spa in todas)
        {
            boletins.TryGetValue(spa, out var b);
            classif.TryGetValue(spa, out var c);
            espinha.Add(new EspinhaRegistro(
                spa, c?.Chegada, c?.Cor, b?.Clinica, b?.Paciente, b?.Prontuario,
                b?.NascimentoIdade, c?.Origem,
                Em407: b is not null, Em667: c is not null));
        }
        return espinha;
    }

    public async Task<IReadOnlyDictionary<string, string>> PuxarCidPorBoletimAsync(
        string provedor, DateOnly dia, CancellationToken ct)
    {
        var instancia = await ResolverInstanciaAsync(provedor, ct);
        var d = dia.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

        // 526 — atendidos por profissional (CID em sub-linha). Pesado (~50s): uso pontual/noturno.
        var xls526 = await sessao.BaixarRelatorioXlsAsync(
            provedor,
            $"rptviewXls.aspx?parNomeMaquina=&parRel=526&parNum=5&par1={instancia.UnidCodigo}&par2={d}&par3={d}&par4=5&par5=",
            ct);

        var mapa = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var a in KlinikosRelatorioParser.Ler526(xls526))
        {
            if (!string.IsNullOrWhiteSpace(a.CidTexto)) mapa[a.SpaCodigo] = a.CidTexto!;
        }
        return mapa;
    }

    public async Task<ResumoDryRun> DryRunEspinhaAsync(string provedor, DateOnly dia, CancellationToken ct)
    {
        var relogio = Stopwatch.StartNew();
        var espinha = await MontarEspinhaAsync(provedor, dia, ct);
        relogio.Stop();

        var porCor = espinha
            .GroupBy(e => e.Cor ?? "(sem cor)", StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        var resumo = new ResumoDryRun(
            provedor, dia,
            Boletins: espinha.Count,
            ComCor: espinha.Count(e => e.Cor is not null),
            ComChegada: espinha.Count(e => e.Chegada is not null),
            SoEm407: espinha.Count(e => e.Em407 && !e.Em667),
            SoEm667: espinha.Count(e => !e.Em407 && e.Em667),
            EmAmbos: espinha.Count(e => e.Em407 && e.Em667),
            PorCor: porCor,
            Segundos: relogio.Elapsed.TotalSeconds);

        logger.LogInformation(
            "Klinikos dry-run espinha {Prov} {Dia}: {N} boletins ({Cor} com cor, {Cheg} com chegada; "
            + "407∩667={Ambos}, só407={So407}, só667={So667}) em {Seg:0.0}s.",
            provedor, dia, resumo.Boletins, resumo.ComCor, resumo.ComChegada,
            resumo.EmAmbos, resumo.SoEm407, resumo.SoEm667, resumo.Segundos);

        return resumo;
    }

    private async Task<KlinikosInstancia> ResolverInstanciaAsync(string provedor, CancellationToken ct)
    {
        var ctx = await credenciais.ObterContextoAsync(provedor, ct);
        return KlinikosInstancia.De(provedor, ctx.ParametrosJson);
    }
}
