using Microsoft.EntityFrameworkCore;
using SMSMais.Core.EscopoExames;
using SMSMais.Core.EscopoExames.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.SolicitacoesExame;

/// <summary>
/// Ligar o envio à worklist de um exame na unidade tem de valer para quem JÁ ESTAVA esperando.
///
/// <para>Caso real de 08/09/2026: o exame <c>260908005</c> foi autorizado às 17:52 com o envio
/// desligado — a autorização carimbou o impedimento e o tirou da fila (<c>ProximaTentativaEm</c>
/// nulo). Às 18:06 alguém ligou o exame na tela e <b>nada aconteceu</b>: o toggle mexia só na
/// configuração, e o worker seleciona por <c>ProximaTentativaEm</c>. A tela oferecia um botão que
/// parecia resolver e não resolvia.</para>
/// </summary>
[Collection(nameof(PostgresCollection))]
public class LigarEscopoReenfileiraTests(PostgresFixture fixture)
{
    private static EscopoExamesService CriarService(SmsMaisDbContext db) =>
        new(db, new UsuarioAtualAccessorFake(Guid.NewGuid()));

    private static async Task<TipoExameUnidade> EscopoDoAsync(
        SmsMaisDbContext db, ExameImagem exame) =>
        await db.TiposExameUnidade.SingleAsync(
            a => a.TipoExameId == exame.TipoExameId
                 && a.UnidadeId == exame.Solicitacao!.UnidadeExecutanteId);

    [Fact]
    public async Task Ligar_o_envio_devolve_a_fila_o_exame_que_ja_estava_esperando()
    {
        await using var db = fixture.CriarDbContext();
        var exame = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid(), enviarParaWorklist: false);
        await db.Entry(exame).Reference(x => x.Solicitacao).LoadAsync();

        // O estado em que a autorização deixa um exame impedido: autorizado, fora da fila e com o
        // motivo carimbado.
        exame.Solicitacao!.AutorizadoEm = DateTime.UtcNow;
        exame.ProximaTentativaEm = null;
        exame.ErroIntegracaoPacs = "envio à worklist desligado";
        await db.SaveChangesAsync();

        var escopo = await EscopoDoAsync(db, exame);
        await CriarService(db).AtualizarAsync(
            escopo.Id, new AtualizarEscopoExameRequest(EnviarParaWorklist: true, EquipamentoId: null));

        var atual = await db.ExamesImagem.AsNoTracking().SingleAsync(x => x.Id == exame.Id);
        Assert.NotNull(atual.ProximaTentativaEm);   // voltou para a fila
        Assert.Null(atual.ErroIntegracaoPacs);      // e o motivo antigo saiu da tela
    }

    [Fact]
    public async Task Ligar_o_envio_NAO_fura_o_gate_da_recepcao()
    {
        // Ligar o envio é decisão de configuração; autorizar é decisão de quem está com o paciente
        // no balcão. Uma não pode virar a outra.
        await using var db = fixture.CriarDbContext();
        var exame = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid(), enviarParaWorklist: false);
        await db.Entry(exame).Reference(x => x.Solicitacao).LoadAsync();
        Assert.Null(exame.Solicitacao!.AutorizadoEm);

        var escopo = await EscopoDoAsync(db, exame);
        await CriarService(db).AtualizarAsync(
            escopo.Id, new AtualizarEscopoExameRequest(EnviarParaWorklist: true, EquipamentoId: null));

        var atual = await db.ExamesImagem.AsNoTracking().SingleAsync(x => x.Id == exame.Id);
        Assert.Null(atual.ProximaTentativaEm);
    }

    [Fact]
    public async Task Ligar_o_envio_nao_mexe_em_exame_ja_realizado()
    {
        await using var db = fixture.CriarDbContext();
        var exame = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid(), enviarParaWorklist: false);
        await db.Entry(exame).Reference(x => x.Solicitacao).LoadAsync();
        exame.Solicitacao!.AutorizadoEm = DateTime.UtcNow;
        exame.Status = StatusSolicitacaoExame.Realizada;
        exame.ProximaTentativaEm = null;
        await db.SaveChangesAsync();

        var escopo = await EscopoDoAsync(db, exame);
        await CriarService(db).AtualizarAsync(
            escopo.Id, new AtualizarEscopoExameRequest(EnviarParaWorklist: true, EquipamentoId: null));

        var atual = await db.ExamesImagem.AsNoTracking().SingleAsync(x => x.Id == exame.Id);
        Assert.Null(atual.ProximaTentativaEm);
    }

    [Fact]
    public async Task Desligar_o_envio_nao_enfileira_nada()
    {
        await using var db = fixture.CriarDbContext();
        var exame = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid(), enviarParaWorklist: true);
        await db.Entry(exame).Reference(x => x.Solicitacao).LoadAsync();
        exame.Solicitacao!.AutorizadoEm = DateTime.UtcNow;
        exame.ProximaTentativaEm = null;
        await db.SaveChangesAsync();

        var escopo = await EscopoDoAsync(db, exame);
        await CriarService(db).AtualizarAsync(
            escopo.Id, new AtualizarEscopoExameRequest(EnviarParaWorklist: false, EquipamentoId: null));

        var atual = await db.ExamesImagem.AsNoTracking().SingleAsync(x => x.Id == exame.Id);
        Assert.Null(atual.ProximaTentativaEm);
    }
}
