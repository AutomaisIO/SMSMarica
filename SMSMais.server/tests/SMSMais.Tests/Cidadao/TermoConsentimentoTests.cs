using SMSMais.Core.Cidadao;
using SMSMais.Core.Institucional.Dtos;

namespace SMSMais.Tests.Cidadao;

/// <summary>
/// Congela a garantia do ADR-0046: com os dados de Maricá, o termo montado do template
/// reproduz BYTE A BYTE o texto 1.0 que era constante no código — os SHA-256 gravados em
/// cada aceite existente continuam batendo. Se este teste quebrar, NÃO ajuste o esperado:
/// ou você mudou uma costura do molde (suba a versão do termo) ou mudou dado da
/// instituição que participa do texto.
/// </summary>
public class TermoConsentimentoTests
{
    private static InstituicaoDto Marica() => new(
        Nome: "Prefeitura Municipal de Maricá",
        NomeSecretaria: "Secretaria Municipal de Saúde de Maricá",
        NomeCurto: "Saúde Maricá",
        Sigla: null, Cnpj: null, CodigoIbge: null, Uf: "RJ", DddPadrao: 21,
        Endereco: null, Telefone: null, EmailContato: null,
        EmailDpo: "lgpd@smsmarica.online",
        WhatsAppNumeroPublico: null, LogoMidiaId: null, FaviconMidiaId: null,
        CorPrimaria: null, CorSecundaria: null, CorGradienteInicio: null, CorGradienteFim: null,
        UrlPainel: null, UrlApp: null, UrlArquivos: null,
        AssinaturaProdutoHtml: null, AtualizadoEm: null);

    [Fact]
    public void Com_os_dados_de_marica_reproduz_o_texto_1_0_byte_a_byte()
    {
        var termo = TermoConsentimento.Montar(Marica());
        termo.Texto.Should().Be(TextoLegado10);
    }

    [Fact]
    public void Hash_do_texto_de_marica_e_o_historico()
    {
        // O hex do SHA-256 do texto 1.0 — o mesmo valor gravado nos aceites em produção.
        var termo = TermoConsentimento.Montar(Marica());
        var esperado = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(TextoLegado10)))
            .ToLowerInvariant();
        termo.Hash.Should().Be(esperado);
    }

    [Fact]
    public void Sem_instituicao_configurada_o_texto_e_neutro_sem_municipio()
    {
        var neutra = Marica() with
        {
            Nome = "Prefeitura Municipal",
            NomeSecretaria = "Secretaria Municipal de Saúde",
            NomeCurto = "Saúde",
            EmailDpo = null,
        };
        var termo = TermoConsentimento.Montar(neutra);
        termo.Texto.Should().NotContain("Maricá").And.NotContain("marica");
    }

    /// <summary>O texto 1.0 EXATAMENTE como era a constante (não editar).</summary>
    private const string TextoLegado10 =
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
}
