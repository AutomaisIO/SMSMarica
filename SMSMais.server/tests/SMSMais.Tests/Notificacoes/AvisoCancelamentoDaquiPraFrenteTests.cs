using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SMSMais.Core.Cidadao;
using SMSMais.Core.Notificacoes.Comunicacao;
using SMSMais.Core.Notificacoes.Confirmacoes;
using SMSMais.Core.Notificacoes.Confirmacoes.Dtos;
using SMSMais.Core.Notificacoes.WhatsApp;
using SMSMais.Core.Pacientes;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Notificacoes;

/// <summary>
/// Aviso de CANCELAMENTO ao paciente — "ligar vale daqui para frente" (25/09/2026).
///
/// <para>Até essa data a conciliação com o SISREG enfileirava o aviso mesmo com a chave desligada, e
/// o enviador nem selecionava essa finalidade: 217 avisos parados. Ligar a chave, do jeito antigo,
/// soltaria todos de uma vez — inclusive de agendamentos que já passaram. A régua agora: chave
/// desligada não enfileira; e aviso de agendamento que já passou não sai.</para>
/// </summary>
[Collection(nameof(PostgresCollection))]
public class AvisoCancelamentoDaquiPraFrenteTests(PostgresFixture fixture)
{
    private static ComunicacaoPacienteService CriarService(
        SmsMaisDbContext db, bool avisoLigado, DateTime? ligadoEm = null)
    {
        var regras = Substitute.For<IConfirmacaoConfiguracaoService>();
        regras.ObterAsync(Arg.Any<CancellationToken>()).Returns(new ConfirmacaoConfiguracaoDto(
            "08:00", "18:00", 100, SomenteSisreg: true, JanelaAbertaAgora: true, AtualizadoEm: null,
            AvisoCancelamentoHabilitado: avisoLigado,
            AvisoCancelamentoLigadoEm: ligadoEm ?? (avisoLigado ? DateTime.UtcNow.AddHours(-1) : null)));

        return new(db,
            Substitute.For<IPacientesService>(),
            Substitute.For<ICidadaoLoginLinkService>(),
            Substitute.For<IWhatsAppCliente>(),
            Options.Create(new ComunicacaoPacienteOptions()),
            new UsuarioAtualAccessorFake(),
            Substitute.For<SMSMais.Core.Telefones.IDispensaContatoService>(),
            Substitute.For<IContatoComprometidoService>(),
            regras,
            NullLogger<ComunicacaoPacienteService>.Instance);
    }

    [Fact]
    public async Task Com_o_aviso_desligado_o_cancelamento_nao_entra_na_fila()
    {
        await using var db = fixture.CriarDbContext();
        var exame = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid(), dataAgendada: DateTime.UtcNow.AddDays(3));
        var solicitacao = await db.Solicitacoes.FirstAsync(x => x.Id == exame.SolicitacaoId);

        await CriarService(db, avisoLigado: false)
            .EnfileirarAsync(solicitacao, FinalidadeComunicacao.CancelamentoAgendamento);
        await db.SaveChangesAsync();

