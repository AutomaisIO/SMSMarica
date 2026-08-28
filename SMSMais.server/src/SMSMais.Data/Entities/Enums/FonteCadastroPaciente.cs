namespace SMSMais.Data.Entities.Enums;

/// <summary>
/// De onde sai o CADASTRO do paciente (nome, CPF, CNS, nascimento, mãe) quando a importação
/// precisa resolver um CNS que ainda não existe na nossa base.
///
/// <para><b>Por que é configurável:</b> os dois caminhos leem o MESMO CADSUS nacional — o que muda
/// é a porta. A do SISREG (<c>cadweb50</c>) tem orçamento anti-robô: por volta de 500 requisições
/// por hora ele passa a exigir CAPTCHA, e aí a unidade fica travada 24h porque só um humano
/// abrindo o SISREG no navegador destrava (ver <c>Automais.SISREG/docs/APRENDIZADOS.md</c>). Uma
/// importação de agenda grande estoura esse teto sozinha: em 26/08/2026, 271 linhas viraram
/// pendência com "o SISREG passou a exigir CAPTCHA". A porta do SER (painel de paciente da tela
/// de solicitação) responde o mesmo cadastro sem esse teto.</para>
/// </summary>
public enum FonteCadastroPaciente
{
    /// <summary>CADSUS pela tela <c>cadweb50</c> do SISREG III. Default histórico.</summary>
    Sisreg = 1,

    /// <summary>Painel de paciente do SER (SES-RJ). Não gasta o orçamento anti-robô do SISREG.</summary>
    Ser = 2,

    /// <summary>
    /// SER primeiro; só quando ele falha (sessão caída, layout mudado) a consulta cai para o
    /// SISREG. Cuidado: sob falha contínua do SER isto vira consumo integral do orçamento do
    /// SISREG — que é exatamente o que se quer evitar em lote grande.
    /// </summary>
    SerComFallbackSisreg = 3,
}
