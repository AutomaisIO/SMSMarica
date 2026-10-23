using Microsoft.Extensions.Logging;

using SMSMais.Core.Regulacao.EnvioSer;

namespace SMSMais.Core.Regulacao.Anexos;

/// <summary>O arquivo como vai ser guardado, depois da compactação de entrada.</summary>
/// <param name="Nome">O nome, com a extensão trocada quando a foto virou JPEG.</param>
/// <param name="TamanhoOriginal">Quanto o arquivo tinha ao chegar — a prévia mostra "de X para Y".</param>
public sealed record AnexoCompactado(string Nome, string ContentType, byte[] Conteudo, long TamanhoOriginal);

/// <summary>
/// Compacta o anexo da regulação <b>na entrada</b>, antes de guardar (pedido do Bernardo, 23/10/2026):
/// a mesma compressão do envio ao SER/SERNIT, para poupar armazenamento — o original não é guardado.
/// Quem anexa vê a prévia do resultado e só então aceita.
/// </summary>
public interface ICompactadorAnexo
{
    Task<AnexoCompactado> CompactarAsync(string nome, string contentType, byte[] conteudo, CancellationToken ct);
}

/// <summary>
/// Um degrau fixo, folgado para leitura, e não a escada até um limite: aqui o objetivo é não guardar
/// peso à toa. Caber no limite do destino continua sendo do envio (<see cref="AnexosParaSer"/>), que
/// só comprime de novo se o arquivo, mesmo enxuto, ainda passar dele.
///
/// <list type="bullet">
/// <item>Foto (JPEG, PNG, WebP): JPEG em <see cref="CompressorImagem.Entrada"/>, de pé.</item>
/// <item>PDF: Ghostscript em <see cref="EscadaCompressaoPdf.Entrada"/> — reduz as páginas escaneadas,
/// texto e layout ficam.</item>
/// <item>Outro tipo, ou o que não diminuiu ao menos <see cref="GanhoMinimo"/>: vai como chegou. Recomprimir
/// por 5% só perde qualidade; PDF já digital (laudo, guia impressa) costuma cair aqui.</item>
/// </list>
///
/// <para>Compactação que falha nunca bloqueia o anexo: o arquivo segue como chegou e o motivo vai para o
/// log.</para>
/// </summary>
public sealed class CompactadorAnexo(ICompressorPdf compressorPdf, ILogger<CompactadorAnexo> logger) : ICompactadorAnexo
{
    /// <summary>Só guarda a versão compactada se ela ficou pelo menos 10% menor.</summary>
    public const double GanhoMinimo = 0.10;

    public async Task<AnexoCompactado> CompactarAsync(
        string nome, string contentType, byte[] conteudo, CancellationToken ct)
    {
        var original = new AnexoCompactado(nome, contentType, conteudo, conteudo.LongLength);

        AnexoCompactado resultado;
        if (EhFoto(contentType))
        {
            var foto = CompressorImagem.CompactarNaEntrada(conteudo);
            resultado = foto is not null && (foto.Endireitada || Compensa(foto.Conteudo, conteudo))
                ? original with { Nome = Path.ChangeExtension(nome, ".jpg"), ContentType = "image/jpeg", Conteudo = foto.Conteudo }
                : original;
        }
        else if (contentType == "application/pdf")
        {
            var pdf = await compressorPdf.ComprimirNoDegrauAsync(conteudo, EscadaCompressaoPdf.Entrada, ct);
            resultado = pdf is not null && Compensa(pdf, conteudo) ? original with { Conteudo = pdf } : original;
        }
        else
        {
            resultado = original;
        }

        logger.LogInformation(
            "Anexo da regulação na entrada ({Tipo}): {Antes} bytes -> {Depois} bytes{ComoChegou}.",
            contentType, conteudo.LongLength, resultado.Conteudo.LongLength,
            ReferenceEquals(resultado, original) ? " (como chegou)" : "");
        return resultado;
    }

    private static bool EhFoto(string contentType) =>
        contentType is "image/jpeg" or "image/png" or "image/webp";

    private static bool Compensa(byte[] compactado, byte[] original) =>
        compactado.LongLength <= original.LongLength * (1 - GanhoMinimo);
}
