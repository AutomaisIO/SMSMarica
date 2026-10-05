using FluentAssertions;
using Hl7.Fhir.Model;
using SMSMais.Core.Integracoes.EsusPec;
using SMSMais.Core.Pacientes.Fhir;

namespace SMSMais.Tests.Integracoes;

/// <summary>
/// Regra da correção de telefone pelo e-SUS PEC (ADR-0067). O que dói se quebrar: trocar um número
/// VALIDADO, apagar o número antigo em vez de mandá-lo ao histórico, ou "corrigir" para o mesmo
/// número que já falhou.
/// </summary>
public class TrocaTelefoneEsusTests
{
    private static readonly DateTimeOffset Agora = DateTimeOffset.UtcNow.AddMinutes(-1);

    private static ContactPoint Fone(string numero, int? rank = null) => new()
    {
        System = ContactPoint.ContactPointSystem.Phone,
        Value = numero,
        Rank = rank,
    };

    private static Patient Paciente(params ContactPoint[] fones) => new() { Telecom = [.. fones] };

    [Theory]
    [InlineData("(21) 99876-5432", "21998765432")]
    [InlineData("5521998765432", "21998765432")]
    [InlineData("2126345678", null)]      // fixo
    [InlineData("21 3634-5678", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void CelularNacional_so_aceita_celular(string? entrada, string? esperado) =>
        TrocaTelefoneEsus.CelularNacional(entrada).Should().Be(esperado);

    [Fact]
    public void Validado_nunca_e_trocado()
    {
        var principal = Fone("21911112222", 1);
        principal.AddExtension(PatientMergeFhir.ExtContatoConfirmado, new FhirDateTime(Agora));
        var p = Paciente(principal);

        TrocaTelefoneEsus.Decidir(p, "21933334444", [], false).Should().Be(DecisaoTelefoneEsus.MantemVerificado);
    }

    [Fact]
    public void Mesmo_numero_que_falhou_e_esus_tambem_errado()
    {
        var p = Paciente(Fone("21911112222", 1));
        TrocaTelefoneEsus.Decidir(p, "(21) 91111-2222", ["5521911112222"], false)
            .Should().Be(DecisaoTelefoneEsus.EsusTambemErrado);
    }

    [Fact]
    public void Numero_negado_no_cadastro_e_esus_tambem_errado()
    {
        var negado = Fone("21955556666");
        negado.AddExtension(PatientMergeFhir.ExtContatoNegado, new FhirDateTime(Agora));
        var p = Paciente(Fone("21911112222", 1), negado);

        TrocaTelefoneEsus.Decidir(p, "21955556666", [], false).Should().Be(DecisaoTelefoneEsus.EsusTambemErrado);
    }

    [Fact]
    public void Sem_celular_no_pec_nao_troca()
    {
        var p = Paciente(Fone("21911112222", 1));
        TrocaTelefoneEsus.Decidir(p, "2126345678", [], false).Should().Be(DecisaoTelefoneEsus.EsusSemCelular);
        TrocaTelefoneEsus.Decidir(p, null, [], false).Should().Be(DecisaoTelefoneEsus.EsusSemCelular);
    }

    [Fact]
    public void Numero_confirmado_de_outra_pessoa_nao_troca()
    {
        var p = Paciente(Fone("21911112222", 1));
        TrocaTelefoneEsus.Decidir(p, "21933334444", ["21911112222"], confirmadoPorOutro: true)
            .Should().Be(DecisaoTelefoneEsus.NumeroDeOutraPessoa);
    }

    [Fact]
    public void Ja_e_o_principal_nao_troca()
    {
        var p = Paciente(Fone("21933334444", 1));
        TrocaTelefoneEsus.Decidir(p, "21933334444", ["21911112222"], false).Should().Be(DecisaoTelefoneEsus.JaEOPrincipal);
    }

    [Fact]
    public void Troca_manda_o_antigo_para_o_historico_e_marca_a_origem()
    {
        var antigo = Fone("21911112222", 1);
        var secundario = Fone("21977778888");
        var p = Paciente(antigo, secundario);

        TrocaTelefoneEsus.Decidir(p, "21933334444", ["21911112222"], false).Should().Be(DecisaoTelefoneEsus.Trocar);
        var (historico, anterior) = TrocaTelefoneEsus.AplicarTroca(p, "(21) 93333-4444", ["21911112222"], Agora);

        historico.Should().Be(1);
        anterior.Should().Be("21911112222");
        p.Telecom.Should().HaveCount(3, "o número antigo nunca some do cadastro");

        antigo.Rank.Should().BeNull();
        antigo.Period!.EndElement.Should().NotBeNull();
        PatientMergeFhir.EhAposentado(antigo).Should().BeTrue();

        secundario.Period.Should().BeNull("número que não falhou continua valendo");

        var novo = p.Telecom.Single(t => t.Value == "21933334444");
        novo.Rank.Should().Be(1);
        novo.Period!.StartElement.Should().NotBeNull();
        (novo.GetExtension(PatientMergeFhir.ExtContatoOrigem)?.Value as FhirString)?.Value.Should().Be(TrocaTelefoneEsus.Origem);
        TrocaTelefoneEsus.Principal(p).Should().BeSameAs(novo);
    }

    [Fact]
    public void Troca_reaproveita_numero_que_ja_estava_no_cadastro()
    {
        var antigo = Fone("21911112222", 1);
        var jaTinha = Fone("21933334444");
        var p = Paciente(antigo, jaTinha);

        TrocaTelefoneEsus.AplicarTroca(p, "21933334444", [], Agora);

        p.Telecom.Should().HaveCount(2);
        jaTinha.Rank.Should().Be(1);
        jaTinha.GetExtension(PatientMergeFhir.ExtContatoOrigem).Should().BeNull("não foi o e-SUS que trouxe o número");
        PatientMergeFhir.EhAposentado(antigo).Should().BeTrue();
    }

    [Theory]
    [InlineData("MARIA DA SILVA SOUZA", "JOÃO SOUZA", true)]
    [InlineData("Maria Conceição", "José da Conceicao", true)]   // acento não separa família
    [InlineData("MARIA DA SILVA", "JOSE DOS SANTOS", false)]
    [InlineData("MARIA DE OLIVEIRA", "ANA DE CARVALHO", false)]  // partícula não é sobrenome
    [InlineData("MARIA", "MARIA SILVA", false)]
    public void Sobrenome_em_comum(string a, string b, bool esperado) =>
        TrocaTelefoneEsus.SobrenomeEmComum(a, b).Should().Be(esperado);

    [Theory]
    [InlineData("02:00", "05:00", "03:30", true)]
    [InlineData("02:00", "05:00", "05:00", false)]
    [InlineData("02:00", "05:00", "14:00", false)]
    [InlineData("23:00", "02:00", "00:30", true)]   // janela que vira a meia-noite
    [InlineData("23:00", "02:00", "22:59", false)]
    public void Janela_da_madrugada(string ini, string fim, string agora, bool esperado) =>
        CorrecaoTelefoneEsusService.DentroDaJanela(TimeOnly.Parse(ini), TimeOnly.Parse(fim), TimeOnly.Parse(agora))
            .Should().Be(esperado);

    [Fact]
    public void Parametros_tem_padrao_seguro()
    {
        var p = CorrecaoTelefoneEsusService.Parametros.Ler("""{"acessoId":"22388","janelaInicio":"lixo"}""");
        p.AcessoId.Should().Be("22388");
        p.Inicio.Should().Be(new TimeOnly(2, 0));
        p.Fim.Should().Be(new TimeOnly(5, 0));
        CorrecaoTelefoneEsusService.Parametros.Ler("não é json").Inicio.Should().Be(new TimeOnly(2, 0));
    }
}
