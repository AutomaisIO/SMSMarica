using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.IO.Buffer;
using SMSMais.Core.Pacs;

namespace SMSMais.Tests.Pacs;

/// <summary>
/// Garante a invariante crítica da compressão do PACS: a transcodificação para
/// JPEG-LS Lossless (1.2.840.10008.1.2.4.80) é PIXEL-IDÊNTICA no roundtrip — sem
/// isso, laudos reais seriam corrompidos. Também valida que o codec nativo está
/// registrado (PacsDicomSetup) e que há ganho de tamanho.
/// </summary>
public sealed class TranscodeJpegLsTests
{
    [Fact]
    public void TranscodeJpegLsLossless_DeveSerPixelIdentico_NoRoundtrip()
    {
        // Arrange: codec nativo registrado (mesmo setup do Program.cs).
        PacsDicomSetup.Inicializar();

        const int linhas = 64;
        const int colunas = 64;
        var original = GerarPixels16Bits(linhas, colunas);
        var dataset = MontarDataset(linhas, colunas, original);

        // Act: comprime para JPEG-LS Lossless e descomprime de volta para cru.
        var comprimido = new DicomTranscoder(
            dataset.InternalTransferSyntax, DicomTransferSyntax.JPEGLSLossless)
            .Transcode(dataset);

        var descomprimido = new DicomTranscoder(
            DicomTransferSyntax.JPEGLSLossless, DicomTransferSyntax.ExplicitVRLittleEndian)
            .Transcode(comprimido);

        var frameVolta = DicomPixelData.Create(descomprimido).GetFrame(0).Data;
        var frameComprimido = DicomPixelData.Create(comprimido).GetFrame(0).Data;

        // Assert: lossless = bytes idênticos ao original; e houve compressão real.
        frameVolta.Should().Equal(original, "JPEG-LS Lossless deve preservar cada pixel");
        comprimido.InternalTransferSyntax.Should().Be(DicomTransferSyntax.JPEGLSLossless);
        frameComprimido.Length.Should().BeLessThan(original.Length, "deve haver ganho de tamanho");
    }

    [Fact]
    public void TranscodeJpeg2000Lossless_DeveSerPixelIdentico_Em12Bits()
    {
        // Espelha a mamografia real do PACS (12 bits em 16 alocados, MONOCHROME1):
        // é o formato para o qual o JPEG-LS gerava frame ilegível no browser. Garante
        // que o alvo atual (JPEG 2000 Lossless) preserva cada pixel no servidor.
        PacsDicomSetup.Inicializar();

        const int linhas = 64;
        const int colunas = 64;
        var original = GerarPixels16Bits(linhas, colunas); // valores até 4095 = 12 bits
        var dataset = MontarDataset(linhas, colunas, original, bitsStored: 12, highBit: 11);

        var comprimido = new DicomTranscoder(
            dataset.InternalTransferSyntax, DicomTransferSyntax.JPEG2000Lossless)
            .Transcode(dataset);

        var descomprimido = new DicomTranscoder(
            DicomTransferSyntax.JPEG2000Lossless, DicomTransferSyntax.ExplicitVRLittleEndian)
            .Transcode(comprimido);

        var frameVolta = DicomPixelData.Create(descomprimido).GetFrame(0).Data;

        frameVolta.Should().Equal(original, "JPEG 2000 Lossless deve preservar cada pixel");
        comprimido.InternalTransferSyntax.Should().Be(DicomTransferSyntax.JPEG2000Lossless);
    }

    /// <summary>Gera um padrão determinístico de 16 bits (little-endian).</summary>
    private static byte[] GerarPixels16Bits(int linhas, int colunas)
    {
        var bytes = new byte[linhas * colunas * 2];
        for (var i = 0; i < linhas * colunas; i++)
        {
            // Gradiente suave (bom para compressão sem aleatoriedade).
            var valor = (ushort)((i * 7) % 4096);
            bytes[i * 2] = (byte)(valor & 0xFF);
            bytes[i * 2 + 1] = (byte)((valor >> 8) & 0xFF);
        }
        return bytes;
    }

    private static DicomDataset MontarDataset(
        int linhas, int colunas, byte[] pixels, ushort bitsStored = 16, ushort highBit = 15)
    {
        var dataset = new DicomDataset
        {
            { DicomTag.SOPClassUID, DicomUID.SecondaryCaptureImageStorage },
            { DicomTag.SOPInstanceUID, DicomUIDGenerator.GenerateDerivedFromUUID() },
            { DicomTag.PhotometricInterpretation, "MONOCHROME2" },
            { DicomTag.SamplesPerPixel, (ushort)1 },
            { DicomTag.Rows, (ushort)linhas },
            { DicomTag.Columns, (ushort)colunas },
            { DicomTag.BitsAllocated, (ushort)16 },
            { DicomTag.BitsStored, bitsStored },
            { DicomTag.HighBit, highBit },
            { DicomTag.PixelRepresentation, (ushort)0 },
        };

        var pixelData = DicomPixelData.Create(dataset, newPixelData: true);
        pixelData.AddFrame(new MemoryByteBuffer(pixels));
        return dataset;
    }
}
