using FluentAssertions;
using SMSMais.Core.SolicitacoesExame;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Tests.Integracoes;

/// <summary>
/// Categoria pelo NOME do SISREG — o caminho usado quando não há SIGTAP (agenda do
/// <c>cons_agendas</c>). O eixo é o nome; o SIGTAP é trabalho de faturamento, à parte.
/// </summary>
public class CategoriaSigtapPorNomeTests
{
    [Theory]
    [InlineData("USG OBSTETRICA")]
    [InlineData("USG MORFOLOGICO")]
    [InlineData("USG TRANSLUCENCIA NUCAL")]
    [InlineData("USG TRANSVAGINAL - GESTANTE")]
    [InlineData("ULTRASONOGRAFIA DE ABDOMEM TOTAL")]
    [InlineData("MAMOGRAFIA BILATERAL")]
    [InlineData("RAIO X DE TORAX")]
    [InlineData("TOMOGRAFIA COMPUTADORIZADA DE CRANIO")]
    [InlineData("RESSONANCIA MAGNETICA DE JOELHO")]
    [InlineData("ECOCARDIOGRAMA")]
    [InlineData("ULTRASSONOGRAFIA OBSTÉTRICA")] // com acento — a normalização tira
    public void Imagem_por_termo_no_nome(string nome) =>
        CategoriaSigtap.ResolverPorNome(nome).Should().Be(CategoriaSolicitacao.Imagem);

    [Theory]
    [InlineData("CONSULTA EM CARDIOLOGIA")]
    [InlineData("CONSULTA EM PLANEJAMENTO FAMILIAR")]
    public void Consulta_quando_o_nome_diz_consulta(string nome) =>
        CategoriaSigtap.ResolverPorNome(nome).Should().Be(CategoriaSolicitacao.Consulta);

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("PROCEDIMENTO DESCONHECIDO QUALQUER")]
    public void Outro_quando_nao_reconhece(string? nome) =>
        CategoriaSigtap.ResolverPorNome(nome).Should().Be(CategoriaSolicitacao.Outro);
}
