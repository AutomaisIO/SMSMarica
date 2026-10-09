using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SMSMais.Core.Cidadao;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Core.Identidade.Dtos;
using SMSMais.Core.Pacientes;
using SMSMais.Core.Pacientes.Dtos;
using SMSMais.Core.Sandbox;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Cidadao;

/// <summary>
/// "Entrar como paciente" do Sandbox. O que está sob teste é o ciclo de vida que o login do app
/// enxerga: valendo → o CPF do operador abre o paciente; trocado/encerrado → as sessões abertas
/// caem e o CPF volta a ser só do operador. Postgres real: é <c>ExecuteUpdate</c> e índice único
/// filtrado, que não existem em memória.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class PersonificacaoPacienteTests(PostgresFixture fixture)
{
    private readonly IPacientesService _pacientes = Substitute.For<IPacientesService>();
    private readonly IIdentidadeService _identidade = Substitute.For<IIdentidadeService>();

    private sealed record Cenario(Guid UsuarioId, string CpfOperador, Guid AlvoA, Guid AlvoB);

    [Fact]
    public async Task Valendo_abre_o_paciente_e_trocar_ou_encerrar_derruba_a_sessao()
    {
        var c = await MontarAsync(comWhatsAppVerificado: true, comSandbox: true);
        await using var db = fixture.CriarDbContext();
        var servico = Servico(db, c.UsuarioId);
        var sessoes = Sessoes(db);

        await servico.AtivarAsync(c.AlvoA);
        var alvo = await servico.ResolverNoLoginAsync(c.CpfOperador);
        alvo.Should().NotBeNull();
        alvo!.PacienteId.Should().Be(c.AlvoA);

        var (_, expira) = await sessoes.AbrirSessaoPersonificadaAsync(
            alvo.PacienteId, alvo.Nome, alvo.Cpf, alvo.PersonificacaoId, alvo.ExpiraEm, "teste", null);
        expira.Should().Be(alvo.ExpiraEm); // não sobrevive à personificação
        var jti = await UltimaSessaoAsync(alvo.PersonificacaoId);

        // Consentida sem termo aceito: a equipe não aceita em nome do paciente, e o gate não trava o teste.
        (await Sessoes(fixture.CriarDbContext()).ValidarAcessoAsync(jti, c.AlvoA))
            .Should().Be(new AcessoCidadaoValidacao(true, true));

        // Trocar de paciente derruba quem estava logado como o anterior.
        await servico.AtivarAsync(c.AlvoB);
        (await Sessoes(fixture.CriarDbContext()).ValidarAcessoAsync(jti, c.AlvoA)).SessaoValida.Should().BeFalse();
        (await servico.ResolverNoLoginAsync(c.CpfOperador))!.PacienteId.Should().Be(c.AlvoB);

        await servico.EncerrarAsync();
        (await servico.ResolverNoLoginAsync(c.CpfOperador)).Should().BeNull();
        (await servico.ObterStatusAsync()).Ativa.Should().BeNull();
    }

    [Fact]
    public async Task Historico_de_acesso_mostra_quem_da_equipe_entrou()
    {
        var c = await MontarAsync(comWhatsAppVerificado: true, comSandbox: true);
        await using var db = fixture.CriarDbContext();
        var servico = Servico(db, c.UsuarioId);
        await servico.AtivarAsync(c.AlvoA);
        var alvo = (await servico.ResolverNoLoginAsync(c.CpfOperador))!;
        await Sessoes(db).AbrirSessaoPersonificadaAsync(
            alvo.PacienteId, alvo.Nome, alvo.Cpf, alvo.PersonificacaoId, alvo.ExpiraEm, "teste", null);

        var acessos = await Sessoes(fixture.CriarDbContext()).ListarAcessosAsync(c.AlvoA);

        acessos.Should().ContainSingle(a => a.Canal == ICidadaoSessaoService.CanalPersonificacao)
            .Which.PersonificadoPor.Should().StartWith("OPERADOR");
    }

    [Fact]
    public async Task Quem_perdeu_o_Sandbox_depois_de_ativar_entra_como_ele_mesmo()
    {
        var c = await MontarAsync(comWhatsAppVerificado: true, comSandbox: true);
        await using var db = fixture.CriarDbContext();
        var servico = Servico(db, c.UsuarioId);
        await servico.AtivarAsync(c.AlvoA);

        PermitirSandbox(AcoesPermissao.Consulta); // tiraram a Edição do perfil

        (await servico.ResolverNoLoginAsync(c.CpfOperador)).Should().BeNull();
    }

    [Fact]
    public async Task Sem_WhatsApp_verificado_nao_fica_apto()
    {
        var c = await MontarAsync(comWhatsAppVerificado: false, comSandbox: true);
        await using var db = fixture.CriarDbContext();
        var servico = Servico(db, c.UsuarioId);

        var status = await servico.ObterStatusAsync();
        status.Apta.Should().BeFalse();
        status.MotivoInapta.Should().Contain("WhatsApp");

        var ativar = () => servico.AtivarAsync(c.AlvoA);
        await ativar.Should().ThrowAsync<ConflitoException>();
    }

    private async Task<Cenario> MontarAsync(bool comWhatsAppVerificado, bool comSandbox)
    {
        var cpfOperador = CpfAleatorio();
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            NomeCompleto = $"OPERADOR {Guid.NewGuid():N}"[..20],
            Email = $"{Guid.NewGuid():N}@teste.local",
            Cpf = cpfOperador,
            SenhaHash = "x",
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
        };
        await using (var db = fixture.CriarDbContext())
        {
            db.Usuarios.Add(usuario);
            await db.SaveChangesAsync();
        }

        var pacienteOperador = Guid.NewGuid();
        _pacientes.ObterPorCpfAsync(cpfOperador, Arg.Any<CancellationToken>())
            .Returns(new PacienteExistenciaDto(pacienteOperador, "OPERADOR", cpfOperador, Ativo: true));
        _pacientes.ObterPorIdAsync(pacienteOperador, Arg.Any<CancellationToken>())
            .Returns(PacienteAuthServiceTests.Paciente(
                pacienteOperador, cpf: cpfOperador,
                telefoneVerificado: comWhatsAppVerificado ? "5521999991234" : null));

        var alvoA = Guid.NewGuid();
        var alvoB = Guid.NewGuid();
        _pacientes.ObterPorIdAsync(alvoA, Arg.Any<CancellationToken>())
            .Returns(PacienteAuthServiceTests.Paciente(alvoA, cpf: CpfAleatorio(), nome: "JOANA ALVO"));
        _pacientes.ObterPorIdAsync(alvoB, Arg.Any<CancellationToken>())
            .Returns(PacienteAuthServiceTests.Paciente(alvoB, cpf: CpfAleatorio(), nome: "PEDRO ALVO"));

        PermitirSandbox(comSandbox ? AcoesPermissao.Todas : AcoesPermissao.Nenhuma);
        return new Cenario(usuario.Id, cpfOperador, alvoA, alvoB);
    }

    private void PermitirSandbox(AcoesPermissao acoes)
    {
        var resolvidas = new List<PermissaoModuloDto> { new(ModuloPermissao.Sandbox, acoes) };
        _identidade.ObterPermissoesResolvidasAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new PermissoesResolvidasDto([], [], resolvidas));
    }

    private PersonificacaoPacienteService Servico(SmsMaisDbContext db, Guid usuarioId) =>
        new(db, _pacientes, _identidade, new UsuarioAtualAccessorFake(usuarioId),
            NullLogger<PersonificacaoPacienteService>.Instance);

    private static CidadaoSessaoService Sessoes(SmsMaisDbContext db)
    {
        var tokens = Substitute.For<IPacienteTokenService>();
        tokens.Gerar(default, default!, default, default, default).ReturnsForAnyArgs("jwt");
        return new CidadaoSessaoService(
            db, tokens, new ConfigurationBuilder().Build(), NullLogger<CidadaoSessaoService>.Instance);
    }

    private async Task<Guid> UltimaSessaoAsync(Guid personificacaoId)
    {
        await using var db = fixture.CriarDbContext();
        return db.CidadaoSessoes.Where(s => s.PersonificacaoId == personificacaoId)
            .OrderByDescending(s => s.CriadaEm).Select(s => s.Id).First();
    }

    private static string CpfAleatorio() =>
        string.Concat(Enumerable.Range(0, 11).Select(_ => (char)('0' + Random.Shared.Next(10))));
}
