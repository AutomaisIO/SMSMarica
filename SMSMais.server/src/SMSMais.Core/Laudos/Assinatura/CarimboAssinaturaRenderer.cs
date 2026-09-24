using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Laudos.Assinatura;

/// <summary>Dados para compor o carimbo visual da assinatura (rubrica + identificação).</summary>
public sealed record CarimboDados(
    byte[]? Rubrica,
    FormatoAssinaturaMedico Formato,
    string Nome,
    string Crm,
    string UfCrm,
    string? Rqe,
    DateTime DataAssinatura,
    // false = médico sem certificado (ADR-0061): o carimbo não pode dizer "Assinado em".
    bool AssinaturaDigital = true);

public interface ICarimboAssinaturaRenderer
{
    /// <summary>Compõe o carimbo como PNG 800×800 (o "quadrado virtual").</summary>
    byte[] Renderizar(CarimboDados dados);
}

/// <summary>
/// Compõe o carimbo da assinatura num quadrado virtual 800×800 (QuestPDF → PNG
/// com fundo TRANSPARENTE, para não cobrir o documento atrás).
/// Z-order: a RUBRICA vai ao FUNDO e os dados do médico (nome/CRM/RQE/data da
/// assinatura) vão POR CIMA, com fundo transparente (sem caixa branca). FitArea
/// preserva a proporção:
/// - Formato 1:1 (Quadrada): rubrica preenche o quadrado; texto sobreposto embaixo.
/// - Formato 2:1 (Horizontal): rubrica vira faixa ancorada no topo; texto embaixo.
/// - Sem rubrica: só os dados, na metade de baixo (mantido por robustez; o gate
///   de assinatura já exige rubrica antes de chegar aqui).
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
        // RQE só quando informado; UF antes do número (mesmo padrão do CRM).
        var rqe = string.IsNullOrWhiteSpace(dados.Rqe) ? null : $"RQE {dados.UfCrm}/{dados.Rqe!.Trim()}";
        // Data/hora já em horário de Brasília (o chamador converte via FusoBrasilia).
        var data = dados.DataAssinatura.ToString(
            dados.AssinaturaDigital ? "'Assinado em 'dd/MM/yyyy' às 'HH:mm" : "'Emitido em 'dd/MM/yyyy' às 'HH:mm",
            System.Globalization.CultureInfo.InvariantCulture);

        var documento = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(Lado, Lado, Unit.Point); // 1pt = 1px a 72 DPI → 800×800px
                page.Margin(0);
                page.PageColor(Colors.Transparent); // PNG transparente: não cobre o documento atrás
                page.DefaultTextStyle(t => t.FontColor("#111111"));

                page.Content().Layers(layers =>
                {
                    // Âncora do quadrado virtual completo.
                    layers.PrimaryLayer().Extend();

                    // CAMADA DE BAIXO (fundo): a rubrica. FitArea preserva a proporção
                    // (1:1 preenche o quadrado; 2:1 vira faixa, ancorada no topo) sem
                    // distorcer. Como a imagem PODE ter fundo opaco, ela vai por baixo.
                    if (rubrica is not null)
                    {
                        layers.Layer().Element(c =>
                        {
                            var alvo = horizontal ? c.AlignTop().Height(Metade) : c;
                            alvo.Image(rubrica).FitArea();
                        });
                    }

                    // CAMADA DE CIMA (frente): identificação do médico SEMPRE por cima
                    // da rubrica, com fundo 100% TRANSPARENTE (sem caixa branca) — a
                    // assinatura aparece inteira atrás do texto. Metade de baixo.
                    layers.Layer()
                        .AlignBottom()
                        .Height(Metade)
                        .AlignMiddle()
                        .AlignCenter()
                        .PaddingVertical(16)
                        .PaddingHorizontal(28)
                        .Column(col =>
                        {
                            col.Spacing(7);
                            col.Item().AlignCenter().Text(nome).FontSize(46).Bold();
                            col.Item().AlignCenter().Text(crm).FontSize(38);
                            if (rqe is not null)
                            {
                                col.Item().AlignCenter().Text(rqe).FontSize(34);
                            }
                            col.Item().AlignCenter().Text(data).FontSize(28);
                        });
                });
            });
        });

        return documento
            .GenerateImages(new ImageGenerationSettings { ImageFormat = ImageFormat.Png, RasterDpi = 72 })
            .First();
    }
}
