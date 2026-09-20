using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SMSMais.Core.Cidadao;
using SMSMais.Core.Notificacoes.Comunicacao;
using SMSMais.Core.Notificacoes.WhatsApp;
using SMSMais.Core.Pacientes;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Notificacoes;

/// <summary>
/// Aviso ao paciente depois de uma CORREÇÃO de identidade.
///
/// <para>O enfileiramento é idempotente por solicitação × finalidade — existindo a linha, ele não
/// cria outra. Isso é certo no dia a dia e ERRADO depois de uma correção: o paciente recebeu o
/// resultado de OUTRA pessoa, o vínculo foi consertado, e o aviso do exame certo nunca sairia
/// porque a linha (do envio errado) já estava lá. Aconteceu com o João Bento em 11/08/2026 — ele
/// ficaria com o exame correto no prontuário e sem nenhum aviso.</para>
///
/// <para>A régua: aviso ENVIADO antes do <c>realizado_em</c> atual do exame fala de outro estudo.</para>
/// </summary>
[Collection(nameof(PostgresCollection))]
public class AvisoObsoletoAposCorrecaoTests(PostgresFixture fixture)
{
    private static ComunicacaoPacienteService CriarService(SmsMaisDbContext db) =>
        new(db,
            Substitute.For<IPacientesService>(),
            Substitute.For<ICidadaoLoginLinkService>(),
            Substitute.For<IWhatsAppCliente>(),
            Options.Create(new ComunicacaoPacienteOptions()),
            new UsuarioAtualAccessorFake(),
            Substitute.For<SMSMais.Core.Telefones.IDispensaContatoService>(),
            Substitute.For<SMSMais.Core.Notificacoes.Comunicacao.IContatoComprometidoService>(),
            Substitute.For<SMSMais.Core.Notificacoes.Confirmacoes.IConfirmacaoConfiguracaoService>(),
            NullLogger<ComunicacaoPacienteService>.Instance);

    private static async Task<ComunicacaoPaciente> SemearAvisoEnviadoAsync(
        SmsMaisDbContext db, ExameImagem exame, DateTime enviadoEm)
    {
        var c = new ComunicacaoPaciente
        {
            Id = Guid.CreateVersion7(),
            Tipo = TipoAgendamento.Exame,
            Finalidade = FinalidadeComunicacao.ExameLiberado,
            SolicitacaoId = exame.SolicitacaoId,
            PacienteId = Guid.CreateVersion7(),
            Status = StatusComunicacao.Lida,
            Telefone = "5521999999999",
            Tentativas = 1,
            EnviadoEm = enviadoEm,
            LidoEm = enviadoEm.AddMinutes(5),
            CriadoEm = enviadoEm,
        };
        db.ComunicacoesPaciente.Add(c);
        await db.SaveChangesAsync();
        return c;
    }

    [Fact]
    public async Task Aviso_enviado_antes_do_estudo_atual_e_rearmado_para_sair_de_novo()
    {
        await using var db = fixture.CriarDbContext();
        var exame = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid());

        // O aviso saiu de manhã (com o estudo ERRADO); a correção vinculou o certo à tarde.
        var enviadoEm = DateTime.UtcNow.AddHours(-6);
        var c = await SemearAvisoEnviadoAsync(db, exame, enviadoEm);
        await db.ExamesImagem.Where(e => e.Id == exame.Id)
            .ExecuteUpdateAsync(u => u.SetProperty(e => e.RealizadoEm, DateTime.UtcNow), default);

        var solicitacao = await db.Solicitacoes.FirstAsync(x => x.Id == exame.SolicitacaoId);
        await CriarService(db).EnfileirarAsync(solicitacao, FinalidadeComunicacao.ExameLiberado);
        await db.SaveChangesAsync();

        var atual = await db.ComunicacoesPaciente.AsNoTracking().SingleAsync(x => x.Id == c.Id);
        Assert.Equal(StatusComunicacao.Pendente, atual.Status);
        Assert.NotNull(atual.ProximaTentativaEm); // o worker vai pegar
        // Tudo que se referia ao envio anterior sai: o link antigo foi revogado e o telefone é
        // re-resolvido no envio (pode ter mudado desde então).
        Assert.Null(atual.EnviadoEm);
        Assert.Null(atual.Telefone);
        Assert.Null(atual.LoginLinkId);
        Assert.Equal(0, atual.Tentativas);
    }

    [Fact]
    public async Task Aviso_enviado_DEPOIS_do_estudo_atual_nao_e_reenviado()
    {
        await using var db = fixture.CriarDbContext();
        var exame = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid());

        // Caminho normal: exame realizado e, em seguida, o aviso. Rearmar aqui seria spam.
        await db.ExamesImagem.Where(e => e.Id == exame.Id)
            .ExecuteUpdateAsync(u => u.SetProperty(e => e.RealizadoEm, DateTime.UtcNow.AddHours(-2)), default);
        var c = await SemearAvisoEnviadoAsync(db, exame, DateTime.UtcNow.AddHours(-1));

        var solicitacao = await db.Solicitacoes.FirstAsync(x => x.Id == exame.SolicitacaoId);
        await CriarService(db).EnfileirarAsync(solicitacao, FinalidadeComunicacao.ExameLiberado);
        await db.SaveChangesAsync();

        var atual = await db.ComunicacoesPaciente.AsNoTracking().SingleAsync(x => x.Id == c.Id);
        Assert.Equal(StatusComunicacao.Lida, atual.Status);
        Assert.NotNull(atual.EnviadoEm);
    }
}
