using FluentAssertions;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Integracoes.Cadastro;
using SMSMais.Core.Ser;
using SMSMais.Core.Ser.Dtos;

namespace SMSMais.Tests.Integracoes;

/// <summary>
/// O CADSUS pela porta do SER — a tradução do painel do SER para o mesmo shape que o CADSUS do
/// SISREG devolve.
///
/// <para>O que está sob teste é o que separa "trocar de porta" de "criar um segundo caminho de
/// importação": a identidade tem de sair daqui idêntica à do <c>cadweb50</c>, senão a régua de
/// dedup do hub (CPF com DV, depois CNS) passaria a se comportar diferente conforme a fonte.</para>
///
/// <para>O HTML é recorte da captura real de 10/08/2026 com os dados trocados.</para>
/// </summary>
public class SerCadastroPacienteTests
{
    private const string Painel = """
        <html><body><form id="form0">
          <div id="form0:painelDadosDoPaciente"><span><table><tbody><tr>
            <td><label>Nome<span class="required">*</span></label><br />
                <input id="form0:nome" name="form0:nome" type="text" disabled="disabled" value="MARLI ROSA DA SILVA" /></td>
            <td><label>CPF</label><br />
                <input id="form0:cpf" name="form0:cpf" type="text" disabled="disabled" value="027.339.137-27" /></td>
            <td><label>CNS</label><br />
                <input id="form0:cns" name="form0:cns" type="text" disabled="disabled" value="702802144727663" /></td>
            <td><label>Data de Nascimento<span class="required">*</span></label><br />
                <input id="form0:dataNascimento" name="form0:dataNascimento" type="text" disabled="disabled" value="29/12/1965" /></td>
            <td><label>Sexo<span class="required">*</span></label><br />
                <select id="form0:sexo" name="form0:sexo" disabled="disabled">
                  <option value="M">Masculino</option><option value="F" selected="selected">Feminino</option>
                </select></td>
            <td><label>Nome da Mãe<span class="required">*</span></label><br />
                <input id="form0:nomeMae" name="form0:nomeMae" type="text" disabled="disabled" value="NAIR SUDANO PEREIRA" /></td>
            <td><label><i class="fas fa-mobile-alt"></i>Telefone WhatsApp<span class="required">*</span></label><br />
                <input class="input telefone" name="form0:j_id178" type="text" value="(21) 97738-1014" /></td>
          </tr></tbody></table></span></div>
        </form></body></html>
        """;

    private static ISerCadastroPacienteService Servico(string html) =>
        new SerCadastroPacienteService(new SerFalso(html));

    [Fact]
    public async Task Identidade_sai_no_mesmo_shape_do_cadsus_do_sisreg()
    {
        var r = await Servico(Painel).ConsultarPorCnsAsync("702802144727663");

        r.Nome.Should().Be("MARLI ROSA DA SILVA");
        r.Cns.Should().Be("702802144727663");
        r.NomeMae.Should().Be("NAIR SUDANO PEREIRA");
        r.DataNascimento.Should().Be(new DateOnly(1965, 12, 29));
    }

    /// <summary>
    /// O SER mostra o CPF com máscara; o hub ancora por dígitos. Deixar a máscara passar faria o
    /// <c>ObterPorCpfAsync</c> não achar ninguém e cadastrar de novo o mesmo cidadão — que é
    /// exatamente a duplicata que a régua de dedup existe para evitar.
    /// </summary>
    [Fact]
    public async Task Cpf_vem_so_com_digitos()
    {
        var r = await Servico(Painel).ConsultarPorCnsAsync("702802144727663");

        r.Cpf.Should().Be("02733913727");
    }

    /// <summary>
    /// O SISREG devolve "Masculino"/"Feminino" por extenso e o SER manda a inicial no
    /// <c>value</c> do select. Sem esta tradução, o importador cairia no <c>else</c> e gravaria
    /// todo mundo como NaoInformado — sem erro nenhum.
    /// </summary>
    [Fact]
    public async Task Sexo_vira_o_vocabulario_do_hub()
    {
        (await Servico(Painel).ConsultarPorCnsAsync("702802144727663")).Sexo.Should().Be("Feminino");

        var masculino = Painel
            .Replace("<option value=\"F\" selected=\"selected\">Feminino</option>", "<option value=\"F\">Feminino</option>")
            .Replace("<option value=\"M\">Masculino</option>", "<option value=\"M\" selected=\"selected\">Masculino</option>");
        (await Servico(masculino).ConsultarPorCnsAsync("702802144727663")).Sexo.Should().Be("Masculino");
    }

