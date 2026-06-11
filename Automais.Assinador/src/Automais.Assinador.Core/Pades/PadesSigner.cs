using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using iText.Bouncycastleconnector;
using iText.Commons.Bouncycastle.Cert;
using iText.Forms.Form.Element;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Signatures;

namespace Automais.Assinador.Core.Pades;

/// <summary>
/// Implementação PAdES com iText (deferred signing em 2 chamadas HTTP).
///
/// Preparar: cria o placeholder de assinatura (fase 1) e calcula o hash dos
/// atributos autenticados CMS (o que a chave precisa assinar). O PDF preparado +
/// o digest do documento + a cadeia voltam serializados no <c>transferState</c>.
/// Concluir: remonta o PKCS7 com a assinatura crua e injeta o CMS no placeholder.
///
/// O carimbo visual é o appearance do campo, em posição fixa no rodapé.
/// </summary>
public sealed class PadesSigner : IPadesSigner
{
    private const string FieldName = "AssinaturaICPBrasil";
    private const string HashAlgo = "SHA256";
    private const string FormatoPades = "PAdES_AD_RB";

    private static readonly iText.Commons.Bouncycastle.IBouncyCastleFactory BcFactory =
        BouncyCastleFactoryCreator.GetFactory();

    public PreparacaoResultado Preparar(PreparacaoRequisicao requisicao)
    {
        var chain = ConverterCadeia(requisicao.CadeiaCertificado);

        // Fase 1: reserva o placeholder + carimbo visual fixo.
        byte[] preparado;
        using (var entrada = new MemoryStream(requisicao.Pdf))
        using (var saida = new MemoryStream())
        {
            var reader = new PdfReader(entrada);
            var signer = new PdfSigner(reader, saida, new StampingProperties());

            var props = new SignerProperties()
                .SetFieldName(FieldName)
                .SetPageNumber(1)
                .SetPageRect(new Rectangle(36, 36, 240, 64))
                .SetSignatureAppearance(MontarAppearance(requisicao.Visual));
            signer.SetSignerProperties(props);

            // Subfilter PAdES (ETSI.CAdES.detached) — sem constante em PdfName.
            IExternalSignatureContainer placeholder =
                new ExternalBlankSignatureContainer(PdfName.Adobe_PPKLite, new PdfName("ETSI.CAdES.detached"));
            signer.SignExternalContainer(placeholder, 16384);
            preparado = saida.ToArray();
        }

        // Calcula o hash dos atributos autenticados (o que será assinado pela chave).
        var captura = new ContainerCaptura(chain);
        PdfSigner.SignDeferred(new PdfReader(new MemoryStream(preparado)), FieldName, Stream.Null, captura);

        var estado = new EstadoTransferencia(
            preparado,
            captura.DocumentDigest!,
            [.. requisicao.CadeiaCertificado],
            FieldName);

        return new PreparacaoResultado(captura.ToSignHash!, HashAlgo, Serializar(estado));
    }

    public ConclusaoResultado Concluir(ConclusaoRequisicao requisicao)
    {
        var estado = Desserializar(requisicao.TransferState);
        var chain = ConverterCadeia(estado.Cadeia);

        var injetor = new ContainerInjecao(chain, estado.DocDigest, requisicao.RawSignature);

        byte[] assinado;
        using (var saida = new MemoryStream())
        {
            PdfSigner.SignDeferred(new PdfReader(new MemoryStream(estado.PreparedPdf)), estado.FieldName, saida, injetor);
            assinado = saida.ToArray();
        }

        var (titular, emissor, cpf) = ExtrairIdentidade(estado.Cadeia[0]);
        return new ConclusaoResultado(assinado, FormatoPades, titular, emissor, cpf);
    }

    // ----------------- Containers iText -----------------

    /// <summary>Fase de cálculo: deriva o digest do documento e o hash dos atributos assinados.</summary>
    private sealed class ContainerCaptura(IX509Certificate[] chain) : IExternalSignatureContainer
    {
        public byte[]? DocumentDigest { get; private set; }
        public byte[]? ToSignHash { get; private set; }

        public byte[] Sign(Stream data)
        {
            DocumentDigest = SHA256.HashData(data);
            var sgn = new PdfPKCS7(null, chain, HashAlgo, false);
            var atributos = sgn.GetAuthenticatedAttributeBytes(
                DocumentDigest, PdfSigner.CryptoStandard.CADES, null, null);
            ToSignHash = SHA256.HashData(atributos);
            return [];
        }

        public void ModifySigningDictionary(PdfDictionary signDic) { }
    }

    /// <summary>Fase de injeção: remonta o CMS com a assinatura crua e devolve o container.</summary>
    private sealed class ContainerInjecao(IX509Certificate[] chain, byte[] docDigest, byte[] rawSignature)
        : IExternalSignatureContainer
    {
        public byte[] Sign(Stream data)
        {
            // Hardening: o ByteRange recém-reaberto tem que produzir o mesmo digest
            // capturado na preparação. Se divergir (re-stamp/troca de versão do iText),
            // o messageDigest assinado não bateria com o documento — falha explícita
            // em vez de gerar um PDF que abre mas é criptograficamente inválido.
            var atual = SHA256.HashData(data);
            if (!atual.SequenceEqual(docDigest))
                throw new InvalidOperationException(
                    "assinatura.digest_diverge: o documento na injeção diverge do capturado na preparação.");

            var sgn = new PdfPKCS7(null, chain, HashAlgo, false);
            sgn.GetAuthenticatedAttributeBytes(docDigest, PdfSigner.CryptoStandard.CADES, null, null);
            sgn.SetExternalSignatureValue(rawSignature, null, "RSA");
            return sgn.GetEncodedPKCS7(docDigest, PdfSigner.CryptoStandard.CADES, null, null, null);
        }

        public void ModifySigningDictionary(PdfDictionary signDic) { }
    }

