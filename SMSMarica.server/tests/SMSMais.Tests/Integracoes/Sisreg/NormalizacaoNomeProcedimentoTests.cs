using FluentAssertions;
using SMSMarica.Core.Integracoes.SisregWeb.Importacao;

namespace SMSMais.Tests.Integracoes.Sisreg;

/// <summary>
/// A forma canônica do nome do procedimento é a CHAVE do tipo de exame — dois nomes que deveriam
/// ser o mesmo e normalizam diferente viram dois tipos, e o exame passa a aparecer com nome
/// duplicado na lista. Por isso ela é testada isoladamente.
/// </summary>
public class NormalizacaoNomeProcedimentoTests
{
    [Fact]
    public void Sobe_para_maiusculas()
    {
        ResolvedorTipoExameSisreg.NormalizarNome("Ultrassom de tireoide")
            .Should().Be("ULTRASSOM DE TIREOIDE");
    }

    [Fact]
    public void Colapsa_espaco_duplo_que_o_sisreg_manda()
    {
        // "CONSULTA  EM CARDIOLOGIA - PEDIATRIA" existe assim, com espaço duplo, na produção.
        ResolvedorTipoExameSisreg.NormalizarNome("CONSULTA  EM CARDIOLOGIA - PEDIATRIA")
            .Should().Be("CONSULTA EM CARDIOLOGIA - PEDIATRIA");
    }

    [Fact]
    public void Tira_espaco_das_pontas_e_quebras_de_linha()
    {
        ResolvedorTipoExameSisreg.NormalizarNome("  MAMOGRAFIA\tBILATERAL \r\n")
            .Should().Be("MAMOGRAFIA BILATERAL");
    }

    [Fact]
    public void Preserva_acento_e_pontuacao()
    {
        // Nada de "normalizar" acento: o nome exibido tem que ser o que o SISREG escreveu.
        ResolvedorTipoExameSisreg.NormalizarNome("ULTRASSONOGRAFIA DO APARELHO URINARIO( RINS,BEXIGA ) - PEDIATRICA")
            .Should().Be("ULTRASSONOGRAFIA DO APARELHO URINARIO( RINS,BEXIGA ) - PEDIATRICA");
    }

    [Fact]
    public void Vazio_continua_vazio_para_o_chamador_decidir()
    {
        ResolvedorTipoExameSisreg.NormalizarNome("   ").Should().BeEmpty();
    }

    [Fact]
    public void Trunca_no_limite_da_coluna_nome()
    {
        var gigante = new string('A', 250);

        ResolvedorTipoExameSisreg.NormalizarNome(gigante).Should().HaveLength(200);
    }
}
