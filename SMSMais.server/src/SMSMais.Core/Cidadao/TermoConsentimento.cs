using System.Security.Cryptography;
using System.Text;
using SMSMais.Core.Institucional.Dtos;

namespace SMSMais.Core.Cidadao;

/// <summary>Texto vigente do termo + seu SHA-256 (hex minúsculo), como montado para esta instância.</summary>
public sealed record TermoVigente(string Texto, string Hash);

/// <summary>
/// Termo de consentimento LGPD do app do cidadão. Fonte única da <b>versão vigente</b> e do
/// <b>molde</b> do texto — os nomes (app, controladora, rede, DPO) vêm da instituição desta
/// instância (ADR-0043/0046), nunca fixos em código. Ao mudar o molde, incremente a versão —
/// o gate passa a exigir novo aceite de todos. O hash do texto montado é gravado em cada
/// aceite para provar exatamente o que foi consentido.
///
/// <b>Compatibilidade byte a byte:</b> com os dados de Maricá, <see cref="Montar"/> reproduz
/// EXATAMENTE o texto 1.0 que era constante aqui — os hashes já gravados continuam válidos.
/// O teste <c>TermoConsentimentoTests</c> congela essa garantia; não mude as costuras sem
/// subir a versão.
/// </summary>
public static class TermoConsentimento
{
    public const string VersaoVigente = "1.0";

    /// <summary>Monta o texto vigente para a instituição informada e calcula o hash.</summary>
    public static TermoVigente Montar(InstituicaoDto inst)
    {
        var app = inst.NomeCurto;
        var controladora = inst.NomeSecretaria;
        var rede = RedeDe(inst.NomeSecretaria);
        var dpo = string.IsNullOrWhiteSpace(inst.EmailDpo) ? "não informado" : inst.EmailDpo!;

        var texto =
            $"""
            Termo de Consentimento e Uso de Dados — {app} (versão {VersaoVigente})

            Para usar o aplicativo {app}, você precisa ler e concordar com os pontos abaixo.

            Quem trata seus dados: a {controladora}, responsável (controladora) pelos seus dados neste aplicativo.

            Quais dados usamos: seus dados pessoais (nome, CPF, CNS, contato) e seus dados de saúde (atendimentos, exames, laudos, documentos e informações de transporte em saúde/TFD).

            Para quê: identificar você com segurança, exibir suas informações de saúde, permitir agendamentos e acompanhar seu transporte (TFD), prestando e dando continuidade ao seu cuidado.

            Sincronização e compartilhamento: seus dados são sincronizados e integrados a um repositório clínico central da {rede} e podem ser acessados pelas unidades e profissionais de saúde envolvidos no seu atendimento, para garantir a continuidade do cuidado. Não vendemos seus dados nem os usamos para publicidade.

            Base legal (LGPD – Lei nº 13.709/2018): o tratamento de dados de saúde ocorre para a tutela da saúde, por profissionais e serviços de saúde (art. 11, II, "f"), e, no que couber, mediante o seu consentimento (art. 7º, I e art. 11, I).

            Seus direitos: você pode, a qualquer momento, acessar e corrigir seus dados e revogar este consentimento. A revogação encerra seu acesso ao aplicativo e não afeta os tratamentos já realizados nem as obrigações legais de guarda dos dados de saúde.

            Segurança: adotamos medidas técnicas e administrativas para proteger seus dados contra acesso não autorizado.

            Contato / Encarregado de Dados (DPO): {dpo}.

            Ao tocar em "Li e concordo", você declara que leu, entendeu e concorda com este termo.
            """;

        return new TermoVigente(texto, HashDe(texto));
    }

    /// <summary>
    /// "Rede" no texto do termo: o nome da secretaria sem o prefixo "Secretaria Municipal de "
    /// — "Secretaria Municipal de Saúde de Maricá" vira "Saúde de Maricá". Se a secretaria
    /// não seguir o padrão, usa o nome como veio.
    /// </summary>
    private static string RedeDe(string nomeSecretaria)
    {
        const string prefixo = "Secretaria Municipal de ";
        return nomeSecretaria.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase)
            ? nomeSecretaria[prefixo.Length..]
            : nomeSecretaria;
    }

    private static string HashDe(string texto) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(texto))).ToLowerInvariant();
}
