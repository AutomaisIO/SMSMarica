using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SMSMais.Core.Cidadao.Push;
using SMSMais.Core.Cidadao.Push.Dtos;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Integracoes.Credenciais;
using SMSMais.Data;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Cidadao.Push;

/// <summary>
/// A credencial do Firebase vista pelo serviço, sem banco nem rede: o "Testar" de Integrações (token
/// + envio de validação, nunca lança) e a credencial que não decifra neste servidor (cifrada com outra
/// chave do Data Protection — banco copiado entre ambientes, bancada), que no Testar vira resposta e
/// no envio vira 400 com código próprio, em vez de 500.
/// </summary>
public sealed class CredencialPushCidadaoTests
{
    private static readonly string ContaJson = ContaServicoFcmFabrica.Gerar().Json;

    private readonly IIntegracaoCredencialService _credenciais = Substitute.For<IIntegracaoCredencialService>();
    private readonly IClienteFcm _fcm = Substitute.For<IClienteFcm>();

    public CredencialPushCidadaoTests()
    {
        _credenciais.ObterContextoAsync(PushCidadaoService.ProvedorFcm, Arg.Any<CancellationToken>())
            .Returns(new IntegracaoCredencialContexto(PushCidadaoService.ProvedorFcm, null, ContaJson, null,
                "{\"projectId\":\"projeto-teste\"}", true));
        _fcm.ObterAccessTokenAsync(Arg.Any<ContaServicoFcm>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns("ya29.acesso");
    }

    // Conexão nunca é aberta: o Testar não consulta o banco e o envio para antes, na credencial.
    private PushCidadaoService Servico() => new(
        new SmsMaisDbContext(new DbContextOptionsBuilder<SmsMaisDbContext>()
            .UseNpgsql("Host=localhost;Database=nao-usado")
            .Options),
        _credenciais, _fcm, new UsuarioAtualAccessorFake(), NullLogger<PushCidadaoService>.Instance);

    // ---------- Testar ----------

    [Fact]
    public async Task Testar_pede_token_novo_e_faz_o_envio_de_validacao_com_ele()
    {
        _fcm.ValidarEnvioAsync(Arg.Any<ContaServicoFcm>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ValidacaoEnvioFcm(true,
                "Credencial válida: o servidor autenticou e o Firebase aceita envios do projeto projeto-teste.", null));

        var r = await Servico().TestarCredencialAsync();

        Assert.True(r.Ok);
        Assert.Equal("projeto-teste", r.ProjectId);
        Assert.Equal("Credencial válida: o servidor autenticou e o Firebase aceita envios do projeto projeto-teste.", r.Mensagem);
        await _fcm.Received(1).ObterAccessTokenAsync(Arg.Any<ContaServicoFcm>(), true, Arg.Any<CancellationToken>());
        await _fcm.Received(1).ValidarEnvioAsync(
            Arg.Is<ContaServicoFcm>(c => c.ProjectId == "projeto-teste"), "ya29.acesso", Arg.Any<CancellationToken>());
        await _fcm.DidNotReceiveWithAnyArgs().EnviarAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task Testar_com_token_ok_mas_envio_recusado_nao_da_ok()
    {
        // Era o buraco: a API do FCM desligada no Google Cloud deixa sair o token, e o Testar dizia "válida".
        _fcm.ValidarEnvioAsync(Arg.Any<ContaServicoFcm>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ValidacaoEnvioFcm(false,
                "A API Firebase Cloud Messaging (V1) está desligada no projeto projeto-teste. Ative no Google Cloud (APIs e serviços) e teste de novo.",
                "FCM validate_only HTTP 403 SERVICE_DISABLED"));

        var r = await Servico().TestarCredencialAsync();

        Assert.False(r.Ok);
        Assert.Equal("projeto-teste", r.ProjectId);
        Assert.StartsWith("A API Firebase Cloud Messaging (V1) está desligada", r.Mensagem);
    }

    [Fact]
    public async Task Testar_com_token_recusado_nem_chega_ao_envio_de_validacao()
    {
        _fcm.ObterAccessTokenAsync(Arg.Any<ContaServicoFcm>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Throws(new FalhaTokenFcmException("invalid_grant: Invalid JWT Signature.",
                DesfechoEnvioFcm.Credencial("token OAuth2 HTTP 400")));

        var r = await Servico().TestarCredencialAsync();

        Assert.False(r.Ok);
        Assert.Equal("O Google recusou a credencial: invalid_grant: Invalid JWT Signature.", r.Mensagem);
        await _fcm.DidNotReceiveWithAnyArgs().ValidarEnvioAsync(default!, default!, default);
    }

    // ---------- credencial que não decifra ----------

    // Os dois jeitos que o ProtetorSegredos (Data Protection) falha: chave de outro ambiente, e valor que
    // nem é texto cifrado (o Unprotect de string embrulha o FormatException do Base64).
    public static TheoryData<Exception> ErrosDeDecifrar() => new()
    {
        new CryptographicException("The key {0b8f8c5e-0000-0000-0000-000000000000} was not found in the key ring."),
        new CryptographicException("An error occurred during a cryptographic operation.",
            new FormatException("The input is not a valid Base-64 string.")),
    };

    [Theory]
    [MemberData(nameof(ErrosDeDecifrar))]
    public async Task Testar_com_credencial_que_nao_decifra_responde_em_vez_de_lancar(Exception erro)
    {
        _credenciais.ObterContextoAsync(PushCidadaoService.ProvedorFcm, Arg.Any<CancellationToken>()).Throws(erro);

        var r = await Servico().TestarCredencialAsync();

        Assert.False(r.Ok);
        Assert.Null(r.ProjectId);
        Assert.Equal("A credencial gravada não pode ser lida por este servidor (foi cifrada em outro ambiente). Use Limpar e cole o JSON de novo.", r.Mensagem);
        await _fcm.DidNotReceiveWithAnyArgs().ObterAccessTokenAsync(default!, default, default);
    }

    [Theory]
    [MemberData(nameof(ErrosDeDecifrar))]
    public async Task Enviar_com_credencial_que_nao_decifra_e_validacao_com_codigo_proprio(Exception erro)
    {
        _credenciais.ObterContextoAsync(PushCidadaoService.ProvedorFcm, Arg.Any<CancellationToken>()).Throws(erro);

        var ex = await Assert.ThrowsAsync<ValidacaoException>(() => Servico().EnviarAsync(Guid.NewGuid(),
            new EnviarNotificacaoAppRequest("Aviso", "Mensagem", null)));

        Assert.Equal(new[] { "push.credencial_ilegivel" }, ex.Erros.Keys);
        Assert.Equal("A credencial gravada não pode ser lida por este servidor (foi cifrada em outro ambiente). Use Limpar e cole o JSON de novo.", ex.Message);
        await _fcm.DidNotReceiveWithAnyArgs().ObterAccessTokenAsync(default!, default, default);
    }

    [Fact]
    public async Task Outra_falha_ao_ler_o_cofre_nao_vira_credencial_ilegivel()
    {
        // Banco fora, por exemplo: é erro de verdade (500 com código), não "cole o JSON de novo".
        var erro = new InvalidOperationException("banco indisponível");
        _credenciais.ObterContextoAsync(PushCidadaoService.ProvedorFcm, Arg.Any<CancellationToken>()).Throws(erro);

        Assert.Same(erro, await Assert.ThrowsAsync<InvalidOperationException>(() => Servico().TestarCredencialAsync()));
        Assert.Same(erro, await Assert.ThrowsAsync<InvalidOperationException>(() => Servico().EnviarAsync(Guid.NewGuid(),
            new EnviarNotificacaoAppRequest("Aviso", "Mensagem", null))));
    }
}
