using FluentAssertions;
using SMSMais.Core.Integracoes.SisregWeb.Importacao;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Tests.Integracoes.Sisreg;

/// <summary>
/// A modalidade inferida decide para QUAL equipamento o exame vai: o resolvedor de estação casa
/// unidade executante + modalidade por igualdade exata. Errar aqui manda o pedido para a sala
/// errada — e em silêncio, porque nenhum componente isolado falha.
/// </summary>
public class InferenciaModalidadeTests
{
    [Theory]
    // Os quatro códigos medidos em produção (04/09/2026): o SISREG exporta 020403xxxx para
    // radiografia de TÓRAX e de COSTELAS. No SIGTAP oficial o subgrupo 02.04.03 tem só três
    // procedimentos, todos mamografia — estes códigos nem existem lá. A regra antiga olhava 6
    // dígitos e marcava os cinco tipos como MG; eram 165 exames de raio-X apontando para o
    // mamógrafo do CDT.
    [InlineData("RADIOGRAFIA DE TORAX (PA E PERFIL)", "0204030153")]
    [InlineData("RADIOGRAFIA DE TÓRAX- PROGRAMA TUBERCULOSE", "0204030153")]
    [InlineData("RADIOGRAFIA DE TORAX (PA + LATERAL + OBLIQUA)", "0204030145")]
    [InlineData("RADIOGRAFIA DE TORAX (PA)", "0204030170")]
    [InlineData("RADIOGRAFIA DE COSTELAS (HEMITORAX ESQUERDO)", "0204030072")]
    public void Radiografia_com_codigo_de_mamografia_defasado_continua_DX(string nome, string sigtap)
    {
        ResolvedorTipoExameSisreg.InferirModalidade(nome, sigtap)
            .Should().Be(ModalidadeDicom.DX);
    }

    [Theory]
    [InlineData("MAMOGRAFIA BILATERAL", "0204030030")]
    [InlineData("Mamografia bilateral de rastreamento", "0204030188")]
    [InlineData("MAMOGRAFIA UNILATERAL MAMA D (ONCOLOGICA)", "0204030013")]
    public void Mamografia_sai_pelo_nome(string nome, string sigtap)
    {
        ResolvedorTipoExameSisreg.InferirModalidade(nome, sigtap)
            .Should().Be(ModalidadeDicom.MG);
    }

    [Fact]
    public void Mamografia_e_reconhecida_mesmo_sem_codigo_nenhum()
    {
        // Um terço das linhas do SISREG chega sem código — o nome é a única chave confiável.
        ResolvedorTipoExameSisreg.InferirModalidade("MAMOGRAFIA BILATERAL", string.Empty)
            .Should().Be(ModalidadeDicom.MG);
    }

    [Theory]
    [InlineData("0204", ModalidadeDicom.DX)]
    [InlineData("0205", ModalidadeDicom.US)]
    [InlineData("0206", ModalidadeDicom.CT)]
    [InlineData("0207", ModalidadeDicom.MR)]
    public void Subgrupo_de_quatro_digitos_segue_valendo(string sigtap, ModalidadeDicom esperada)
    {
        ResolvedorTipoExameSisreg.InferirModalidade("PROCEDIMENTO QUALQUER", sigtap)
            .Should().Be(esperada);
    }

    [Theory]
    [InlineData("")]
    [InlineData("020")]
    [InlineData("0301")]
    public void Sem_codigo_util_e_sem_nome_de_mamografia_fica_indefinida(string sigtap)
    {
        // Não chuta: Indefinida nunca casa com equipamento, então o exame para em vez de ir errado.
        ResolvedorTipoExameSisreg.InferirModalidade("CONSULTA EM CARDIOLOGIA", sigtap)
            .Should().Be(ModalidadeDicom.Indefinida);
    }
}
