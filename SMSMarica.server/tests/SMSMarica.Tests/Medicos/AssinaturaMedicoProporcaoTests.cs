using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SMSMarica.Core.Medicos.Assinatura;
using SMSMarica.Data.Entities.Enums;
using QuestDocument = QuestPDF.Fluent.Document;

namespace SMSMarica.Tests.Medicos;

/// <summary>
/// Leitura de dimensão (PNG/JPEG) e validação da proporção (1:1 / 2:1) da rubrica.
/// </summary>
public class AssinaturaMedicoProporcaoTests
{
    static AssinaturaMedicoProporcaoTests() => QuestPDF.Settings.License = LicenseType.Community;

    [Theory]
    [InlineData(800, 800)]
    [InlineData(800, 400)]
    [InlineData(600, 200)]
    public void DimensaoImagem_le_png(int w, int h)
    {
        var png = Png(w, h);
        DimensaoImagem.Ler(png).Should().Be((w, h));
    }

    [Fact]
    public void DimensaoImagem_le_jpeg()
    {
        var jpeg = Jpeg(800, 400);
        DimensaoImagem.Ler(jpeg).Should().Be((800, 400));
    }

    [Fact]
    public void DimensaoImagem_retorna_null_para_lixo()
    {
        DimensaoImagem.Ler([1, 2, 3, 4, 5]).Should().BeNull();
        DimensaoImagem.Ler(System.Text.Encoding.ASCII.GetBytes("não é imagem nenhuma...."))
            .Should().BeNull();
    }

    [Theory]
    [InlineData(800, 800, FormatoAssinaturaMedico.Quadrada, true)]   // 1:1 ✓
    [InlineData(800, 800, FormatoAssinaturaMedico.Horizontal, false)] // 1:1 declarado 2:1 ✗
    [InlineData(800, 400, FormatoAssinaturaMedico.Horizontal, true)] // 2:1 ✓
    [InlineData(800, 400, FormatoAssinaturaMedico.Quadrada, false)]  // 2:1 declarado 1:1 ✗
    [InlineData(800, 420, FormatoAssinaturaMedico.Horizontal, true)] // 1,90 dentro de ±5% ✓
    [InlineData(800, 460, FormatoAssinaturaMedico.Horizontal, false)] // 1,74 fora de ±5% ✗
    [InlineData(820, 800, FormatoAssinaturaMedico.Quadrada, true)]   // 1,025 dentro de ±5% ✓
    public void Validator_checa_proporcao(int w, int h, FormatoAssinaturaMedico formato, bool esperaValido)
    {
        var req = new SalvarAssinaturaMedicoRequest(
            ImagemBase64: Convert.ToBase64String(Png(w, h)),
            ContentType: "image/png",
            Formato: formato);

        var resultado = new SalvarAssinaturaMedicoValidator().Validate(req);

        var temErroProporcao = resultado.Errors.Any(e => e.ErrorMessage.Contains("proporção"));
        temErroProporcao.Should().Be(!esperaValido);
    }

    [Fact]
    public void Validator_aceita_data_url()
    {
        var req = new SalvarAssinaturaMedicoRequest(
            ImagemBase64: "data:image/png;base64," + Convert.ToBase64String(Png(800, 400)),
            ContentType: "image/png",
            Formato: FormatoAssinaturaMedico.Horizontal);

        new SalvarAssinaturaMedicoValidator().Validate(req).IsValid.Should().BeTrue();
    }

    // ---- helpers ----

    private static byte[] Png(int w, int h) => Imagem(w, h, ImageFormat.Png);
    private static byte[] Jpeg(int w, int h) => Imagem(w, h, ImageFormat.Jpeg);

    private static byte[] Imagem(int w, int h, ImageFormat formato) =>
        QuestDocument.Create(c => c.Page(p =>
        {
            p.Size(w, h, Unit.Point);
            p.Margin(0);
            p.PageColor(Colors.Blue.Lighten3);
            p.Content().AlignMiddle().AlignCenter().Text("rubrica").FontSize(28);
        }))
        .GenerateImages(new ImageGenerationSettings { ImageFormat = formato, RasterDpi = 72 })
        .First();
}
