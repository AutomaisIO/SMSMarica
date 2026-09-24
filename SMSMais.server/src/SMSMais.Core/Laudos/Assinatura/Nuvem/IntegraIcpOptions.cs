namespace SMSMais.Core.Laudos.Assinatura.Nuvem;

/// <summary>
/// Assinatura em nuvem pela API IntegraICP v3 (broker dos PSCs; canal com clearance VIDaaS),
/// seção <c>Assinatura:Nuvem</c> (ADR-0061). Sem <see cref="Canal"/> o modo Nuvem fica
/// indisponível e o painel explica o motivo em vez de tentar.
/// </summary>
public sealed class IntegraIcpOptions
{
    public const string SecaoConfig = "Assinatura:Nuvem";

    /// <summary>Base da API. Não existe homologação: este é o único ambiente.</summary>
    public string BaseUrl { get; set; } = "https://services.integraicp.com.br/";

    /// <summary>Nome do canal contratado (ex.: o domínio da instância). Vazio = modo Nuvem desligado.</summary>
    public string? Canal { get; set; }

    /// <summary>
    /// Cabeçalho de autenticação do canal, quando o contrato exigir (ex.: <c>Authorization</c>).
    /// O probe de 04/08/2026 funcionou só com o canal; por isso é opcional.
    /// </summary>
    public string? CabecalhoAutenticacao { get; set; }

    /// <summary>Valor do <see cref="CabecalhoAutenticacao"/>. Vem de variável de ambiente, nunca do repositório.</summary>
    public string? ValorAutenticacao { get; set; }

    /// <summary>
    /// URL pública da API desta instância, usada para montar o retorno da autorização
    /// (<c>{UrlPublicaApi}/assinatura/nuvem/retorno</c>). Vazio = cai em <c>Publico:BaseUrl</c>.
    /// </summary>
    public string? UrlPublicaApi { get; set; }

    /// <summary>
    /// Vida da credencial emitida (segundos). Curta de propósito: credencial viva permite ao
    /// servidor assinar em nome do médico sem novo consentimento (a API aceita até 168h).
    /// </summary>
    public int CredencialVidaSegundos { get; set; } = 900;

    /// <summary>Vida da autorização pendente (segundos) — sem ela o PSC devolve expiração imediata.</summary>
    public int AutorizacaoVidaSegundos { get; set; } = 600;

    /// <summary>Minutos que o job espera o médico aprovar no app antes de expirar.</summary>
    public int JanelaAutorizacaoMinutos { get; set; } = 10;

    /// <summary>
    /// Pacote oficial de ACs do ITI (raízes + intermediárias). A credencial devolve só o
    /// certificado do médico e a AC VALID RFB v5 não publica AIA; sem este pacote o CMS sai
    /// sem cadeia.
    /// </summary>
    public string UrlCadeiaIcpBrasil { get; set; } =
        "http://acraiz.icpbrasil.gov.br/credenciadas/CertificadosAC-ICP-Brasil/ACcompactado.zip";

    public bool Habilitado => !string.IsNullOrWhiteSpace(Canal);
}
