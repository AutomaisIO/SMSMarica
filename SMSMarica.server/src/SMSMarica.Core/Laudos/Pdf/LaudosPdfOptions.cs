namespace SMSMarica.Core.Laudos.Pdf;

/// <summary>
/// Configuração estática do PDF de laudo, carregada de
/// <c>appsettings.json:Laudos:Pdf</c>. Permite alterar cabeçalho/rodapé sem
/// recompilar.
/// </summary>
public sealed class LaudosPdfOptions
{
    public const string SecaoConfig = "Laudos:Pdf";

    public string TituloInstituicao { get; set; } = "Secretaria Municipal de Saúde de Maricá";
    public string SubtituloServico { get; set; } = "Serviço de Diagnóstico por Imagem";
    public string EnderecoLinha1 { get; set; } = string.Empty;
    public string EnderecoLinha2 { get; set; } = string.Empty;
    public string Telefone { get; set; } = string.Empty;

    /// <summary>Caminho absoluto da imagem do logotipo (PNG). Opcional.</summary>
    public string? CaminhoLogo { get; set; }

    public string TarjaRodape { get; set; } =
        "DOCUMENTO ELETRÔNICO — Sem assinatura digital ICP-Brasil. " +
        "Validade jurídica limitada (Resolução CFM 2.299/2021).";

    /// <summary>Fuso para exibição do horário de emissão (default: UTC-3).</summary>
    public int OffsetHorasParaExibicao { get; set; } = -3;
}
