using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;

namespace SMSMais.Core.Integracoes.KlinikosWeb.Varredura;

/// <summary>
/// Leitura "como usuário" do Klinikos (ADITIVO — não substitui a estratégia SQL). Monta a
/// ESPINHA de um dia a partir dos relatórios leves (407 + 667) e o CID em lote (526), sem
/// escrever no hub. É a base compartilhada do dry-run, do teste de paridade e da escrita.
/// </summary>
public interface IKlinikosWebSincronizacaoService
{
    /// <summary>Monta a espinha de um dia (JOIN 407×667 por <c>spa_codigo</c>), sem gravar.</summary>
    Task<IReadOnlyList<EspinhaRegistro>> MontarEspinhaAsync(string slug, DateOnly dia, CancellationToken ct);

    /// <summary>CID por boletim do dia, do relatório 526 (texto do diagnóstico), sem gravar.</summary>
    Task<IReadOnlyDictionary<string, string>> PuxarCidPorBoletimAsync(string slug, DateOnly dia, CancellationToken ct);

    /// <summary>Contagens da espinha de um dia (sem PII).</summary>
    Task<ResumoDryRun> DryRunEspinhaAsync(string slug, DateOnly dia, CancellationToken ct);
}

public sealed class KlinikosWebSincronizacaoService(
    IKlinikosWebSessao sessao,
    IKlinikosWebFonteResolver resolver,
    ILogger<KlinikosWebSincronizacaoService> logger) : IKlinikosWebSincronizacaoService
{
    public async Task<IReadOnlyList<EspinhaRegistro>> MontarEspinhaAsync(
        string slug, DateOnly dia, CancellationToken ct)
    {
        var fonte = await resolver.ResolverAsync(slug, ct);
        // GUARDA DE VERSÃO: detecta a versão da tela e só segue se bater com a declarada na fonte
        // (e houver perfil). Divergência/build novo bloqueiam — nunca roda o perfil errado.
        await GarantirVersaoAsync(slug, fonte.Build, ct);
        var perfil = KlinikosBuildCatalogo.Perfil(fonte.Build)!; // a guarda garantiu que existe
        var d = dia.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

        return perfil.Estrategia == KlinikosBuildCatalogo.Estrategia.Upa2025
            ? await MontarEspinha2025Async(slug, fonte.Instancia, d, ct)
            : await MontarEspinha2024Async(slug, fonte.Instancia, d, ct);
    }

    /// <summary>Espinha do Conde 2024: JOIN 407×667 (chegada+cor+clínica+cadastro); CID via 526.</summary>
    private async Task<IReadOnlyList<EspinhaRegistro>> MontarEspinha2024Async(
        string slug, KlinikosInstancia instancia, string d, CancellationToken ct)
    {
        // 407 — registrados no dia (boletim + prontuário + paciente + clínica).
        var xls407 = await sessao.BaixarRelatorioXlsAsync(
            slug,
            $"rptviewXls.aspx?parRel=407&parNum=4&par1={instancia.UnidCodigo}&par2={d}&par3={d}&par4=1&Modulo=UPA",
            ct);
        var boletins = KlinikosRelatorioParser.Ler407(xls407)
            .GroupBy(b => b.SpaCodigo).ToDictionary(g => g.Key, g => g.Last(), StringComparer.Ordinal);

        // 667 — nominal por classificação (chegada + cor).
        var xls667 = await sessao.BaixarRelatorioXlsAsync(
            slug,
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

    /// <summary>
    /// Espinha da UPA/Santa Rita 2025: relatório 751 (Nominal) — cadastro + atendimento. Nesta build
    /// a CHEGADA e a COR NÃO vêm por relatório (ficam na fila viva / deep), então saem nulas aqui;
    /// o CID vem do 752 (ver <see cref="PuxarCidPorBoletimAsync"/>). Só boletins ATENDIDOS.
    /// </summary>
    private async Task<IReadOnlyList<EspinhaRegistro>> MontarEspinha2025Async(
        string slug, KlinikosInstancia instancia, string d, CancellationToken ct)
    {
        var xls751 = await sessao.BaixarRelatorioXlsAsync(
            slug,
            $"rptviewXls.aspx?parNomeMaquina=&parRel=751&parNum=8&par1={instancia.UnidCodigo}"
            + $"&par2={d}&par3={d}&par4=5&par5=&par6=0023&par7=&par8=1",
            ct);
        var boletins = KlinikosRelatorioParser.Ler751(xls751)
            .GroupBy(b => b.SpaCodigo).ToDictionary(g => g.Key, g => g.Last(), StringComparer.Ordinal);

        var espinha = new List<EspinhaRegistro>(boletins.Count);
        foreach (var b in boletins.Values)
        {
            espinha.Add(new EspinhaRegistro(
                b.SpaCodigo, Chegada: null, Cor: null, b.Clinica,
                b.Paciente, b.Prontuario, b.NascimentoIdade, Origem: null,
                Em407: true, Em667: false));
        }
        return espinha;
    }

    public async Task<IReadOnlyDictionary<string, string>> PuxarCidPorBoletimAsync(
        string slug, DateOnly dia, CancellationToken ct)
    {
        var fonte = await resolver.ResolverAsync(slug, ct);
        await GarantirVersaoAsync(slug, fonte.Build, ct);
        var perfil = KlinikosBuildCatalogo.Perfil(fonte.Build)!;
        var instancia = fonte.Instancia;
        var d = dia.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var ehUpa2025 = perfil.Estrategia == KlinikosBuildCatalogo.Estrategia.Upa2025;

        var query = ehUpa2025
            // 752 — Por CID (build 2025): CID por boletim, em COLUNA (texto, sem código).
            ? $"rptviewXls.aspx?parNomeMaquina=&parRel=752&parNum=8&par1={instancia.UnidCodigo}"
              + $"&par2={d}&par3={d}&par4=5&par5=&par6=0023&par7=&par8=2"
            // 526 — atendidos por profissional (CID em sub-linha). Pesado (~50s): uso pontual/noturno.
            : $"rptviewXls.aspx?parNomeMaquina=&parRel=526&parNum=5&par1={instancia.UnidCodigo}&par2={d}&par3={d}&par4=5&par5=";

        var xls = await sessao.BaixarRelatorioXlsAsync(slug, query, ct);
        var atendimentos = ehUpa2025
            ? KlinikosRelatorioParser.Ler752(xls)
            : KlinikosRelatorioParser.Ler526(xls);

        var mapa = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var a in atendimentos)
        {
            if (!string.IsNullOrWhiteSpace(a.CidTexto)) mapa[a.SpaCodigo] = a.CidTexto!;
        }
        return mapa;
    }

    public async Task<ResumoDryRun> DryRunEspinhaAsync(string slug, DateOnly dia, CancellationToken ct)
    {
        var relogio = Stopwatch.StartNew();
        var espinha = await MontarEspinhaAsync(slug, dia, ct);
        relogio.Stop();

        var porCor = espinha
            .GroupBy(e => e.Cor ?? "(sem cor)", StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        var resumo = new ResumoDryRun(
            slug, dia,
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
            slug, dia, resumo.Boletins, resumo.ComCor, resumo.ComChegada,
            resumo.EmAmbos, resumo.SoEm407, resumo.SoEm667, resumo.Segundos);

        return resumo;
    }

    /// <summary>
    /// Guarda de versão: lê a <c>lblVersao</c> da tela (Default.aspx) e só deixa sincronizar se a
    /// versão DETECTADA bater com a DECLARADA na fonte e houver perfil no catálogo. Divergência ou
    /// build novo BLOQUEIAM — para nunca rodar o perfil de um build contra um sistema atualizado.
    /// </summary>
    private async Task GarantirVersaoAsync(string slug, string buildDeclarado, CancellationToken ct)
    {
        var home = await sessao.AbrirTelaAsync(slug, "Default.aspx", ct);
        var detectada = KlinikosBuildCatalogo.ExtrairVersao(home);
        var declarada = KlinikosBuildCatalogo.Normalizar(buildDeclarado);

        if (string.IsNullOrEmpty(detectada))
        {
            // Não achou a lblVersao: a tela mudou de layout — não confirma a versão, então bloqueia.
            throw new KlinikosBuildDivergenteException(slug, declarada, "(não detectada)");
        }
        if (!KlinikosBuildCatalogo.Reconhece(detectada))
        {
            throw new KlinikosBuildDesconhecidoException(slug, detectada);
        }
        if (!string.Equals(detectada, declarada, StringComparison.OrdinalIgnoreCase))
        {
            throw new KlinikosBuildDivergenteException(slug, declarada, detectada);
        }

        logger.LogInformation("Klinikos versão OK {Prov}: {Versao} (declarada = detectada).", slug, detectada);
    }
}
