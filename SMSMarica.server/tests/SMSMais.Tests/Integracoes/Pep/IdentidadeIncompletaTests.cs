using Hl7.Fhir.Model;
using SMSMarica.Core.Integracoes.Pep.Estrategias.Salux;

namespace SMSMais.Tests.Integracoes.Pep;

/// <summary>
/// Paciente SEM CPF entra no hub marcado, em vez de ser descartado.
///
/// <para>Até 03/08 o importador exigia CPF e varria 63.324 pacientes do Salux (17,1%) para
/// debaixo do tapete — com 127 mil atendimentos e 8.216 internações, das quais <b>6.697 de
/// recém-nascidos</b> (o HMCML é hospital maternal; bebê não tem CPF). Perder o registro de
/// nascimento para preservar uma promessa de unicidade é a troca errada.</para>
///
/// <para>A tag <c>meta.tag</c> declara a incerteza de forma buscável e é o que impede o
/// registro de participar de qualquer casamento entre bases.</para>
/// </summary>
public class IdentidadeIncompletaTests
{
    private static Patient Novo() => new() { Name = [new HumanName { Text = "FULANO DE TAL" }] };

    [Fact]
    public void Marca_com_o_system_e_o_codigo_buscaveis()
    {
        var p = Novo();
        SaluxFhirMapper.MarcarIdentidadeIncompleta(p);

        var tag = Assert.Single(p.Meta!.Tag);
        Assert.Equal("urn:smsmarica:qualidade", tag.System);
        Assert.Equal("identidade-incompleta", tag.Code);
        Assert.False(string.IsNullOrWhiteSpace(tag.Display));
    }

    [Fact]
    public void E_idempotente__reimport_nao_duplica_a_tag()
    {
        var p = Novo();
        SaluxFhirMapper.MarcarIdentidadeIncompleta(p);
        SaluxFhirMapper.MarcarIdentidadeIncompleta(p);
        SaluxFhirMapper.MarcarIdentidadeIncompleta(p);

        Assert.Single(p.Meta!.Tag);
    }

    [Fact]
    public void Nao_pisa_em_tag_de_outro_system()
    {
        var p = Novo();
        p.Meta = new Meta { Tag = [new Coding("urn:outra:coisa", "algum-codigo")] };

        SaluxFhirMapper.MarcarIdentidadeIncompleta(p);

        Assert.Equal(2, p.Meta.Tag.Count);
        Assert.Contains(p.Meta.Tag, t => t.System == "urn:outra:coisa");
        Assert.Contains(p.Meta.Tag, t => t.Code == "identidade-incompleta");
    }

    [Fact]
    public void Nao_toca_no_dado_clinico__so_no_meta()
    {
        var p = Novo();
        p.BirthDate = "1980-05-10";
        p.Identifier = [new Identifier("urn:salux:cd_paciente", "salux-hcml:4242")];

        SaluxFhirMapper.MarcarIdentidadeIncompleta(p);

        Assert.Equal("1980-05-10", p.BirthDate);
        Assert.Single(p.Identifier);
        Assert.Equal("FULANO DE TAL", p.Name[0].Text);
    }

    /// <summary>
    /// A promoção é automática e não precisa de código: o paciente que ganha CPF na origem
    /// passa pelo caminho canônico, e o recurso montado do zero simplesmente não recebe a tag.
    /// </summary>
    [Fact]
    public void Paciente_com_CPF_nao_recebe_a_marca()
    {
        var p = Novo();
        p.Identifier = [new Identifier("https://fhir.saude.gov.br/sid/cpf", "12345678909")];

        Assert.True(p.Meta?.Tag is null or { Count: 0 });
    }
}
