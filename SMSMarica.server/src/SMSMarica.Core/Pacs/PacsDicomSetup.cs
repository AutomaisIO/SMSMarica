using FellowOakDicom;
using FellowOakDicom.Imaging.NativeCodec;
using Microsoft.Extensions.DependencyInjection;

namespace SMSMarica.Core.Pacs;

/// <summary>
/// Inicialização única do fo-dicom para transcodificação JPEG-LS Lossless.
/// Replica o setup validado no spike (DicomSetupBuilder + AddFellowOakDicom +
/// AddTranscoderManager&lt;NativeTranscoderManager&gt;), que registra o provider
/// global usado por <c>new DicomTranscoder(...)</c>. Chamado uma vez do
/// <c>Program.cs</c>. Idempotente — chamadas repetidas são ignoradas.
/// </summary>
public static class PacsDicomSetup
{
    private static int _inicializado;

    /// <summary>
    /// Configura o fo-dicom com os codecs nativos. Só tem efeito na 1ª chamada.
    /// Sem isso, o transcoder não encontra o codec JPEG-LS e o Transcode lança
    /// (o que, no nosso fluxo, vira fallback seguro para o frame cru).
    /// </summary>
    public static void Inicializar()
    {
        if (System.Threading.Interlocked.Exchange(ref _inicializado, 1) == 1) return;

        new DicomSetupBuilder()
            .RegisterServices(s => s
                .AddFellowOakDicom()
                .AddTranscoderManager<NativeTranscoderManager>())
            // SkipValidation: imagens reais de equipamento (mamografia) costumam ter
            // pequenas não-conformidades DICOM. Sem isto, o transcode lança validação
            // e cai no fallback cru (perde a compressão). Espelha o spike validado.
            .SkipValidation()
            .Build();
    }
}
