using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SMSMais.Api.Hubs;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Core.Identidade.Dtos;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Tests.Rastreamento;

/// <summary>
/// Quem entra no hub de rastreamento: só a equipe com Consulta em Rastreamento. O token do cidadão
/// é do mesmo esquema JWT e passava no <c>[Authorize]</c> — até 23/10/2026 caía no grupo do painel.
/// </summary>
public class RastreamentoHubTests
{
    private readonly IIdentidadeService _identidade = Substitute.For<IIdentidadeService>();
    private readonly IGroupManager _grupos = Substitute.For<IGroupManager>();

    [Fact]
    public async Task Equipe_com_consulta_em_rastreamento_entra_no_painel()
    {
        var usuarioId = Guid.NewGuid();
        Permite(usuarioId, new PermissaoModuloDto(ModuloPermissao.Rastreamento, AcoesPermissao.Consulta));
        var contexto = Contexto(new Claim("sub", usuarioId.ToString()));

        await Conectar(contexto);

        contexto.Abortado.Should().BeFalse();
        await _grupos.Received(1).AddToGroupAsync(contexto.ConnectionId, RastreamentoHub.GrupoPainel, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Token_de_cidadao_e_derrubado_sem_consultar_permissoes()
    {
        var contexto = Contexto(new Claim("sub", Guid.NewGuid().ToString()), new Claim("tipo", "cidadao"));

        await Conectar(contexto);

        contexto.Abortado.Should().BeTrue();
        await _grupos.DidNotReceiveWithAnyArgs().AddToGroupAsync(default!, default!, default);
        await _identidade.DidNotReceiveWithAnyArgs().ObterPermissoesResolvidasAsync(default, default);
    }

    [Fact]
    public async Task Equipe_sem_rastreamento_e_derrubada()
    {
        var usuarioId = Guid.NewGuid();
        Permite(usuarioId, new PermissaoModuloDto(ModuloPermissao.Translados, AcoesPermissao.Consulta));
        var contexto = Contexto(new Claim("sub", usuarioId.ToString()));

        await Conectar(contexto);

        contexto.Abortado.Should().BeTrue();
        await _grupos.DidNotReceiveWithAnyArgs().AddToGroupAsync(default!, default!, default);
    }

    [Fact]
    public async Task Rastreamento_sem_consulta_e_derrubado()
    {
        var usuarioId = Guid.NewGuid();
        Permite(usuarioId, new PermissaoModuloDto(ModuloPermissao.Rastreamento, AcoesPermissao.Inclusao));
        var contexto = Contexto(new Claim("sub", usuarioId.ToString()));

        await Conectar(contexto);

        contexto.Abortado.Should().BeTrue();
    }

    [Fact]
    public async Task Sub_que_nao_e_usuario_da_equipe_e_derrubado()
    {
        var id = Guid.NewGuid();
        _identidade.ObterPermissoesResolvidasAsync(id, Arg.Any<CancellationToken>())
            .ThrowsAsync(new NaoEncontradoException(nameof(Usuario), id));
        var contexto = Contexto(new Claim("sub", id.ToString()));

        await Conectar(contexto);

        contexto.Abortado.Should().BeTrue();
    }

    private void Permite(Guid usuarioId, params PermissaoModuloDto[] resolvidas) =>
        _identidade.ObterPermissoesResolvidasAsync(usuarioId, Arg.Any<CancellationToken>())
            .Returns(new PermissoesResolvidasDto([], [], resolvidas));

    private async Task Conectar(ContextoFalso contexto)
    {
        var hub = new RastreamentoHub(_identidade) { Context = contexto, Groups = _grupos };
        await hub.OnConnectedAsync();
    }

    private static ContextoFalso Contexto(params Claim[] claims) =>
        new(new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Bearer")));

    private sealed class ContextoFalso(ClaimsPrincipal user) : HubCallerContext
    {
        public bool Abortado { get; private set; }

        public override string ConnectionId { get; } = Guid.NewGuid().ToString("N");
        public override string? UserIdentifier => null;
        public override ClaimsPrincipal? User { get; } = user;
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override CancellationToken ConnectionAborted => CancellationToken.None;
        public override void Abort() => Abortado = true;
    }
}
