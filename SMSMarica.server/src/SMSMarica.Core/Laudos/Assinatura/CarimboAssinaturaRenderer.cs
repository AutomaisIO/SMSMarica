using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Laudos.Assinatura;

/// <summary>Dados para compor o carimbo visual da assinatura (rubrica + identificação).</summary>
public sealed record CarimboDados(
    byte[]? Rubrica,
    FormatoAssinaturaMedico Formato,
    string Nome,
    string Crm,
    string UfCrm,
    string? Rqe);

public interface ICarimboAssinaturaRenderer
{
    /// <summary>Compõe o carimbo como PNG 800×800 (o "quadrado virtual").</summary>
    byte[] Renderizar(CarimboDados dados);
}

/// <summary>
/// Compõe o carimbo da assinatura num quadrado virtual 800×800 (QuestPDF → PNG):
/// os dados do médico (centralizados) ficam SEMPRE na metade de baixo.
/// - Formato 2:1 (Horizontal): rubrica ocupa a metade de cima; dados na metade de baixo (sem sobreposição).
/// - Formato 1:1 (Quadrada): rubrica preenche o quadrado; dados sobrepõem a metade de baixo.
/// - Sem rubrica: só os dados, na metade de baixo.
/// RQE só aparece quando informado.
/// </summary>
public sealed class CarimboAssinaturaRenderer : ICarimboAssinaturaRenderer
{
    private const int Lado = 800;
    private const int Metade = Lado / 2;

    public byte[] Renderizar(CarimboDados dados)
    {
        var rubrica = dados.Rubrica;
        var horizontal = dados.Formato == FormatoAssinaturaMedico.Horizontal;

        var nome = dados.Nome.Trim();
        var crm = $"CRM {dados.UfCrm}/{dados.Crm}".Trim();
        var rqe = string.IsNullOrWhiteSpace(dados.Rqe) ? null : $"RQE {dados.Rqe!.Trim()}";

        var documento = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(Lado, Lado, Unit.Point); // 1pt = 1px a 72 DPI → 800×800px
                page.Margin(0);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(t => t.FontColor("#111111"));

                page.Content().Layers(layers =>
                {
                    // Âncora do quadrado virtual completo.
                    layers.PrimaryLayer().Extend();

                    // Rubrica: cheia (1:1) ou só metade de cima (2:1).
                    if (rubrica is not null)
                    {
                        layers.Layer().Element(c =>
                        {
                            var alvo = horizontal ? c.AlignTop().Height(Metade) : c;
                            alvo.Image(rubrica).FitArea();
                        });
                    }

                    // Dados do médico: metade de baixo, centralizados.
                    layers.Layer()
                        .AlignBottom()
                        .Height(Metade)
                        .PaddingHorizontal(24)
                        .AlignMiddle()
                        .Column(col =>
                        {
                            col.Spacing(6);
                            col.Item().AlignCenter().Text(nome).FontSize(34).Bold();
                            col.Item().AlignCenter().Text(crm).FontSize(28);
                            if (rqe is not null)
                            {
                                col.Item().AlignCenter().Text(rqe).FontSize(26);
                            }
                        });
                });
            });
        });

        return documento
            .GenerateImages(new ImageGenerationSettings { ImageFormat = ImageFormat.Png, RasterDpi = 72 })
            .First();
    }
}
