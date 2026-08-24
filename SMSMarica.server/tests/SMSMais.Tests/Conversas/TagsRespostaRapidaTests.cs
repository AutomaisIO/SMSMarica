using SMSMais.Core.Conversas.RespostasRapidas;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Tests.Conversas;

public class TagsRespostaRapidaTests
{
    private static readonly ContextoTags Contexto = new(
        PacienteNome: "MARIA DAS DORES DA SILVA",
        Cpf: "12345678901",
        Cns: null,
        DataNascimento: new DateOnly(1980, 5, 9),
        Telefone: "5521999990000",
        OperadorNome: "BERNARDO ALMEIDA",
        UnidadeNome: "CDT");

    private static string Resolver(
        string corpo,
        Dictionary<string, string>? valores = null,
        Dictionary<string, TipoCampoRespostaRapida>? tipos = null) =>
        TagsRespostaRapida.Resolver(corpo, Contexto, valores ?? [], tipos ?? []);

    [Fact]
    public void Tags_automaticas_saem_do_contexto_ja_capitalizadas()
    {
        var texto = Resolver("{{saudacao}}, {{primeironome}}! Aqui é {{atendente}}, da {{unidade}}.");

        Assert.Contains("Maria", texto);
        Assert.Contains("Bernardo", texto);
        Assert.Contains("CDT", texto);
        Assert.DoesNotContain("{{", texto);
    }

    [Fact]
    public void Nome_completo_e_cpf_saem_formatados()
    {
        var texto = Resolver("{{nomecompleto}} — CPF {{cpf}} — nasc. {{nascimento}}");

        Assert.Equal("Maria das Dores da Silva — CPF 123.456.789-01 — nasc. 09/05/1980", texto);
    }

    [Fact]
    public void Campo_manual_de_data_sai_no_formato_do_paciente()
    {
        var texto = Resolver(
            "Seu exame é {{data_exame}} na sala {{sala}}.",
            new() { ["data_exame"] = "2026-07-20", ["sala"] = "3" },
            new() { ["data_exame"] = TipoCampoRespostaRapida.Data, ["sala"] = TipoCampoRespostaRapida.Numero });

        Assert.Equal("Seu exame é 20/07/2026 na sala 3.", texto);
    }

    [Fact]
    public void Tag_sem_valor_continua_visivel_para_o_operador_ver_o_buraco()
    {
        // CNS não existe no cadastro e o campo manual veio vazio: nada de frase truncada.
        var texto = Resolver("CNS {{cns}}, sala {{sala}}", new() { ["sala"] = "  " });

        Assert.Equal("CNS {{cns}}, sala {{sala}}", texto);
        Assert.Equal(["cns", "sala"], TagsRespostaRapida.Extrair(texto));
    }

    [Fact]
    public void Extrair_lista_as_tags_sem_repetir_e_na_ordem()
    {
        var tags = TagsRespostaRapida.Extrair("{{primeironome}}, {{sala}} e de novo {{primeironome}}");

        Assert.Equal(["primeironome", "sala"], tags);
    }

    [Fact]
    public void EhAutomatica_reconhece_o_catalogo_e_rejeita_o_resto()
    {
        Assert.True(TagsRespostaRapida.EhAutomatica("primeironome"));
        Assert.True(TagsRespostaRapida.EhAutomatica("PRIMEIRONOME"));
        Assert.False(TagsRespostaRapida.EhAutomatica("data_exame"));
    }
}
