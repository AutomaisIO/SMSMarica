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
    /// TETO da vida da credencial emitida (segundos) = da sessão VIDaaS do médico (ADR-0061
    /// §2.1). A vida pedida ao provedor é o que resta da sessão de login no SMSMais, limitado a
    /// este teto — a sessão VIDaaS nunca sobrevive ao login. Enquanto ela vale, o médico assina
    /// laudo após laudo sem voltar ao aplicativo, sempre pelo próprio clique em Assinar e com a
    /// conferência de cada documento. A API recusa acima de 168 h; valores fora de
    /// [<see cref="CredencialVidaMinimaSegundos"/>, <see cref="CredencialVidaMaximaSegundos"/>]
    /// são trazidos para dentro.
    /// </summary>
    public int CredencialVidaSegundos { get; set; } = 12 * 3600;

    public const int CredencialVidaMinimaSegundos = 300;

    /// <summary>
    /// Vida da credencial quando o médico NÃO mantém a autorização: só o bastante para o laudo
    /// da vez (a assinatura acontece segundos depois do retorno). 15 min é o valor que rodou em
    /// produção antes da sessão.
    /// </summary>
    public const int CredencialVidaUmLaudoSegundos = 900;
    public const int CredencialVidaMaximaSegundos = 168 * 3600;

    /// <summary><see cref="CredencialVidaSegundos"/> dentro do que a API aceita.</summary>
    public int CredencialVidaEfetivaSegundos =>
        Math.Clamp(CredencialVidaSegundos, CredencialVidaMinimaSegundos, CredencialVidaMaximaSegundos);

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