    /// <summary>
    /// Pesquisando por CPF, o painel pode não repetir o número perguntado. O que não pode é o
    /// registro voltar sem a chave que o chamador já tinha na mão.
    /// </summary>
    [Fact]
    public async Task Documento_perguntado_preenche_o_que_o_painel_omite()
    {
        var semCns = Painel.Replace("value=\"702802144727663\"", "value=\"\"");

        var r = await Servico(semCns).ConsultarPorCpfAsync("027.339.137-27");

        r.Cpf.Should().Be("02733913727");
        r.Cns.Should().BeEmpty("o painel não trouxe CNS e o que foi perguntado era um CPF");
    }

    /// <summary>
    /// Painel vazio no SER é "não está no CADSUS", não "a fonte caiu" — e a distinção decide a
    /// causa da pendência: uma se resolve informando o CPF, a outra tentando de novo mais tarde.
    /// </summary>
    [Fact]
    public async Task Painel_vazio_e_nao_encontrado()
    {
        var vazio = "<html><body><form id=\"form0\"></form></body></html>";

        await FluentActions.Awaiting(() => Servico(vazio).ConsultarPorCnsAsync("702802144727663"))
            .Should().ThrowAsync<NaoEncontradoException>();
    }

    /// <summary>
    /// Painel presente mas SEM identidade = layout mudou. Devolver o registro em branco criaria um
    /// paciente "SEM NOME" no hub — lixo permanente na identidade do cidadão.
    /// </summary>
    [Fact]
    public async Task Painel_sem_identidade_falha_alto()
    {
        var mutilado = """
            <html><body><form id="form0"><div id="form0:painelDadosDoPaciente">
              <input id="form0:nomeSocial" name="form0:nomeSocial" type="text" value="" />
            </div></form></body></html>
            """;

        await FluentActions.Awaiting(() => Servico(mutilado).ConsultarPorCnsAsync("702802144727663"))
            .Should().ThrowAsync<ValidacaoException>();
    }

    [Theory]
    [InlineData("123")]
    [InlineData("7028021447276631")]
    public async Task Cns_fora_de_15_digitos_nao_chega_a_consultar(string cns)
    {
        await FluentActions.Awaiting(() => Servico(Painel).ConsultarPorCnsAsync(cns))
            .Should().ThrowAsync<ValidacaoException>();
    }

    /// <summary>
    /// SER falso que devolve os campos do HTML dado — parseados pelo MESMO extrator do motor real
    /// (<see cref="SerNovaSolicitacaoService.CamposDoPaciente"/>). Fingir a lista de campos à mão
    /// testaria o mapeador contra uma fantasia; assim ele é testado contra o que a tela entrega.
    /// </summary>
    private sealed class SerFalso(string html) : ISerNovaSolicitacaoService
    {
        public Task<SerPacienteEncontradoDto> PesquisarPacienteAsync(
            string cnsOuCpf, CancellationToken cancellationToken)
        {
            var campos = SerNovaSolicitacaoService.CamposDoPaciente(html);
            return Task.FromResult(new SerPacienteEncontradoDto(
                campos.Count > 0, SerNovaSolicitacaoService.Avisos(html), campos));
        }

        public Task<SerFormularioNovaDto> ObterFormularioAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<SerOpcaoDto>> ListarRecursosAsync(
            string tipo, bool ambulatorioEstadual, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<SerCampoDinamicoDto>> ObterCamposDinamicosAsync(
            string tipo, string recurso, bool ambulatorioEstadual, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<SerCidSugestoesDto> SugerirCidsAsync(
            string tipo, string recurso, bool ambulatorioEstadual, string termo,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<SerAssinaturaCidDto> MedirAssinaturasCidAsync(
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<SerCidDto>> CopiarListaCidAsync(
            string tipo, string recurso, bool ambulatorioEstadual, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
