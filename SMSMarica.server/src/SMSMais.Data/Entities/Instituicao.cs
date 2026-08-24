namespace SMSMais.Data.Entities;

/// <summary>
/// Identidade da instituição que opera <b>esta instância</b> do sistema (singleton,
/// PK fixa <see cref="IdSingleton"/>) — ver ADR-0043.
///
/// <para>
/// O produto é entregue como <b>uma instância por município</b>: mesmo código, banco e
/// processo próprios. Tudo que distingue um cliente do outro — nome da secretaria, marca,
/// domínios, contatos legais — mora aqui, e não em código. Antes desta tabela, "Maricá"
/// estava espalhado por textos de PDF, páginas públicas, títulos e temas do front; cada
/// ocorrência era um ponto de retrabalho a cada nova prefeitura.
/// </para>
///
/// <para>
/// <b>Não confundir com <see cref="Unidade"/>.</b> Unidade é o estabelecimento de saúde
/// (tem CNES, é o eixo durável do ADR-0039 e existe às dezenas). Instituicao é o órgão
/// gestor da instância — existe exatamente uma.
/// </para>
///
/// <para>
/// Exposta sem autenticação em <c>GET /publico/instituicao</c>: o painel e os PWAs
/// precisam dela para se pintar <i>antes</i> de haver login. Por isso nada de segredo
/// entra aqui — credenciais continuam cifradas nas tabelas de integração.
/// </para>
/// </summary>
public class Instituicao
{
    /// <summary>PK fixa do registro único (padrão singleton, igual ao <see cref="LaudoConfiguracao"/>).</summary>
    public static readonly Guid IdSingleton = new("00000000-0000-0000-0000-00000000fffe");

    public Guid Id { get; set; } = IdSingleton;

    // ---- Identidade institucional ----

    /// <summary>Ente federativo por extenso (ex.: "Prefeitura Municipal de Maricá").</summary>
    public string Nome { get; set; } = string.Empty;

    /// <summary>Órgão gestor da saúde (ex.: "Secretaria Municipal de Saúde de Maricá").
    /// É o texto que encabeça laudos, declarações e páginas públicas.</summary>
    public string NomeSecretaria { get; set; } = string.Empty;

    /// <summary>Nome curto para cabeçalhos e título de aba (ex.: "Saúde Maricá").</summary>
    public string NomeCurto { get; set; } = string.Empty;

    /// <summary>Sigla do órgão (ex.: "SMS"). Opcional.</summary>
    public string? Sigla { get; set; }

    /// <summary>CNPJ do ente, só dígitos. Aparece em documentos oficiais.</summary>
    public string? Cnpj { get; set; }

    /// <summary>Código IBGE do município (7 dígitos, ex.: "3302700"). Chave de integrações
    /// federais — o SISREG já o exige em <c>sisreg_configuracao</c>.</summary>
    public string? CodigoIbge { get; set; }

    /// <summary>UF de duas letras (ex.: "RJ"). Decide quais integrações estaduais se aplicam.</summary>
    public string Uf { get; set; } = string.Empty;

    /// <summary>
    /// DDD padrão do município (ex.: 21). Usado para completar telefones digitados sem DDD
    /// na normalização de WhatsApp — antes era constante de Maricá no código.
    /// </summary>
    public int? DddPadrao { get; set; }

    public Endereco? Endereco { get; set; }

    public string? Telefone { get; set; }

    /// <summary>Canal público de contato exibido ao cidadão.</summary>
    public string? EmailContato { get; set; }

    /// <summary>Encarregado de dados (LGPD, art. 41). Citado no termo de consentimento.</summary>
    public string? EmailDpo { get; set; }

    /// <summary>
    /// Número de WhatsApp divulgado ao cidadão, formato E.164 sem "+" (ex.: "552137315313").
    /// É o número que o app mostra; o número que <i>envia</i> vive em
    /// <c>whatsapp_configuracao</c> (cifrado).
    /// </summary>
    public string? WhatsAppNumeroPublico { get; set; }

    // ---- Marca ----

    /// <summary>Logotipo principal (fundo claro). Servido por <c>GET /midias/{id}</c>.</summary>
    public Guid? LogoMidiaId { get; set; }
    public Midia? LogoMidia { get; set; }

    /// <summary>Favicon / ícone do app.</summary>
    public Guid? FaviconMidiaId { get; set; }
    public Midia? FaviconMidia { get; set; }

    /// <summary>Cor da marca em hex <c>#RRGGBB</c>. Vira a CSS var <c>--theme-primary</c>.</summary>
    public string? CorPrimaria { get; set; }

    /// <summary>Cor de apoio em hex <c>#RRGGBB</c>.</summary>
    public string? CorSecundaria { get; set; }

    /// <summary>Início do gradiente do menu/cabeçalho, hex <c>#RRGGBB</c>.</summary>
    public string? CorGradienteInicio { get; set; }

    /// <summary>Fim do gradiente do menu/cabeçalho, hex <c>#RRGGBB</c>.</summary>
    public string? CorGradienteFim { get; set; }

    // ---- Domínios desta instância ----

    /// <summary>URL do painel administrativo (ex.: "https://smsmarica.online").</summary>
    public string? UrlPainel { get; set; }

    /// <summary>URL do app do cidadão.</summary>
    public string? UrlApp { get; set; }

    /// <summary>URL do PWA de digitalização de arquivos.</summary>
    public string? UrlArquivos { get; set; }

    // ---- Assinatura do fornecedor ----

    /// <summary>
    /// HTML curto exibido de forma discreta no login e no rodapé ("desenvolvido por…").
    /// Fica em configuração, e não fixo no código, porque o contrato de cada município
    /// decide se e como o fornecedor aparece.
    /// </summary>
    public string? AssinaturaProdutoHtml { get; set; }

    public Guid? AtualizadoPorUsuarioId { get; set; }
    public Usuario? AtualizadoPorUsuario { get; set; }

    public DateTime? AtualizadoEm { get; set; }
}
