using System.Security.Cryptography;
using System.Text;

namespace SMSMais.Core.Cidadao;

/// <summary>
/// Termo de consentimento LGPD do app do cidadão. Fonte única do <b>texto</b> e da
/// <b>versão vigente</b>. Ao mudar o texto, incremente a versão — o gate passa a exigir
/// novo aceite de todos. O <see cref="Hash"/> (SHA-256 do texto) é gravado em cada aceite
/// para provar exatamente o que foi consentido.
/// </summary>
public static class TermoConsentimento
{
    public const string VersaoVigente = "1.0";

    public const string Texto =
        """
        Termo de Consentimento e Uso de Dados — Saúde Maricá (versão 1.0)

        Para usar o aplicativo Saúde Maricá, você precisa ler e concordar com os pontos abaixo.

        Quem trata seus dados: a Secretaria Municipal de Saúde de Maricá, responsável (controladora) pelos seus dados neste aplicativo.

        Quais dados usamos: seus dados pessoais (nome, CPF, CNS, contato) e seus dados de saúde (atendimentos, exames, laudos, documentos e informações de transporte em saúde/TFD).

        Para quê: identificar você com segurança, exibir suas informações de saúde, permitir agendamentos e acompanhar seu transporte (TFD), prestando e dando continuidade ao seu cuidado.

        Sincronização e compartilhamento: seus dados são sincronizados e integrados a um repositório clínico central da Saúde de Maricá e podem ser acessados pelas unidades e profissionais de saúde envolvidos no seu atendimento, para garantir a continuidade do cuidado. Não vendemos seus dados nem os usamos para publicidade.

        Base legal (LGPD – Lei nº 13.709/2018): o tratamento de dados de saúde ocorre para a tutela da saúde, por profissionais e serviços de saúde (art. 11, II, "f"), e, no que couber, mediante o seu consentimento (art. 7º, I e art. 11, I).

        Seus direitos: você pode, a qualquer momento, acessar e corrigir seus dados e revogar este consentimento. A revogação encerra seu acesso ao aplicativo e não afeta os tratamentos já realizados nem as obrigações legais de guarda dos dados de saúde.

        Segurança: adotamos medidas técnicas e administrativas para proteger seus dados contra acesso não autorizado.

        Contato / Encarregado de Dados (DPO): lgpd@smsmarica.online.

        Ao tocar em "Li e concordo", você declara que leu, entendeu e concorda com este termo.
        """;

    /// <summary>SHA-256 (hex minúsculo) do <see cref="Texto"/> vigente.</summary>
    public static string Hash { get; } =
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Texto))).ToLowerInvariant();
}
