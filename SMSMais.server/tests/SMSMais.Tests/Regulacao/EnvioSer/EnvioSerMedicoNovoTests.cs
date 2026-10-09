using Microsoft.Extensions.Logging.Abstractions;

using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Integracoes.SerWeb;
using SMSMais.Core.Regulacao.EnvioSer;
using SMSMais.Core.Ser.Criacao;

namespace SMSMais.Tests.Regulacao.EnvioSer;

/// <summary>
/// "Autorizo cadastrar no SER": o modal "Adicionar Médico" da tela de criação, dirigido pelo motor
/// (ADR-0065, complemento de 08/10/2026). A marcação é a da captura real do SER
/// (<c>Automais.SER/capturas/criar_aba_editar.html</c>), reduzida. O que prendem: o Gravar é o DO
/// MODAL (nunca o de form0) e vai pelo caminho de escrita nomeado; a especialidade é escolhida pelo
/// rótulo depois de abrir o modal; e "escrita acionada" só vira verdade no POST do Gravar.
/// </summary>
public class EnvioSerMedicoNovoTests
{
    private static string Pagina(params string[] medicos) =>
        "<html><body>"
        + "<form id=\"form0\" name=\"form0\" method=\"post\" action=\"/ser/pages/consultas-exames/solicitacao/solicitar-consulta-editar.seam\">"
        + "<input type=\"hidden\" name=\"form0\" value=\"form0\" />"
        + "<a href=\"#\" id=\"form0:pesquisar\" title=\"Pesquisar\">Pesquisar</a>"
        + "<select name=\"form0:comboTipoRecurso\"><option value=\"CONSULTA\">CONSULTA</option></select>"
        + "<select name=\"form0:medicoResp\" size=\"1\"><option value=\"\">Selecione</option>"
        + string.Concat(medicos.Select((m, i) => $"<option value=\"{i}\">{m}</option>"))
        + "</select>"
        + "<a href=\"#\" id=\"form0:addMedico\" name=\"form0:addMedico\" onclick=\"A4J.AJAX.Submit('form0',event,{'similarityGroupingId':'form0:addMedico','parameters':{'form0:addMedico':'form0:addMedico'} } );return false;\" title=\"Adicionar m&eacute;dico\"><img src=\"/ser/images/ser/ico_Incluir_On.gif\" /></a>"
        + "<a href=\"#\" id=\"form0:j_id313\" title=\"Gravar\" onclick=\"A4J.AJAX.Submit('form0',event,{});return false;\">Gravar</a>"
        + "<input type=\"hidden\" name=\"javax.faces.ViewState\" value=\"j_id6\" />"
        + "</form>"
        + "<form id=\"formModalAdicionarMedico\" name=\"formModalAdicionarMedico\" method=\"post\" action=\"/ser/pages/consultas-exames/solicitacao/solicitar-consulta-editar.seam\">"
        + "<input type=\"hidden\" name=\"formModalAdicionarMedico\" value=\"formModalAdicionarMedico\" />"
        + "<input id=\"formModalAdicionarMedico:aplicInicial\" name=\"formModalAdicionarMedico:aplicInicial\" type=\"text\" value=\"\" />"
        + "<input id=\"formModalAdicionarMedico:txtNome\" type=\"text\" name=\"formModalAdicionarMedico:txtNome\" />"
        + "<select name=\"formModalAdicionarMedico:j_id345\" size=\"1\"><option value=\"CNS\">CNS</option><option value=\"RG\">RG</option>"
        + "<option value=\"CRM\">CRM</option><option value=\"CPF\">CPF</option></select>"
        + "<input id=\"formModalAdicionarMedico:txtDocumento\" type=\"text\" name=\"formModalAdicionarMedico:txtDocumento\" />"
        + "<select name=\"formModalAdicionarMedico:j_id349\" size=\"1\"><option value=\"936\">OFTALMOLOGIA - PALPEBRAS E VIAS LACRIMAIS</option>"
        + "<option value=\"937\">ONCOLOGIA</option><option value=\"943\">ONCOLOGIA - MASTOLOGIA</option></select>"
        + "<input id=\"formModalAdicionarMedico:j_id352\" name=\"formModalAdicionarMedico:j_id352\" onclick=\"A4J.AJAX.Submit('formModalAdicionarMedico',event,{'similarityGroupingId':'formModalAdicionarMedico:j_id352'});return false;\" value=\"Gravar\" type=\"button\" />"
        + "<input id=\"formModalAdicionarMedico:j_id353\" name=\"formModalAdicionarMedico:j_id353\" value=\"Cancelar\" type=\"button\" />"
        + "<input type=\"hidden\" name=\"javax.faces.ViewState\" value=\"j_id6\" />"
        + "</form></body></html>";

    private sealed class TransporteFalso(string pagina, string depoisDoGravar) : ITransporteTelaCriacao
    {
        public List<(string Form, IReadOnlyDictionary<string, string> Extras)> Leituras { get; } = [];
        public List<(string Form, IReadOnlyDictionary<string, string> Extras, string Operacao)> Escritas { get; } = [];

        public Task<string> AbrirTelaAsync(string caminho, CancellationToken ct) =>
            Task.FromResult(Escritas.Count > 0 ? depoisDoGravar : pagina);

        public Task<string> SubmeterLeituraAsync(
            string html, string formId, IReadOnlyDictionary<string, string> extras, string? viewState, CancellationToken ct)
        {
            // A trava de somente-leitura real roda aqui: abrir o modal não pode parecer escrita.
            SerWebSessao.GarantirLeitura(extras, SerHtmlParser.Documento(html));
            Leituras.Add((formId, extras));
            return Task.FromResult(pagina);
        }