        // Desligado quer dizer "não avisar", e não "avisar quando ligarem".
        Assert.False(await db.ComunicacoesPaciente.AnyAsync(c =>
            c.SolicitacaoId == solicitacao.Id && c.Finalidade == FinalidadeComunicacao.CancelamentoAgendamento));
    }

    [Fact]
    public async Task Com_o_aviso_ligado_o_cancelamento_entra_na_fila()
    {
        await using var db = fixture.CriarDbContext();
        var exame = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid(), dataAgendada: DateTime.UtcNow.AddDays(3));
        var solicitacao = await db.Solicitacoes.FirstAsync(x => x.Id == exame.SolicitacaoId);

        await CriarService(db, avisoLigado: true)
            .EnfileirarAsync(solicitacao, FinalidadeComunicacao.CancelamentoAgendamento);
        await db.SaveChangesAsync();

        var c = await db.ComunicacoesPaciente.AsNoTracking().SingleAsync(x =>
            x.SolicitacaoId == solicitacao.Id && x.Finalidade == FinalidadeComunicacao.CancelamentoAgendamento);
        Assert.Equal(StatusComunicacao.Pendente, c.Status);
        Assert.NotNull(c.ProximaTentativaEm); // o enviador vai pegar
    }

    [Fact]
    public async Task Aviso_de_agendamento_que_ja_passou_nao_e_enviado()
    {
        await using var db = fixture.CriarDbContext();
        // Entrou na fila à noite para um agendamento das 7h; a janela só abre às 8h.
        var exame = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid(), dataAgendada: DateTime.UtcNow.AddHours(-1));
        await db.Solicitacoes.Where(s => s.Id == exame.SolicitacaoId)
            .ExecuteUpdateAsync(u => u.SetProperty(s => s.Status, StatusSolicitacao.Cancelada), default);

        var c = new ComunicacaoPaciente
        {
            Id = Guid.CreateVersion7(),
            Tipo = TipoAgendamento.Exame,
            Finalidade = FinalidadeComunicacao.CancelamentoAgendamento,
            SolicitacaoId = exame.SolicitacaoId,
            PacienteId = Guid.CreateVersion7(),
            Status = StatusComunicacao.Pendente,
            ProximaTentativaEm = DateTime.UtcNow.AddMinutes(-1),
            CriadoEm = DateTime.UtcNow.AddHours(-10),
        };
        db.ComunicacoesPaciente.Add(c);
        await db.SaveChangesAsync();

        await CriarService(db, avisoLigado: true).ProcessarTentativaEnvioAsync(c.Id);

        var atual = await db.ComunicacoesPaciente.AsNoTracking().SingleAsync(x => x.Id == c.Id);
        Assert.Equal(StatusComunicacao.Falha, atual.Status);
        Assert.Contains("já passou", atual.MotivoFalha);
    }
    [Fact]
    public async Task Aviso_que_entrou_na_fila_antes_de_ligar_sai_como_retroativo()
    {
        await using var db = fixture.CriarDbContext();
        var exame = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid(), dataAgendada: DateTime.UtcNow.AddDays(5));
        await db.Solicitacoes.Where(s => s.Id == exame.SolicitacaoId)
            .ExecuteUpdateAsync(u => u.SetProperty(s => s.Status, StatusSolicitacao.Cancelada), default);

        // Ficou parado na fila desde ontem; o aviso foi ligado agora.
        var c = new ComunicacaoPaciente
        {
            Id = Guid.CreateVersion7(),
            Tipo = TipoAgendamento.Exame,
            Finalidade = FinalidadeComunicacao.CancelamentoAgendamento,
            SolicitacaoId = exame.SolicitacaoId,
            PacienteId = Guid.CreateVersion7(),
            Status = StatusComunicacao.Pendente,
            ProximaTentativaEm = DateTime.UtcNow.AddMinutes(-1),
            CriadoEm = DateTime.UtcNow.AddDays(-1),
        };
        db.ComunicacoesPaciente.Add(c);
        await db.SaveChangesAsync();

        await CriarService(db, avisoLigado: true, ligadoEm: DateTime.UtcNow).ProcessarTentativaEnvioAsync(c.Id);

        var atual = await db.ComunicacoesPaciente.AsNoTracking().SingleAsync(x => x.Id == c.Id);
        Assert.Equal(StatusComunicacao.Falha, atual.Status);
        Assert.Contains("retroativo", atual.MotivoFalha);
        Assert.Null(atual.EnviadoEm);
    }

    [Fact]
    public async Task Cancelamento_feito_antes_de_ligar_e_conciliado_depois_nao_entra_na_fila()
    {
        await using var db = fixture.CriarDbContext();
        var exame = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid(), dataAgendada: DateTime.UtcNow.AddDays(5));
        // A releitura do fechamento trouxe hoje um cancelamento feito no SISREG há duas horas.
        await db.Solicitacoes.Where(s => s.Id == exame.SolicitacaoId)
            .ExecuteUpdateAsync(u => u
                .SetProperty(s => s.Status, StatusSolicitacao.Cancelada)
                .SetProperty(s => s.CanceladoEm, DateTime.UtcNow.AddHours(-2)), default);
        // Sem rastreamento: o ExecuteUpdate não atualiza a entidade que o seed deixou no tracker.
        var solicitacao = await db.Solicitacoes.AsNoTracking().FirstAsync(x => x.Id == exame.SolicitacaoId);

        await CriarService(db, avisoLigado: true, ligadoEm: DateTime.UtcNow.AddMinutes(-30))
            .EnfileirarAsync(solicitacao, FinalidadeComunicacao.CancelamentoAgendamento);
        await db.SaveChangesAsync();

        Assert.False(await db.ComunicacoesPaciente.AnyAsync(c =>
            c.SolicitacaoId == solicitacao.Id && c.Finalidade == FinalidadeComunicacao.CancelamentoAgendamento));
    }
}
