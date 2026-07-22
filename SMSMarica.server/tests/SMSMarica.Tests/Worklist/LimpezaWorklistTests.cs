using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Laudos.Assinatura;
using SMSMarica.Core.Notificacoes;
using SMSMarica.Core.Notificacoes.Comunicacao;
using SMSMarica.Core.Pacientes.Fhir;
using SMSMarica.Core.SolicitacoesExame;
using SMSMarica.Core.SolicitacoesExame.Identificadores;
using SMSMarica.Core.Worklist;
using SMSMarica.Data;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Enums;
using SMSMarica.Tests.Infraestrutura;

namespace SMSMarica.Tests.Worklist;

/// <summary>
/// Limpeza da worklist: exame que chegou (ou foi cancelado/excluído) tem o item removido do
/// dcm4chee pelo motor. É o que substitui o MPPS que o equipamento não manda — sem isso a
/// lista do aparelho só cresce e o exame do dia se perde no meio dos já realizados.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class LimpezaWorklistTests(PostgresFixture fixture)
{
    private static (SolicitacoesExameService Service, IDcm4cheeMwlClient Mwl) CriarService(SmsMaricaDbContext db)
    {
        var mwl = Substitute.For<IDcm4cheeMwlClient>();
        var service = new SolicitacoesExameService(
            db,
            Substitute.For<IGeradorIdentificadores>(),
            mwl,
            Substitute.For<INotificadorExame>(),
            new UsuarioAtualAccessorFake(Guid.NewGuid()),
            Substitute.For<IPacienteResolver>(),
            new Lazy<ILaudoAssinaturaService>(() => Substitute.For<ILaudoAssinaturaService>()),
            new Lazy<IComunicacaoPacienteService>(() => Substitute.For<IComunicacaoPacienteService>()),
            NullLogger<SolicitacoesExameService>.Instance);
        return (service, mwl);
    }

    private static async Task<ExameImagem> PrepararAsync(
        SmsMaricaDbContext db,
        StatusSolicitacaoExame status,
        string? worklistItemUid = "SPS-TESTE",
        bool excluido = false)
    {
        var exame = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid());
        exame.Status = status;
        exame.WorklistItemUid = worklistItemUid;
        if (excluido) exame.ExcluidoEm = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return exame;
    }

    [Theory]
    [InlineData(StatusSolicitacaoExame.Realizada)]
    [InlineData(StatusSolicitacaoExame.Laudada)]
    [InlineData(StatusSolicitacaoExame.Cancelada)]
    public async Task Exame_fora_do_jogo_sai_da_worklist(StatusSolicitacaoExame status)
    {
        await using var db = fixture.CriarDbContext();
        var exame = await PrepararAsync(db, status);
        var (service, mwl) = CriarService(db);

        await service.ProcessarLimpezaWorklistAsync(exame.Id);

        await mwl.Received(1).ExcluirMwlItemAsync(Arg.Any<ExameImagem>(), Arg.Any<CancellationToken>());
        var atualizado = await db.ExamesImagem.AsNoTracking().FirstAsync(e => e.Id == exame.Id);
        Assert.Null(atualizado.WorklistItemUid);
        Assert.Null(atualizado.ProximaTentativaEm);
    }

    [Fact]
    public async Task Exame_excluido_tambem_sai_da_worklist()
    {
        await using var db = fixture.CriarDbContext();
        var exame = await PrepararAsync(db, StatusSolicitacaoExame.Recebida, excluido: true);
        var (service, mwl) = CriarService(db);

        await service.ProcessarLimpezaWorklistAsync(exame.Id);

        await mwl.Received(1).ExcluirMwlItemAsync(Arg.Any<ExameImagem>(), Arg.Any<CancellationToken>());
        var atualizado = await db.ExamesImagem.AsNoTracking().FirstAsync(e => e.Id == exame.Id);
        Assert.Null(atualizado.WorklistItemUid);
    }

    [Fact]
    public async Task Exame_agendado_e_nao_realizado_permanece_na_worklist()
    {
        // Paciente faltou: o item continua na lista do equipamento. Só sai por cancelamento
        // ou exclusão explícitos — decisão humana, nunca do motor.
        await using var db = fixture.CriarDbContext();
        var exame = await PrepararAsync(db, StatusSolicitacaoExame.Recebida);
        var (service, mwl) = CriarService(db);

        await service.ProcessarLimpezaWorklistAsync(exame.Id);

        await mwl.DidNotReceive().ExcluirMwlItemAsync(Arg.Any<ExameImagem>(), Arg.Any<CancellationToken>());
        var atualizado = await db.ExamesImagem.AsNoTracking().FirstAsync(e => e.Id == exame.Id);
        Assert.Equal("SPS-TESTE", atualizado.WorklistItemUid);
    }

    [Fact]
    public async Task Exame_sem_item_de_worklist_nao_chama_o_PACS()
    {
        await using var db = fixture.CriarDbContext();
        var exame = await PrepararAsync(db, StatusSolicitacaoExame.Realizada, worklistItemUid: null);
        var (service, mwl) = CriarService(db);

        await service.ProcessarLimpezaWorklistAsync(exame.Id);

        await mwl.DidNotReceive().ExcluirMwlItemAsync(Arg.Any<ExameImagem>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Falha_no_PACS_mantem_o_item_e_reagenda()
    {
        await using var db = fixture.CriarDbContext();
        var exame = await PrepararAsync(db, StatusSolicitacaoExame.Realizada);
        var (service, mwl) = CriarService(db);
        mwl.ExcluirMwlItemAsync(Arg.Any<ExameImagem>(), Arg.Any<CancellationToken>())
            .Returns<Task<bool>>(_ => throw new ConflitoException("pacs.indisponivel", "PACS fora."));

        await service.ProcessarLimpezaWorklistAsync(exame.Id); // não propaga: worker segue a passagem

        var atualizado = await db.ExamesImagem.AsNoTracking().FirstAsync(e => e.Id == exame.Id);
        Assert.Equal("SPS-TESTE", atualizado.WorklistItemUid); // espelho intacto → volta na fila
        Assert.NotNull(atualizado.ProximaTentativaEm);
        Assert.True(atualizado.ProximaTentativaEm > DateTime.UtcNow.AddMinutes(3));
    }
}
