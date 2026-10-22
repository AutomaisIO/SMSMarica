using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SMSMais.Core.Cidadao.Push;
using SMSMais.Core.Cidadao.Push.Dtos;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Translado;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Tests.Translado;

/// <summary>
/// Os avisos do transporte pelo push: vão como aviso do SISTEMA (origem "transporte", sem operador),
/// abrem a tela do Transporte, levam só o primeiro nome e nunca derrubam o fluxo do transporte quando
/// o paciente não tem o app ou o push está desligado.
/// </summary>
public class AvisoAoPacientePorPushTests
{
    private readonly IPushCidadaoService _push = Substitute.For<IPushCidadaoService>();
    private readonly Guid _paciente = Guid.CreateVersion7();
    private readonly Guid _sessao = Guid.CreateVersion7();

    private AvisoAoPacientePorPush Aviso() => new(_push, NullLogger<AvisoAoPacientePorPush>.Instance);

    private void Entrega(int entregues) =>
        _push.EnviarDoSistemaAsync(default, default!, default!, default)
            .ReturnsForAnyArgs(new EnvioNotificacaoAppDto(Guid.CreateVersion7(), 1, entregues, []));

    [Fact]
    public async Task Pergunta_do_resgate_vai_como_aviso_do_transporte_e_abre_a_tela_do_transporte()
    {
        Entrega(1);

        var entregou = await Aviso().PerguntarSeDisponivelAsync(_paciente, "Ana", _sessao);

        Assert.True(entregou);
        await _push.Received(1).EnviarDoSistemaAsync(
            _paciente,
            Arg.Is<EnviarNotificacaoAppRequest>(r => r.Titulo == "Você já está disponível para retorno?"
                && r.Mensagem!.StartsWith("Olá, Ana!")
                && r.Rota == "/transporte"),
            PushCidadaoService.OrigemTransporte,
            Arg.Any<CancellationToken>());
        await _push.DidNotReceiveWithAnyArgs().EnviarAsync(default, default!, default);
    }

    [Theory]
    [InlineData(TrechoViagem.Ida, "chegou ao seu destino")]
    [InlineData(TrechoViagem.Resgate, "chegou em casa")]
    public async Task Pedido_de_avaliacao_diz_o_trecho_sem_dizer_onde(TrechoViagem trecho, string trechoNoTexto)
    {
        Entrega(1);

        await Aviso().PedirAvaliacaoAsync(_paciente, "Ana", _sessao, trecho, "João");

        await _push.Received(1).EnviarDoSistemaAsync(
            _paciente,
            Arg.Is<EnviarNotificacaoAppRequest>(r => r.Titulo == "Como foi o motorista?"
                && r.Mensagem!.Contains(trechoNoTexto)
                && !r.Mensagem.Contains("João")
                && r.Rota == "/transporte"),
            PushCidadaoService.OrigemTransporte,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sem_nome_a_saudacao_continua_de_pe()
    {
        Entrega(1);

        await Aviso().PerguntarSeDisponivelAsync(_paciente, "  ", _sessao);

        await _push.Received(1).EnviarDoSistemaAsync(
            _paciente, Arg.Is<EnviarNotificacaoAppRequest>(r => r.Mensagem!.StartsWith("Olá! ")),
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Nenhum_aparelho_aceitou_nao_conta_como_entregue()
    {
        Entrega(0);

        Assert.False(await Aviso().PerguntarSeDisponivelAsync(_paciente, "Ana", _sessao));
    }

    [Fact]
    public async Task Sem_app_ou_sem_push_configurado_o_aviso_fica_so_no_app_sem_erro()
    {
        _push.EnviarDoSistemaAsync(default, default!, default!, default).ThrowsAsyncForAnyArgs(
            new ConflitoException("push.sem_aparelho", "Este paciente não tem o app com as notificações ativas."));
        Assert.False(await Aviso().PerguntarSeDisponivelAsync(_paciente, "Ana", _sessao));

        _push.EnviarDoSistemaAsync(default, default!, default!, default).ThrowsAsyncForAnyArgs(
            new ValidacaoException("push.nao_configurado", "O envio de notificações não está configurado."));
        Assert.False(await Aviso().PedirAvaliacaoAsync(_paciente, "Ana", _sessao, TrechoViagem.Ida, null));
    }
}
