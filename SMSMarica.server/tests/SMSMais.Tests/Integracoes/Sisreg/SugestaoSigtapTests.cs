using SMSMarica.Core.Integracoes.SisregWeb.Varredura.Sigtap;

namespace SMSMais.Tests.Integracoes.Sisreg;

/// <summary>
/// A heurística que liga o procedimento do SISREG ao SIGTAP. O teste que mais importa aqui é o
/// que garante que semelhança NÃO auto-confirma: SIGTAP errado não estoura em lugar nenhum —
/// vira worklist errada e laudo no exame errado, semanas depois.
/// </summary>
public class SugestaoSigtapTests
{
    private static readonly SugestaoSigtap.Candidato Mamografia =
        new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "02.04.03.018-8", "MAMOGRAFIA BILATERAL");

    private static readonly SugestaoSigtap.Candidato MamografiaUnilateral =
        new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "02.04.03.017-0", "MAMOGRAFIA UNILATERAL");

    private static readonly SugestaoSigtap.Candidato Ultrassom =
        new(Guid.Parse("33333333-3333-3333-3333-333333333333"), "02.05.02.004-6", "ULTRASSONOGRAFIA MAMARIA BILATERAL");

    private static readonly List<SugestaoSigtap.Candidato> Catalogo = [Mamografia, MamografiaUnilateral, Ultrassom];

    [Fact]
    public void Nome_identico_confirma_sozinho()
    {
        var sugestao = SugestaoSigtap.Sugerir("MAMOGRAFIA BILATERAL", Catalogo);

        Assert.NotNull(sugestao);
        Assert.True(sugestao.Exata);
        Assert.Equal(Mamografia.Id, sugestao.ProcedimentoSigtapId);
        Assert.Equal(1.0, sugestao.Score);
    }

    [Theory]
    [InlineData("Mamografia Bilateral")]          // caixa
    [InlineData("MAMOGRAFIA  BILATERAL")]         // espaço duplicado
    [InlineData("MAMOGRAFIA, BILATERAL")]         // pontuação
    [InlineData("GRUPO - MAMOGRAFIA BILATERAL")]  // agregador do SISREG
    [InlineData("GRUPO-MAMOGRAFIA BILATERAL")]    // agregador sem espaço
    public void Diferencas_de_grafia_ainda_sao_igualdade(string nomeSisreg)
    {
        var sugestao = SugestaoSigtap.Sugerir(nomeSisreg, Catalogo);

        Assert.NotNull(sugestao);
        Assert.True(sugestao.Exata);
        Assert.Equal(Mamografia.Id, sugestao.ProcedimentoSigtapId);
    }

    [Fact]
    public void Acento_nao_impede_a_igualdade()
    {
        var catalogo = new List<SugestaoSigtap.Candidato>
        {
            new(Guid.NewGuid(), "03.01.01.004-8", "AVALIAÇÃO CARDIOLÓGICA"),
        };

        var sugestao = SugestaoSigtap.Sugerir("AVALIACAO CARDIOLOGICA", catalogo);

        Assert.NotNull(sugestao);
        Assert.True(sugestao.Exata);
    }

    [Fact]
    public void Parecido_mas_diferente_sugere_sem_confirmar()
    {
        // "MAMOGRAFIA" sozinha parece as duas — e é justamente onde um palpite erra o lado.
        var sugestao = SugestaoSigtap.Sugerir("MAMOGRAFIA BILATERAL DE RASTREAMENTO", Catalogo);

        Assert.NotNull(sugestao);
        Assert.False(sugestao.Exata);
        Assert.True(sugestao.Score < 1.0);
    }

    [Fact]
    public void Nada_parecido_nao_sugere_nada()
    {
        var sugestao = SugestaoSigtap.Sugerir("CONSULTA EM ORTOPEDIA", Catalogo);

        Assert.Null(sugestao);
    }

    [Fact]
    public void Nome_vazio_ou_catalogo_vazio_nao_sugere()
    {
        Assert.Null(SugestaoSigtap.Sugerir("", Catalogo));
        Assert.Null(SugestaoSigtap.Sugerir("   ", Catalogo));
        Assert.Null(SugestaoSigtap.Sugerir("MAMOGRAFIA BILATERAL", []));
    }

    [Fact]
    public void Palavras_curtas_nao_inflam_a_semelhanca()
    {
        // "DE"/"EM"/"DO" aparecem em quase todo procedimento; se contassem, qualquer par
        // pareceria semelhante e o operador seria inundado de sugestão inútil.
        var catalogo = new List<SugestaoSigtap.Candidato>
        {
            new(Guid.NewGuid(), "04.01.01.001-1", "DRENAGEM DE ABSCESSO"),
        };

        Assert.Null(SugestaoSigtap.Sugerir("CONSULTA DE RETORNO", catalogo));
    }

    [Fact]
    public void Normalizar_e_estavel_para_o_mesmo_conteudo()
    {
        Assert.Equal(
            SugestaoSigtap.Normalizar("GRUPO - Ultrassonografia Mamária, Bilateral"),
            SugestaoSigtap.Normalizar("ULTRASSONOGRAFIA MAMARIA BILATERAL"));
    }
}
