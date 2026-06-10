using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Automais.Assinador.Core.Pades;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Signatures;
using Xunit;

namespace Automais.Assinador.Tests;

public class PadesSignerTests
{
    /// <summary>
    /// Valida o fluxo PAdES diferido ponta a ponta (mecânica): preparar → o "agente"
    /// assina o hash com a chave do cert autoassinado → concluir → o PDF resultante
    /// carrega uma assinatura íntegra e autêntica. (Conformidade ICP-Brasil/ITI exige
    /// certificado real e o Verificador do ITI — fora do escopo deste teste mecânico.)
    /// </summary>
    [Fact]
    public void TwoStep_AssinaPdf_AssinaturaIntegraEAutentica()
    {
        var pdf = GerarPdfDeTeste();

        using var rsa = RSA.Create(2048);
        var pedido = new CertificateRequest(
            "CN=TESTE FULANO DA SILVA:12345678909", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = pedido.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        var certDer = cert.Export(X509ContentType.Cert);

        var signer = new PadesSigner();
        var visual = new CarimboVisual("Fulano da Silva", "12345", "RJ", null, "Assinado digitalmente (teste)");

        // Passo 1 — preparar: devolve o hash a assinar.
        var preparado = signer.Preparar(new PreparacaoRequisicao(pdf, [certDer], visual));
        Assert.NotEmpty(preparado.ToSignHash);
        Assert.Equal("SHA256", preparado.AlgoritmoHash);

        // Passo 2 — o "agente" assina o hash com a chave privada (simula VIDaaS Connect).
        var rawSignature = rsa.SignHash(preparado.ToSignHash, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        // Passo 3 — concluir: injeta o CMS e devolve o PDF assinado.
        var resultado = signer.Concluir(new ConclusaoRequisicao(preparado.TransferState, rawSignature));

        Assert.True(resultado.PdfAssinado.Length > pdf.Length);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(resultado.PdfAssinado, 0, 4));
        Assert.Equal("12345678909", resultado.CpfTitular);

        // Verifica a assinatura embutida com o próprio iText.
        using var reader = new PdfReader(new MemoryStream(resultado.PdfAssinado));
        using var assinado = new PdfDocument(reader);
        var util = new SignatureUtil(assinado);
        var nomes = util.GetSignatureNames();
        Assert.Single(nomes);
        var pkcs7 = util.ReadSignatureData(nomes[0]);
        Assert.True(pkcs7.VerifySignatureIntegrityAndAuthenticity(), "A assinatura deveria ser íntegra e autêntica.");
    }

    private static byte[] GerarPdfDeTeste()
    {
        using var ms = new MemoryStream();
        using (var doc = new Document(new PdfDocument(new PdfWriter(ms))))
        {
            doc.Add(new Paragraph("Laudo de teste — assinatura PAdES diferida."));
        }
        return ms.ToArray();
    }
}
