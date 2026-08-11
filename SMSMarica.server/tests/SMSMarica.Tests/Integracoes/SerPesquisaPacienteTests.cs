using FluentAssertions;
using SMSMarica.Core.Ser;

namespace SMSMarica.Tests.Integracoes;

/// <summary>
/// Pesquisa de paciente no SER (CNS/CPF) — o motor que resolve o cadastro sem passar pelo CADSUS
/// do SISREG, que tem limitação de acesso.
///
/// <para>O HTML abaixo é RECORTE DA CAPTURA REAL de 10/08/2026 (dados trocados). O que está sob
/// teste é a distinção que decide a tela: o SER trava a identidade com <c>disabled</c>, e input
/// travado não é enviado pelo navegador — oferecer digitação neles seria oferecer um campo que o
/// SER descarta.</para>
/// </summary>
public class SerPesquisaPacienteTests
{
    /// <summary>
    /// Estrutura fiel: <c>&lt;td&gt;</c> com <c>&lt;label&gt;</c> (podendo ter ícone e asterisco)
    /// seguido do campo. Note que dois telefones têm SÓ <c>name</c> posicional, sem <c>id</c>.
    /// </summary>
    private const string Painel = """
        <html><body><form id="form0">
          <div id="form0:divMensagens">O CNS definitivo é diferente do CNS provisório</div>
          <div id="form0:painelDadosDoPaciente"><span id="form0:renderizacaoPaciente"><table><tbody><tr>
            <td><label>Nome<span id="form0:j_id126" class="required" style="color:red">*</span></label><br />
                <input id="form0:nome" type="text" name="form0:nome" disabled="disabled" value="MARLI ROSA" /></td>
            <td><label>CPF</label><br />
                <input id="form0:cpf" type="text" name="form0:cpf" disabled="disabled" value="027.339.137-27" /></td>
            <td><label>Nome Social</label><br />
                <input id="form0:nomeSocial" type="text" name="form0:nomeSocial" value="MONICA SANTOS" /></td>
            <td><label>Sexo<span class="required">*</span></label><br />
                <select id="form0:sexo" name="form0:sexo" disabled="disabled">
                  <option value="1">Masculino</option><option value="2" selected="selected">Feminino</option>
                </select></td>
            <td><label>Bairro</label><br />
                <input id="form0:bairro" type="text" name="form0:bairro" value="JACONÉ" /></td>
            <td><label><i class="fas fa-phone"></i>Telefone Residencial</label><br />
                <input class="input telefone" name="form0:j_id173" type="text" value="(21) 93639-2053" /></td>
            <td><label><i class="fas fa-mobile-alt"></i>Telefone WhatsApp<span class="required">*</span></label><br />
                <input class="input telefone" name="form0:j_id178" type="text" value="(21) 97738-1014" /></td>
          </tr></tbody></table></span></div>
        </form></body></html>
        """;

    [Fact]
    public void Identidade_vem_travada_e_o_resto_editavel()
    {
        var campos = SerNovaSolicitacaoService.CamposDoPaciente(Painel);

        campos.Single(c => c.Campo == "form0:nome").Editavel.Should().BeFalse();
        campos.Single(c => c.Campo == "form0:cpf").Editavel.Should().BeFalse();
        campos.Single(c => c.Campo == "form0:sexo").Editavel.Should().BeFalse();

        campos.Single(c => c.Campo == "form0:nomeSocial").Editavel.Should().BeTrue();
        campos.Single(c => c.Campo == "form0:bairro").Editavel.Should().BeTrue();
        campos.Single(c => c.Campo == "form0:j_id178").Editavel.Should().BeTrue(
            "endereço e telefones são o que muda na vida do cidadão");
    }

    /// <summary>
    /// Dois dos três telefones não têm <c>id</c>, só <c>name</c> posicional. Se o rótulo saísse do
    /// id, eles ficariam anônimos na tela; e chumbar o <c>j_id</c> daria formulário mudo na próxima
    /// recompilação da SES-RJ — sem erro nenhum, como sempre acontece neste sistema.
    /// </summary>
    [Fact]
    public void Telefone_sem_id_ainda_sai_com_rotulo_e_com_o_name_da_pagina()
    {
        var campos = SerNovaSolicitacaoService.CamposDoPaciente(Painel);

        var zap = campos.Single(c => c.Campo == "form0:j_id178");
        zap.Rotulo.Should().Be("Telefone WhatsApp", "o ícone não pode virar rótulo");
        zap.Obrigatorio.Should().BeTrue("o SER marca o WhatsApp com asterisco");

        campos.Single(c => c.Campo == "form0:j_id173").Rotulo.Should().Be("Telefone Residencial");
    }

    [Fact]
    public void Select_traz_opcoes_e_o_valor_selecionado()
    {
        var sexo = SerNovaSolicitacaoService.CamposDoPaciente(Painel).Single(c => c.Campo == "form0:sexo");

        sexo.Tipo.Should().Be("select");
        sexo.Valor.Should().Be("2");
        sexo.Opcoes.Should().HaveCount(2);
    }

    /// <summary>
    /// O aviso de CNS definitivo × provisório é a mesma armadilha que nos custou 17 duplicatas no
    /// piloto de conciliação. Perdê-lo seria jogar fora o único lugar onde o SER a denuncia.
    /// </summary>
    [Fact]
    public void Aviso_do_ser_e_preservado()
    {
        SerNovaSolicitacaoService.Avisos(Painel).Should()
            .ContainSingle().Which.Should().Contain("CNS definitivo");
    }

    [Fact]
    public void Botao_pesquisar_sai_da_pagina_e_nao_de_id_fixo()
    {
        const string html = """
            <a class="rf-btn" href="#" id="form0:j_id76" name="form0:j_id76" title="Pesquisar">
              <span>Pesquisar</span></a>
            """;

        SerNovaSolicitacaoService.BotaoPesquisarPaciente(html).Should().Be("form0:j_id76");
        SerNovaSolicitacaoService.BotaoPesquisarPaciente(html.Replace("j_id76", "j_id99"))
            .Should().Be("form0:j_id99", "j_id é posicional e muda quando a SES-RJ recompila");
    }

    [Fact]
    public void Sem_painel_devolve_lista_vazia()
    {
        SerNovaSolicitacaoService.CamposDoPaciente("<html><body><form id=\"form0\"></form></body></html>")
            .Should().BeEmpty();
    }
}
