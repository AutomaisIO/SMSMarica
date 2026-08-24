using QuestPDF.Infrastructure;
using SMSMarica.Core.Laudos.Assinatura;
using SMSMais.Data.Entities.Enums;
using Xunit;

namespace SMSMais.Tests.Laudos;

public class CarimboAssinaturaRendererTests
{
    static CarimboAssinaturaRendererTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private static bool EhPng(byte[] bytes) =>
        bytes.Length > 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47;

    [Fact]
    public void Renderiza_SemRubrica_GeraPng()
    {
        var renderer = new CarimboAssinaturaRenderer();
        var png = renderer.Renderizar(new CarimboDados(
            Rubrica: null, Formato: FormatoAssinaturaMedico.Horizontal,
            Nome: "Dra. Fulana de Tal", Crm: "12345", UfCrm: "RJ", Rqe: "58716",
            DataAssinatura: new DateTime(2026, 7, 10, 14, 30, 0)));

        Assert.True(EhPng(png), "A saída deve ser um PNG.");
        Assert.True(png.Length > 1000, "PNG não deveria ser trivialmente vazio.");
    }

    [Theory]
    [InlineData(FormatoAssinaturaMedico.Quadrada)]
    [InlineData(FormatoAssinaturaMedico.Horizontal)]
    public void Renderiza_ComRubrica_EmAmbosFormatos(FormatoAssinaturaMedico formato)
    {
        // Rubrica de teste: um PNG simples gerado pelo próprio renderer (sem dados).
        var rubrica = new CarimboAssinaturaRenderer().Renderizar(new CarimboDados(
            null, formato, "x", "x", "x", null, new DateTime(2026, 7, 10, 14, 30, 0)));

        var renderer = new CarimboAssinaturaRenderer();
        var png = renderer.Renderizar(new CarimboDados(
            Rubrica: rubrica, Formato: formato,
            Nome: "Dr. Beltrano", Crm: "999", UfCrm: "RJ", Rqe: null, // sem RQE
            DataAssinatura: new DateTime(2026, 7, 10, 14, 30, 0)));

        Assert.True(EhPng(png));
        Assert.True(png.Length > 1000);
    }
}
