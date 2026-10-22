using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SMSMais.Core.Cidadao.Entrada;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Integracoes.Credenciais;
using SMSMais.Core.Integracoes.Credenciais.Dtos;
using SMSMais.Core.Integracoes.EsusPec;
using SMSMais.Core.Pacientes.Enriquecimento;

namespace SMSMais.Tests.Cidadao;

/// <summary>
/// O e-SUS na entrada do cidadão (ADR-0078 §4), com o PEC dublado: só celular da MESMA pessoa (mesmo
/// nascimento, viva, ativa); conta em uso, janela da madrugada ou login recusado viram "indisponível" —
/// e o login recusado pausa as próximas tentativas em vez de bloquear a conta no PEC.
/// </summary>
[Collection(nameof(TelefonesEsusNaEntradaTests))]
[CollectionDefinition(nameof(TelefonesEsusNaEntradaTests), DisableParallelization = true)]
public sealed class TelefonesEsusNaEntradaTests : IDisposable
{
    private static readonly DateOnly Nascimento = new(1980, 3, 10);

    private readonly IIntegracaoCredencialService _credenciais = Substitute.For<IIntegracaoCredencialService>();
    private readonly IConsultaFichaEsusPec _pec = Substitute.For<IConsultaFichaEsusPec>();
    private readonly string _cpf = Random.Shared.NextInt64(10_000_000_000, 99_999_999_999).ToString();

    public TelefonesEsusNaEntradaTests()
    {
        TelefonesEsusNaEntrada.ReiniciarPausa();
        // Janela da rotina da madrugada num horário que nunca é "agora": 00:00–00:00 é vazia.
        Credencial("{\"janelaInicio\":\"00:00\",\"janelaFim\":\"00:00\"}");
    }

    public void Dispose() => TelefonesEsusNaEntrada.ReiniciarPausa();

    private void Credencial(string parametros) =>
        _credenciais.ObterContextoAsync(CorrecaoTelefoneEsusService.Provedor, Arg.Any<CancellationToken>())
            .Returns(new IntegracaoCredencialContexto(CorrecaoTelefoneEsusService.Provedor, "conta-plataforma", "senha", null, parametros, true));

    private TelefonesEsusNaEntrada Servico() =>
        new(_credenciais, _pec, new MemoryCache(new MemoryCacheOptions()), NullLogger<TelefonesEsusNaEntrada>.Instance);

    private static FichaCidadaoEsusPec Ficha(
        string? nascimento = "1980-03-10", bool faleceu = false, bool ativo = true,
        string? celular = "(21) 98888-7777", string? residencial = "21 2633-0000", string? contato = "21966665555") =>
        new("1", null, null, "MARIA DA SILVA", null, nascimento, "FEMININO", null, null, null, null,
            celular, residencial, contato, null, null, null, null, null, null, null, null, faleceu, ativo, null);

    private void PecDevolve(FichaCidadaoEsusPec? ficha, bool outraSessao = false) =>
        _pec.ConsultarAsync(Arg.Any<ContaEsusPec>(), _cpf, Arg.Any<CancellationToken>())
            .Returns(new ResultadoConsultaEsusPec(outraSessao, ficha));

    [Fact]
    public async Task Devolve_so_os_celulares_da_mesma_pessoa_sem_forcar_a_entrada()
    {
        PecDevolve(Ficha());

        var numeros = await Servico().BuscarAsync(new PessoaNaEntrada(_cpf, Nascimento));

        numeros.Should().Equal("5521988887777", "5521966665555");
        await _pec.Received(1).ConsultarAsync(
            Arg.Is<ContaEsusPec>(c => !c.Forcar && c.Usuario == "conta-plataforma"), _cpf, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("1981-03-10", false, true)]
    [InlineData("1980-03-10", true, true)]
    [InlineData("1980-03-10", false, false)]
    public async Task Outra_pessoa_obito_ou_inativo_nao_dao_numero(string nascimento, bool faleceu, bool ativo)
    {
        PecDevolve(Ficha(nascimento, faleceu, ativo));

        (await Servico().BuscarAsync(new PessoaNaEntrada(_cpf, Nascimento))).Should().BeEmpty();
    }

    [Fact]
    public async Task Cidadao_que_o_esus_nao_conhece_nao_da_numero()
    {
        PecDevolve(null);

        (await Servico().BuscarAsync(new PessoaNaEntrada(_cpf, Nascimento))).Should().BeEmpty();
    }

    [Fact]
    public async Task Conta_em_uso_vira_indisponivel()
    {
        PecDevolve(null, outraSessao: true);

        var acao = () => Servico().BuscarAsync(new PessoaNaEntrada(_cpf, Nascimento));

        (await acao.Should().ThrowAsync<ConflitoException>()).Which.Codigo.Should().Be("esus.indisponivel");
    }

    [Fact]
    public async Task Na_janela_da_madrugada_nem_tenta()
    {
        Credencial("{\"janelaInicio\":\"00:00\",\"janelaFim\":\"23:59\"}");
        PecDevolve(Ficha());

        var acao = () => Servico().BuscarAsync(new PessoaNaEntrada(_cpf, Nascimento));

        (await acao.Should().ThrowAsync<ConflitoException>()).Which.Codigo.Should().Be("esus.indisponivel");
        await _pec.DidNotReceiveWithAnyArgs().ConsultarAsync(default!, default!, default);
    }

    [Fact]
    public async Task Login_recusado_pausa_as_proximas_consultas()
    {
        _pec.ConsultarAsync(Arg.Any<ContaEsusPec>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Throws(new ErroEsusPec("Login: usuário ou senha inválidos."));
        var servico = Servico();

        var primeira = () => servico.BuscarAsync(new PessoaNaEntrada(_cpf, Nascimento));
        await primeira.Should().ThrowAsync<ConflitoException>();

        var outroCpf = Random.Shared.NextInt64(10_000_000_000, 99_999_999_999).ToString();
        var segunda = () => servico.BuscarAsync(new PessoaNaEntrada(outroCpf, Nascimento));
        (await segunda.Should().ThrowAsync<ConflitoException>()).Which.Codigo.Should().Be("esus.indisponivel");
        await _pec.Received(1).ConsultarAsync(Arg.Any<ContaEsusPec>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task O_mesmo_cpf_de_novo_usa_o_resultado_guardado()
    {
        PecDevolve(Ficha());
        var servico = Servico();

        await servico.BuscarAsync(new PessoaNaEntrada(_cpf, Nascimento));
        await servico.BuscarAsync(new PessoaNaEntrada(_cpf, Nascimento));

        await _pec.Received(1).ConsultarAsync(Arg.Any<ContaEsusPec>(), _cpf, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Disponivel_so_com_a_conta_da_plataforma_gravada_e_ativa()
    {
        _credenciais.ObterAsync(CorrecaoTelefoneEsusService.Provedor, Arg.Any<CancellationToken>())
            .Returns(new IntegracaoCredencialDto(CorrecaoTelefoneEsusService.Provedor, "e-SUS PEC", true, true, null, null, false));

        (await Servico().DisponivelAsync()).Should().BeFalse();

        _credenciais.ObterAsync(CorrecaoTelefoneEsusService.Provedor, Arg.Any<CancellationToken>())
            .Returns(new IntegracaoCredencialDto(CorrecaoTelefoneEsusService.Provedor, "e-SUS PEC", true, true, null, null, true));

        (await Servico().DisponivelAsync()).Should().BeTrue();
    }
}
