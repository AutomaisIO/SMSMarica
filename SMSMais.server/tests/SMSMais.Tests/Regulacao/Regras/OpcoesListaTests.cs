using System.Text.Json;

using SMSMais.Core.Regulacao.Regras;

namespace SMSMais.Tests.Regulacao.Regras;

/// <summary>
/// O formato versionado das opções das perguntas de lista. O que prendem: o leitor entende o
/// formato antigo (v0, lista crua) e o atual (v1, envelope) — é isso que deixa trocar o formato e
/// fazer backfill sem quebrar tela —, e o que se grava sai sempre na versão atual.
/// </summary>
public class OpcoesListaTests
{
    [Fact]
    public void Le_o_formato_antigo_como_opcoes_numeradas()
    {
        var opcoes = OpcoesLista.LerDaRegra("""["Genitália ambígua", "Doenças Raras"]""");

        opcoes.Should().Equal(new OpcaoLista("o1", "Genitália ambígua"), new OpcaoLista("o2", "Doenças Raras"));
    }

    [Fact]
    public void Grava_na_versao_atual_e_le_de_volta()
    {
        var json = OpcoesLista.GravarDaRegra([" Genitália ambígua ", "", "Doenças Raras"]);

        using var doc = JsonDocument.Parse(json!);
        doc.RootElement.GetProperty("v").GetInt32().Should().Be(OpcoesLista.VersaoAtual);
        OpcoesLista.LerDaRegra(json).Should()
            .Equal(new OpcaoLista("o1", "Genitália ambígua"), new OpcaoLista("o2", "Doenças Raras"));
    }

    [Fact]
    public void Resposta_guarda_id_e_texto()
    {
        var json = OpcoesLista.GravarDaResposta([new OpcaoLista("o2", "Doenças Raras")]);

        json.Should().Contain("\"marcadas\"");
        OpcoesLista.LerDaResposta(json).Should().Equal(new OpcaoLista("o2", "Doenças Raras"));
    }

    [Fact]
    public void Versao_nova_que_so_acrescenta_campo_continua_legivel()
    {
        var json = """{"v":2,"opcoes":[{"id":"o1","texto":"Asma","idadeMax":12}]}""";

        OpcoesLista.LerDaRegra(json).Should().Equal(new OpcaoLista("o1", "Asma"));
    }

    [Fact]
    public void Parecer_antigo_com_texto_solto_continua_abrindo()
    {
        // Pareceres da análise do SER gravados antes do envelope têm as opções como texto solto.
        var dto = JsonSerializer.Deserialize<PerguntaPendenteDto>(
            """{"regraId":"6f9a0e2e-0000-0000-0000-000000000000","pergunta":"P?","opcoes":["A","B"]}""",
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        dto!.Opcoes!.Select(o => o.Texto).Should().Equal("A", "B");
    }

    [Fact]
    public void Json_torto_vira_pergunta_simples()
    {
        OpcoesLista.LerDaRegra("{isto não é json").Should().BeEmpty();
        OpcoesLista.LerDaRegra(null).Should().BeEmpty();
    }
}
