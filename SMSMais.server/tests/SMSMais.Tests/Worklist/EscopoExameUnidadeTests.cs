using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SMSMais.Core.Worklist;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Worklist;

/// <summary>
/// O escopo por unidade — a régua que substituiu a flag global <c>TipoExame.EnviarParaWorklist</c>.
///
/// <para>O caso que motivou tudo: em 09/2026 o CDT ganhou um raio-X e Hospital Santo Antônio,
/// Ernesto Che Guevara e DIMAGEM executavam radiografia <b>sem ter equipamento</b>. Com um bit do
/// município não havia estado correto — ligar servia o CDT e enchia as outras de erro; desligar
/// deixava o aparelho novo sem worklist. O primeiro teste aqui é exatamente esse impasse.</para>
/// </summary>
[Collection(nameof(PostgresCollection))]
public class EscopoExameUnidadeTests(PostgresFixture fixture)
{
    private static ResolvedorEstacaoWorklist CriarResolvedor(SmsMaisDbContext db) =>
        new(db, new EscopoExameUnidade(db), NullLogger<ResolvedorEstacaoWorklist>.Instance);

    private static async Task<TipoExameUnidade> EscoparAsync(
        SmsMaisDbContext db, Guid tipoExameId, Guid unidadeId,
        bool envia, Guid? equipamentoId = null)
    {
        var escopo = new TipoExameUnidade
        {
            Id = Guid.CreateVersion7(),
            TipoExameId = tipoExameId,
            UnidadeId = unidadeId,
            EnviarParaWorklist = envia,
            EquipamentoId = equipamentoId,
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
        };
        db.TiposExameUnidade.Add(escopo);
        await db.SaveChangesAsync();
        return escopo;
    }

    private static async Task<Equipamento> EquipamentoAsync(
        SmsMaisDbContext db, Guid unidadeId, ModalidadeDicom modalidade, string ae, string nome)
    {
        var equipamento = new Equipamento
        {
            Id = Guid.CreateVersion7(),
            Nome = nome,
            UnidadeId = unidadeId,
            ModalidadeDicom = modalidade,
            IdentificadorDicom = ae,
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
        };
        db.Equipamentos.Add(equipamento);
        await db.SaveChangesAsync();
        return equipamento;
    }

    [Fact]
    public async Task O_mesmo_exame_envia_na_unidade_com_aparelho_e_nao_na_outra()
    {
        // O impasse que a flag global não resolvia, agora resolvido: MESMO tipo de exame,
        // ligado numa unidade e desligado na outra, ao mesmo tempo.
        await using var db = fixture.CriarDbContext();
        var comAparelho = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid(), enviarParaWorklist: true);
        await db.Entry(comAparelho).Reference(x => x.Solicitacao).LoadAsync();
        var unidadeComAparelho = comAparelho.Solicitacao!.UnidadeExecutanteId;
        var tipoId = comAparelho.TipoExameId!.Value;

        await EquipamentoAsync(db, unidadeComAparelho, ModalidadeDicom.MG, "RX-TESTE", "Raio-X");

        // Uma segunda unidade executa o MESMO tipo, sem aparelho e fora do envio.
        var semAparelho = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid(), enviarParaWorklist: false);
        await db.Entry(semAparelho).Reference(x => x.Solicitacao).LoadAsync();
        var unidadeSemAparelho = semAparelho.Solicitacao!.UnidadeExecutanteId;
        await EscoparAsync(db, tipoId, unidadeSemAparelho, envia: false);

        var escopo = new EscopoExameUnidade(db);

        var ligado = await escopo.ObterAsync(tipoId, unidadeComAparelho);
        var desligado = await escopo.ObterAsync(tipoId, unidadeSemAparelho);

        Assert.True(ligado!.EnviarParaWorklist);
        Assert.False(desligado!.EnviarParaWorklist);
    }

    [Fact]
    public async Task Unidade_sem_o_exame_no_escopo_nao_tem_configuracao()
    {
        await using var db = fixture.CriarDbContext();
        var exame = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid());
        await db.Entry(exame).Reference(x => x.Solicitacao).LoadAsync();

        var outraUnidade = new Unidade
        {
            Id = Guid.NewGuid(),
            Nome = $"UNIDADE SEM ESCOPO {Guid.NewGuid().ToString("N")[..8]}",
            CriadoEm = DateTime.UtcNow,
        };
        db.Unidades.Add(outraUnidade);
        await db.SaveChangesAsync();

        var achado = await new EscopoExameUnidade(db)
            .ObterAsync(exame.TipoExameId!.Value, outraUnidade.Id);

        Assert.Null(achado);
    }

    [Fact]
    public async Task Equipamento_configurado_no_escopo_vence_a_deducao_por_modalidade()
    {
        // A razão de ser do campo: sem ele o destino sai do casamento
        // equipamento.modalidade == tipo.modalidade, e foi esse fio que em 04/09/2026 fez cinco
        // radiografias marcadas como MG apontarem para o mamógrafo do CDT. Aqui o tipo é MG (vem
        // do seed) e mesmo assim o exame vai para o raio-X, porque alguém configurou.
        await using var db = fixture.CriarDbContext();
        var exame = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid(), enviarParaWorklist: true);
        await db.Entry(exame).Reference(x => x.Solicitacao).LoadAsync();
        var unidadeId = exame.Solicitacao!.UnidadeExecutanteId;

        await EquipamentoAsync(db, unidadeId, ModalidadeDicom.MG, "MAMO-TESTE", "Mamógrafo");
        var raioX = await EquipamentoAsync(db, unidadeId, ModalidadeDicom.DX, "RX-TESTE", "Raio-X");

        var escopo = await db.TiposExameUnidade
            .SingleAsync(a => a.TipoExameId == exame.TipoExameId && a.UnidadeId == unidadeId);
        escopo.EquipamentoId = raioX.Id;
        await db.SaveChangesAsync();

        var estacao = await CriarResolvedor(db).ResolverAsync(exame);

        Assert.Equal("RX-TESTE", estacao.AeTitle);
    }

    [Fact]
    public async Task Sem_equipamento_configurado_continua_deduzindo_pela_modalidade()
    {
        // Deixar o destino vazio é escolha válida: é o caso dos dois ultrassons do CDT, em que a
        // recepção escolhe a sala. O comportamento antigo tem de sobreviver.
        await using var db = fixture.CriarDbContext();
        var exame = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid(), enviarParaWorklist: true);
        await db.Entry(exame).Reference(x => x.Solicitacao).LoadAsync();
        var unidadeId = exame.Solicitacao!.UnidadeExecutanteId;

        await EquipamentoAsync(db, unidadeId, ModalidadeDicom.MG, "MAMO-UNICO", "Mamógrafo");

        var estacao = await CriarResolvedor(db).ResolverAsync(exame);

        Assert.Equal("MAMO-UNICO", estacao.AeTitle);
    }
}
