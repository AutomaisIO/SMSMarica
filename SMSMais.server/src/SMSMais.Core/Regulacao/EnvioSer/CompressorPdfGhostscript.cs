using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SMSMais.Core.Regulacao.EnvioSer;

/// <summary>Configuração do Ghostscript (seção <c>Ghostscript</c>).</summary>
public sealed class GhostscriptOptions
{
    public const string Secao = "Ghostscript";

    /// <summary>
    /// O executável. Na VM de produção é o pacote do Ubuntu (<c>apt-get install ghostscript</c>, skill
    /// <c>operar-servidor</c>). No Windows, para testar local, aponte para o <c>gswin64c.exe</c>. Se o
    /// arquivo não existir, a compressão de PDF fica indisponível e o PDF grande é recusado como antes.
    /// </summary>
    public string Caminho { get; set; } = "/usr/bin/gs";

    /// <summary>Tempo máximo de UMA passada; estourou, o processo é morto e a compressão falha.</summary>
    public int TimeoutSegundos { get; set; } = 60;

    /// <summary>Quantas passadas rodam ao mesmo tempo no servidor (cada uma ocupa um núcleo).</summary>
    public int MaximoSimultaneos { get; set; } = 2;
}

/// <summary>
/// Comprime PDF chamando o <b>Ghostscript</b> (<c>pdfwrite</c>) como processo separado, na escada de
/// <see cref="EscadaCompressaoPdf"/>.
///
/// <para><b>Ghostscript é um interpretador e recebe arquivo de fora</b> (unidade, app do cidadão), e já
/// teve falha de execução de código por arquivo malicioso. Por isso: só entra o que começa com
/// <c>%PDF</c>; sempre <c>-dSAFER</c>; o processo nasce com o ambiente <b>vazio</b> (não herda a
/// connection string nem as chaves da API); tem prazo; roda numa pasta temporária própria, apagada no
/// fim. Argumentos vão um a um (<see cref="ProcessStartInfo.ArgumentList"/>), sem shell.</para>
///
/// <para><b>Armadilha medida:</b> por padrão o <c>pdfwrite</c> repassa o JPEG embutido sem
/// recomprimir — o <c>/ebook</c> pronto levou um PDF de 10,4 MB só a 8,5 MB. Daí o
/// <c>-dPassThroughJPEGImages=false</c> e a qualidade dada por <c>QFactor</c>.</para>
///
/// <para>O resultado fica em cache por 30 min pelo SHA-256 do PDF: a prévia comprime, e o envio, logo
/// depois, reaproveita sem rodar o Ghostscript de novo.</para>
/// </summary>
public sealed class CompressorPdfGhostscript(
    IOptions<GhostscriptOptions> opcoes,
    IMemoryCache cache,
    ILogger<CompressorPdfGhostscript> logger) : ICompressorPdf, IDisposable
{
    private readonly GhostscriptOptions _opcoes = opcoes.Value;
    private readonly SemaphoreSlim _vagas = new(Math.Max(1, opcoes.Value.MaximoSimultaneos));

    public async Task<CompressaoPdf> ComprimirAteAsync(byte[] pdf, long alvoBytes, CancellationToken ct)
    {
        if (!File.Exists(_opcoes.Caminho))
        {
            logger.LogWarning(
                "Compressão de PDF indisponível: Ghostscript não encontrado em {Caminho}. Instalar: "
                + "apt-get install -y --no-install-recommends ghostscript (skill operar-servidor).", _opcoes.Caminho);
            return CompressaoPdf.Indisponivel;
        }
        if (!EhPdf(pdf))
        {
            logger.LogWarning("Compressão de PDF recusada: o arquivo não começa com %PDF ({Bytes} bytes).", pdf.Length);
            return CompressaoPdf.Falhou;
        }

        var chave = $"regulacao:pdf-gs:{Convert.ToHexString(SHA256.HashData(pdf))}:{alvoBytes}";
        if (cache.TryGetValue(chave, out CompressaoPdf? guardada) && guardada is not null) return guardada;

        var pasta = CriarPastaTemporaria();
        var relogio = Stopwatch.StartNew();
        try
        {
            var entrada = Path.Combine(pasta, "entrada.pdf");
            await File.WriteAllBytesAsync(entrada, pdf, ct);

            var escada = await EscadaCompressaoPdf.BuscarAsync(
                EscadaCompressaoPdf.Degraus.Count,
                (i, c) => PassadaAsync(entrada, Path.Combine(pasta, $"saida-{i}.pdf"), EscadaCompressaoPdf.Degraus[i], c),
                alvoBytes, ct);

            var desfecho = escada.Desfecho switch
            {
                DesfechoCompressaoPdf.Coube => new CompressaoPdf(
                    DesfechoCompressaoPdf.Coube, escada.Conteudo, EscadaCompressaoPdf.Degraus[escada.Indice].Rotulo),
                DesfechoCompressaoPdf.NaoCoube => CompressaoPdf.NaoCoube,
                _ => CompressaoPdf.Falhou,
            };
            logger.LogInformation(
                "Compressão de PDF: {Antes} bytes -> {Desfecho} {Depois} bytes ({Degrau}) em {Passadas} passada(s), {Ms} ms.",
                pdf.Length, desfecho.Desfecho, desfecho.Conteudo?.Length, desfecho.Degrau, escada.Passadas,
                relogio.ElapsedMilliseconds);

            if (desfecho.Desfecho is DesfechoCompressaoPdf.Coube or DesfechoCompressaoPdf.NaoCoube)
                cache.Set(chave, desfecho, TimeSpan.FromMinutes(30));
            return desfecho;
        }
        finally
        {
            try { Directory.Delete(pasta, recursive: true); }
            catch (Exception ex) { logger.LogWarning(ex, "Não consegui apagar a pasta temporária {Pasta}.", pasta); }
        }
    }

    /// <summary>
    /// Os argumentos de uma passada. Imagens coloridas e em tons de cinza: reduzidas para
    /// <see cref="DegrauPdf.Dpi"/> (só as que estão acima) e regravadas em JPEG na qualidade do degrau.
    /// </summary>
    public static IReadOnlyList<string> Argumentos(DegrauPdf degrau, string entrada, string saida)
    {
        var dpi = degrau.Dpi.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var q = degrau.QFactorTexto;
        return
        [
            "-q", "-dSAFER", "-dBATCH", "-dNOPAUSE", "-dNOPROMPT",
            "-sDEVICE=pdfwrite", "-dCompatibilityLevel=1.5",
            "-dPassThroughJPEGImages=false", "-dDetectDuplicateImages=true",
            "-dDownsampleColorImages=true", "-dColorImageDownsampleType=/Bicubic", "-dColorImageDownsampleThreshold=1.0",
            "-dAutoFilterColorImages=false", "-dColorImageFilter=/DCTEncode", $"-dColorImageResolution={dpi}",
            "-dDownsampleGrayImages=true", "-dGrayImageDownsampleType=/Bicubic", "-dGrayImageDownsampleThreshold=1.0",
            "-dAutoFilterGrayImages=false", "-dGrayImageFilter=/DCTEncode", $"-dGrayImageResolution={dpi}",
            $"-sOutputFile={saida}",
            "-c",
            $"<< /ColorImageDict << /QFactor {q} /Blend 1 /HSamples [2 1 1 2] /VSamples [2 1 1 2] >> "
            + $"/GrayImageDict << /QFactor {q} /Blend 1 /HSamples [2 1 1 2] /VSamples [2 1 1 2] >> >> setdistillerparams",
            "-f", entrada,
        ];
    }

    private async Task<byte[]?> PassadaAsync(string entrada, string saida, DegrauPdf degrau, CancellationToken ct)
    {
        await _vagas.WaitAsync(ct);
        try
        {
            var psi = new ProcessStartInfo(_opcoes.Caminho)
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(entrada)!,
            };
            foreach (var a in Argumentos(degrau, entrada, saida)) psi.ArgumentList.Add(a);

            // Ambiente vazio: o Ghostscript não pode herdar a connection string nem as chaves da API.
            psi.Environment.Clear();
            if (OperatingSystem.IsWindows())
                psi.Environment["SystemRoot"] = Environment.GetEnvironmentVariable("SystemRoot") ?? @"C:\Windows";

            using var processo = Process.Start(psi);
            if (processo is null) return null;
            processo.StandardInput.Close();
            var saidaPadrao = processo.StandardOutput.ReadToEndAsync(CancellationToken.None);
            var erro = processo.StandardError.ReadToEndAsync(CancellationToken.None);

            using var prazo = CancellationTokenSource.CreateLinkedTokenSource(ct);
            prazo.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _opcoes.TimeoutSegundos)));
            try
            {
                await processo.WaitForExitAsync(prazo.Token);
            }
            catch (OperationCanceledException)
            {
                try { processo.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
                ct.ThrowIfCancellationRequested();
                logger.LogWarning("Ghostscript estourou {Segundos} s no degrau {Degrau}.", _opcoes.TimeoutSegundos, degrau.Rotulo);
                return null;
            }

            await saidaPadrao;
            var textoErro = await erro;
            if (processo.ExitCode != 0 || !File.Exists(saida))
            {
                logger.LogWarning(
                    "Ghostscript saiu com {Codigo} no degrau {Degrau}: {Erro}",
                    processo.ExitCode, degrau.Rotulo, Trecho(textoErro));
                return null;
            }

            var bytes = await File.ReadAllBytesAsync(saida, ct);
            return EhPdf(bytes) ? bytes : null;
        }
        finally
        {
            _vagas.Release();
        }
    }

    private static string CriarPastaTemporaria()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "smsmais-gs-" + Guid.NewGuid().ToString("N"));
        if (OperatingSystem.IsWindows())
            Directory.CreateDirectory(pasta);
        else
            Directory.CreateDirectory(pasta, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return pasta;
    }

    /// <summary>O cabeçalho <c>%PDF-</c> nos primeiros 1024 bytes (o que a especificação tolera).</summary>
    public static bool EhPdf(byte[] conteudo)
    {
        var inicio = Encoding.ASCII.GetString(conteudo, 0, Math.Min(conteudo.Length, 1024));
        return inicio.Contains("%PDF-", StringComparison.Ordinal);
    }

    private static string Trecho(string texto) =>
        texto.Length <= 500 ? texto.Trim() : texto[..500].Trim() + "…";

    public void Dispose() => _vagas.Dispose();
}