    // ----------------- Helpers -----------------

    private static SignatureFieldAppearance MontarAppearance(CarimboVisual v)
    {
        var rqe = string.IsNullOrWhiteSpace(v.Rqe) ? string.Empty : $" — RQE {v.Rqe}";
        var texto = $"Dr(a). {v.NomeMedico}\nCRM {v.UfCrm}/{v.Crm}{rqe}\n{v.TextoRodape}";
        return new SignatureFieldAppearance(FieldName).SetContent(texto);
    }

    private static IX509Certificate[] ConverterCadeia(IReadOnlyList<byte[]> der) =>
        [.. der.Select(b => BcFactory.CreateX509Certificate(new MemoryStream(b)))];

    private static (string? Titular, string? Emissor, string? Cpf) ExtrairIdentidade(byte[] certDer)
    {
        using var cert = X509CertificateLoader.LoadCertificate(certDer);
        var titular = cert.GetNameInfo(X509NameType.SimpleName, forIssuer: false);
        var emissor = cert.GetNameInfo(X509NameType.SimpleName, forIssuer: true);
        return (titular, emissor, ExtrairCpf(cert));
    }

    /// <summary>
    /// CPF do titular ICP-Brasil: primário = OtherName OID 2.16.76.1.3.1 do
    /// SubjectAltName (DOC-ICP-04: nascimento[8] + CPF[11] + …). Fallback: dígitos
    /// no CN no formato "NOME:CPF". Mesma régua do agente local (Program.cs).
    /// </summary>
    private static string? ExtrairCpf(X509Certificate2 cert)
    {
        var san = cert.Extensions.FirstOrDefault(e => e.Oid?.Value == "2.5.29.17");
        if (san is not null)
        {
            var doSan = CpfDoSan(san.RawData);
            if (doSan is not null) return doSan;
        }

        var cn = cert.GetNameInfo(X509NameType.SimpleName, forIssuer: false) ?? string.Empty;
        var idx = cn.LastIndexOf(':');
        if (idx >= 0 && idx < cn.Length - 1)
        {
            var sufixo = new string([.. cn[(idx + 1)..].Where(char.IsDigit)]);
            if (sufixo.Length >= 11) return sufixo[..11];
        }
        return null;
    }

    private static string? CpfDoSan(byte[] sanRaw)
    {
        try
        {
            var outer = new AsnReader(sanRaw, AsnEncodingRules.DER).ReadSequence();
            while (outer.HasData)
            {
                var tag = outer.PeekTag();
                if (tag is { TagClass: TagClass.ContextSpecific, TagValue: 0 })
                {
                    var other = outer.ReadSequence(tag);
                    var oid = other.ReadObjectIdentifier();
                    if (oid == "2.16.76.1.3.1")
                    {
                        var valueExplicit = other.ReadSequence(new Asn1Tag(TagClass.ContextSpecific, 0, isConstructed: true));
                        var texto = LerTexto(valueExplicit);
                        var digitos = new string([.. (texto ?? string.Empty).Where(char.IsDigit)]);
                        // DOC-ICP-04 pessoa física: nascimento(8) + CPF(11) + …
                        if (digitos.Length >= 19) return digitos.Substring(8, 11);
                        if (digitos.Length == 11) return digitos;
                        return null;
                    }
                }
                else
                {
                    outer.ReadEncodedValue();
                }
            }
        }
        catch { /* SAN malformado: sem CPF por aqui */ }
        return null;
    }

    private static string? LerTexto(AsnReader r)
    {
        try
        {
            var tag = r.PeekTag();
            if (tag.TagClass == TagClass.Universal)
            {
                var u = (UniversalTagNumber)tag.TagValue;
                if (u == UniversalTagNumber.OctetString)
                    return Encoding.ASCII.GetString(r.ReadOctetString());
                if (u is UniversalTagNumber.UTF8String or UniversalTagNumber.PrintableString
                    or UniversalTagNumber.IA5String or UniversalTagNumber.T61String)
                    return r.ReadCharacterString(u);
            }
            return Encoding.ASCII.GetString(r.ReadEncodedValue().ToArray());
        }
        catch { return null; }
    }

    private static byte[] Serializar(EstadoTransferencia e) =>
        JsonSerializer.SerializeToUtf8Bytes(e);

    private static EstadoTransferencia Desserializar(byte[] bytes) =>
        JsonSerializer.Deserialize<EstadoTransferencia>(bytes)
        ?? throw new InvalidOperationException("transferState inválido.");

    private sealed record EstadoTransferencia(
        byte[] PreparedPdf,
        byte[] DocDigest,
        List<byte[]> Cadeia,
        string FieldName);
}