        public Task<string> SubmeterEscritaAsync(
            string html, string formId, IReadOnlyDictionary<string, string> extras, string? viewState,
            string operacao, CancellationToken ct)
        {
            Escritas.Add((formId, extras, operacao));
            return Task.FromResult(depoisDoGravar);
        }

        public Task<string> EnviarArquivoAsync(
            string html, string formId, string campoArquivo, string nomeArquivo, string contentType, byte[] conteudo,
            IReadOnlyDictionary<string, string> parametrosUrl, string? viewState, string operacao, CancellationToken ct) =>
            throw new NotSupportedException();
    }

    [Fact]
    public void Le_o_modal_pelos_combos_sem_depender_do_id()
    {
        var modal = SerCriacaoSolicitacao.ModalMedicoDe(SerHtmlParser.Documento(Pagina("A B")));

        modal.Should().NotBeNull();
        modal!.CampoTipoDocumento.Should().Be("formModalAdicionarMedico:j_id345", "é o combo que oferece CRM");
        modal.CampoEspecialidade.Should().Be("formModalAdicionarMedico:j_id349");
        modal.Especialidades.Select(o => o.Rotulo).Should().Contain("ONCOLOGIA");
    }

    [Fact]
    public async Task Cadastra_pelo_Gravar_do_modal_com_a_especialidade_escolhida_pelo_rotulo()
    {
        var transporte = new TransporteFalso(
            Pagina("ANA SOUZA"),
            depoisDoGravar: Pagina("ANA SOUZA", "RAFAELA ROCHA BEDRAN"));
        var motor = new SerCriacaoSolicitacao(transporte, PerfilTelaCriacao.Ser, NullLogger.Instance);
        await motor.AbrirAsync(CancellationToken.None);
        motor.EscritaAcionada.Should().BeFalse();

        await motor.CadastrarMedicoAsync(
            "RAFAELA ROCHA BEDRAN", "CRM", "5201216066",
            opcoes => opcoes.FirstOrDefault(o => o.Rotulo == "ONCOLOGIA"),
            "cadastrar o médico RAFAELA ROCHA BEDRAN (teste)", CancellationToken.None);

        transporte.Leituras.Should().Contain(l => l.Form == "form0" && l.Extras.ContainsKey(SerCriacaoSolicitacao.AbrirModalMedico),
            "o ícone abre o modal como o clique — e passa pela trava de somente-leitura");
        var escrita = transporte.Escritas.Should().ContainSingle().Subject;
        escrita.Form.Should().Be(SerCriacaoSolicitacao.FormModalMedico, "o Gravar é o DO MODAL, nunca o de form0");
        escrita.Extras.Should().ContainKey("formModalAdicionarMedico:j_id352");
        escrita.Extras.Should().NotContainKey("form0:j_id313");
        escrita.Extras[SerCriacaoSolicitacao.CampoModalNome].Should().Be("RAFAELA ROCHA BEDRAN");
        escrita.Extras["formModalAdicionarMedico:j_id345"].Should().Be("CRM");
        escrita.Extras[SerCriacaoSolicitacao.CampoModalDocumento].Should().Be("5201216066");
        escrita.Extras["formModalAdicionarMedico:j_id349"].Should().Be("937");
        escrita.Extras["AJAXREQUEST"].Should().Be("formModalAdicionarMedico");
        motor.EscritaAcionada.Should().BeTrue();
        motor.Combo(SerCriacaoSolicitacao.CampoMedico).Select(o => o.Rotulo).Should().Contain("RAFAELA ROCHA BEDRAN");
    }

    [Fact]
    public async Task Especialidade_fora_da_lista_para_antes_de_escrever()
    {
        var transporte = new TransporteFalso(Pagina("ANA SOUZA"), Pagina("ANA SOUZA"));
        var motor = new SerCriacaoSolicitacao(transporte, PerfilTelaCriacao.Ser, NullLogger.Instance);
        await motor.AbrirAsync(CancellationToken.None);

        var acao = () => motor.CadastrarMedicoAsync(
            "RAFAELA ROCHA BEDRAN", null, null, _ => null, "teste", CancellationToken.None);

        await acao.Should().ThrowAsync<ValidacaoException>();
        transporte.Escritas.Should().BeEmpty();
        motor.EscritaAcionada.Should().BeFalse("nada saiu — o regulador pode tentar de novo");
    }

    [Theory]
    [InlineData("ONCOLOGISTA", "ONCOLOGIA")]
    [InlineData("oncologia", "ONCOLOGIA")]
    [InlineData("PEDIATRA", "PEDIATRIA")]
    [InlineData("CLÍNICO GERAL", "CLÍNICA GERAL")]
    [InlineData("CARDIOLOGISTA", "CARDIOLOGIA")]
    [InlineData("ORTOPEDISTA", "ORTOPEDIA E TRAUMATOLOGIA")]
    [InlineData("ASTRONAUTA", null)]
    [InlineData(null, null)]
    public void Sugere_a_especialidade_da_lista_pelo_que_a_unidade_escreveu(string? pedida, string? esperada)
    {
        string[] lista =
        [
            "CARDIOLOGIA", "CARDIOLOGIA - INTERVENCIONISTA", "CLÍNICA GERAL", "ONCOLOGIA - MASTOLOGIA", "ONCOLOGIA",
            "ORTOPEDIA E TRAUMATOLOGIA", "ORTOPEDIA/TRAUMATOLOGIA - GERAIS", "PEDIATRIA", "PNEUMOLOGIA",
        ];

        RegulacaoEnvioSerService.SugerirEspecialidade(pedida, lista).Should().Be(esperada);
    }
}
