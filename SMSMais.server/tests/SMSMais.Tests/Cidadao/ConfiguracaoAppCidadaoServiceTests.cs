using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SMSMais.Core.Auditoria;
using SMSMais.Core.Cidadao.Entrada;
using SMSMais.Core.Integracoes.Credenciais;
using SMSMais.Core.Integracoes.Credenciais.Dtos;
using SMSMais.Core.Integracoes.EsusPec;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Cidadao;

/// <summary>
/// Sistema → App do cidadão (ADR-0078 §5), contra Postgres real: só admin global lê e altera; cada
/// ajuste vai para a trilha de auditoria e volta na tela como "de → para"; o PWA e o nativo têm a
/// consulta ao e-SUS cada um a sua, desligada de nascença.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class ConfiguracaoAppCidadaoServiceTests(PostgresFixture fixture)
{
    private readonly IIntegracaoCredencialService _credenciais = Substitute.For<IIntegracaoCredencialService>();

    private async Task<Guid> SemearUsuarioAsync(SmsMaisDbContext db, bool acessoGlobal)
    {
        var u = new Usuario
        {
            Id = Guid.NewGuid(),
            NomeCompleto = acessoGlobal ? "ADMIN GLOBAL (exemplo)" : "OPERADOR (exemplo)",
            Email = $"{Guid.NewGuid():N}@teste.local",
            SenhaHash = "x",
            Ativo = true,
            AcessoGlobal = acessoGlobal,
            CriadoEm = DateTime.UtcNow,
        };
        db.Usuarios.Add(u);
        await db.SaveChangesAsync();
        return u.Id;
    }

    private ConfiguracaoAppCidadaoService Servico(SmsMaisDbContext db, Guid? usuarioId)
    {
        _credenciais.ObterAsync(CorrecaoTelefoneEsusService.Provedor, Arg.Any<CancellationToken>())
            .Returns(new IntegracaoCredencialDto(CorrecaoTelefoneEsusService.Provedor, "e-SUS PEC", true, true, null, null, true));
        var usuario = new UsuarioAtualAccessorFake(usuarioId);
        return new ConfiguracaoAppCidadaoService(
            db, usuario, new AuditoriaService(db, usuario, Substitute.For<IPacienteResolver>()), _credenciais);
    }

    private static async Task LimparAsync(SmsMaisDbContext db)
    {
        await db.ConfiguracoesAppCidadao.ExecuteDeleteAsync();
        await db.RegistrosAuditoria.Where(r => r.Entidade == ConfiguracaoAppCidadaoService.EntidadeAuditoria).ExecuteDeleteAsync();
    }

    [Fact]
    public async Task Quem_nao_e_admin_global_nao_le_nem_altera()
    {
        await using var db = fixture.CriarDbContext();
        var servico = Servico(db, await SemearUsuarioAsync(db, acessoGlobal: false));

        await servico.Invoking(s => s.ObterAsync()).Should().ThrowAsync<UnauthorizedAccessException>();
        await servico.Invoking(s => s.AtualizarAsync(new AtualizarConfiguracaoAppCidadaoRequest(new CanalEntradaDto(true), null)))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Ajuste_grava_por_canal_e_aparece_no_historico_com_quem_e_o_que_mudou()
    {
        await using var db = fixture.CriarDbContext();
        await LimparAsync(db);
        var servico = Servico(db, await SemearUsuarioAsync(db, acessoGlobal: true));

        var antes = await servico.ObterAsync();
        antes.Pwa.ConsultarEsus.Should().BeFalse();
        antes.Nativo.ConsultarEsus.Should().BeFalse();
        antes.EsusConfigurado.Should().BeTrue();

        var depois = await servico.AtualizarAsync(new AtualizarConfiguracaoAppCidadaoRequest(null, new CanalEntradaDto(true)));

        depois.Pwa.ConsultarEsus.Should().BeFalse();
        depois.Nativo.ConsultarEsus.Should().BeTrue();
        depois.AtualizadoPor.Should().Be("ADMIN GLOBAL (exemplo)");
        var ajuste = depois.Ajustes.Should().ContainSingle().Subject;
        ajuste.Mudancas.Should().Equal("App nativo: consulta ao e-SUS desligada → ligada");

        (await servico.ConsultarEsusNoCanalAsync(CanalAppCidadao.Nativo)).Should().BeTrue();
        (await servico.ConsultarEsusNoCanalAsync(CanalAppCidadao.Pwa)).Should().BeFalse();
    }

    [Fact]
    public async Task Salvar_sem_mudar_nada_nao_cria_ajuste()
    {
        await using var db = fixture.CriarDbContext();
        await LimparAsync(db);
        var servico = Servico(db, await SemearUsuarioAsync(db, acessoGlobal: true));

        var r = await servico.AtualizarAsync(new AtualizarConfiguracaoAppCidadaoRequest(new CanalEntradaDto(false), new CanalEntradaDto(false)));

        r.Ajustes.Should().BeEmpty();
        (await db.ConfiguracoesAppCidadao.AnyAsync()).Should().BeFalse();
    }
}
