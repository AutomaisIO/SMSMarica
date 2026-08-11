using FellowOakDicom;

namespace SMSMarica.Tests.Pacs;

/// <summary>
/// Fidelidade da reescrita de identidade: trocar quem é o paciente <b>não pode</b> tocar na imagem.
///
/// <para>Este projeto já foi mordido por transcodificação silenciosa (o US Mindray com RLE+YBR
/// planar que quebrou o visualizador). A reescrita abre o objeto com fo-dicom e regrava; se essa
/// ida e volta recomprimir, mudar transfer syntax ou mexer no <c>PixelData</c>, o exame corrigido
/// deixa de ser o exame adquirido — e ninguém perceberia até um laudo sair errado.</para>
///
/// <para><b>Precisa de uma instância REAL.</b> Aponte <c>SMSMARICA_DICOM_AMOSTRA</c> para um
/// arquivo .dcm (ex.: uma instância de mamografia baixada do PACS por WADO). Sem a variável, o
/// caso é ignorado: a amostra tem dado de paciente e não entra no repositório.</para>
/// </summary>
public class ReescritaFidelidadeTests
{
    private const string VarAmostra = "SMSMARICA_DICOM_AMOSTRA";

    [Fact]
    public void Trocar_identidade_nao_altera_pixel_nem_transfer_syntax()
    {
        var caminho = Environment.GetEnvironmentVariable(VarAmostra);
        if (string.IsNullOrWhiteSpace(caminho) || !File.Exists(caminho))
            return; // sem amostra local, não há o que medir (ver o comentário da classe)

        var original = DicomFile.Open(caminho!);
        var tsOriginal = original.FileMetaInfo.TransferSyntax.UID.UID;
        var pixelsOriginais = original.Dataset.GetDicomItem<DicomElement>(DicomTag.PixelData)?.Buffer.Data
            ?? throw new InvalidOperationException("A amostra não tem PixelData.");
        var linhas = original.Dataset.GetSingleValue<ushort>(DicomTag.Rows);

        // Mesma troca que o reescritor faz: identidade e UIDs, nada mais.
        var arquivo = DicomFile.Open(caminho!);
        arquivo.Dataset.AddOrUpdate(DicomTag.StudyInstanceUID, DicomUIDGenerator.GenerateDerivedFromUUID().UID);
        arquivo.Dataset.AddOrUpdate(DicomTag.SeriesInstanceUID, DicomUIDGenerator.GenerateDerivedFromUUID().UID);
        var sopNovo = DicomUIDGenerator.GenerateDerivedFromUUID().UID;
        arquivo.Dataset.AddOrUpdate(DicomTag.SOPInstanceUID, sopNovo);
        arquivo.FileMetaInfo.MediaStorageSOPInstanceUID = DicomUID.Parse(sopNovo);
        arquivo.Dataset.AddOrUpdate(DicomTag.PatientID, "99999999999");
        arquivo.Dataset.AddOrUpdate(DicomTag.PatientName, "TESTE^FIDELIDADE");
        arquivo.Dataset.AddOrUpdate(DicomTag.AccessionNumber, "260811999");

        using var memoria = new MemoryStream();
        arquivo.Save(memoria);
        memoria.Position = 0;
        var relido = DicomFile.Open(memoria);

        // 1. O transfer syntax é o mesmo — nada foi recomprimido nem descomprimido.
        Assert.Equal(tsOriginal, relido.FileMetaInfo.TransferSyntax.UID.UID);

        // 2. Os pixels são byte a byte os mesmos.
        var pixelsRelidos = relido.Dataset.GetDicomItem<DicomElement>(DicomTag.PixelData)!.Buffer.Data;
        Assert.Equal(pixelsOriginais.Length, pixelsRelidos.Length);
        Assert.True(pixelsOriginais.AsSpan().SequenceEqual(pixelsRelidos),
            "O PixelData mudou na ida e volta — a reescrita estaria alterando a imagem.");

        // 3. A geometria continua de pé (guarda contra perda de atributo na regravação).
        Assert.Equal(linhas, relido.Dataset.GetSingleValue<ushort>(DicomTag.Rows));

        // 4. E a identidade realmente trocou.
        Assert.Equal("99999999999", relido.Dataset.GetSingleValue<string>(DicomTag.PatientID));
        Assert.Equal("260811999", relido.Dataset.GetSingleValue<string>(DicomTag.AccessionNumber));
    }
}
