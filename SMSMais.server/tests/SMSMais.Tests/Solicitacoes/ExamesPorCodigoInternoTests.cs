using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SMSMais.Core.Anamneses;
using SMSMais.Core.Anamneses.Dtos;
using SMSMais.Core.Erros;
using SMSMais.Core.Integracoes.SisregWeb.Importacao;
using SMSMais.Core.Laudos.Assinatura;
using SMSMais.Core.Notificacoes;
using SMSMais.Core.Notificacoes.Comunicacao;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Core.Solicitacoes;
using SMSMais.Core.Solicitacoes.Dtos;
using SMSMais.Core.Solicitacoes.Identificadores;
using SMSMais.Core.Telefones;
using SMSMais.Core.Worklist;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Solicitacoes;

/// <summary>
/// ADR-0074: pedido do SISREG com mais de um código interno (joelho D e E com o mesmo nº) é UMA
/// solicitação com um exame por código — accession base e base-1, cada um com o seu item de
/// worklist e o seu laudo. O que é do pedido (receber, cancelar, anamnese) vale para todos.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class ExamesPorCodigoInternoTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Procedimento_extra_vira_exame_com_accession_menos_um_e_tipo_proprio()
    {
        await using var db = fixture.CriarDbContext();
        var (principal, itemExtra) = await SemearPedidoComDoisCodigosAsync(db);

        Assert.Equal(ResultadoSatelite.Criado, await CriarSatelite(db).GarantirAsync(principal.Solicitacao!, null, default));
        await db.SaveChangesAsync();

        var exames = await db.ExamesImagem.AsNoTracking().Include(e => e.TipoExame)
            .Where(e => e.SolicitacaoId == principal.SolicitacaoId).OrderBy(e => e.Ordem).ToListAsync();
        Assert.Equal(2, exames.Count);
        Assert.Equal(principal.Id, exames[0].Id);
        Assert.Equal(principal.SolicitacaoId, exames[0].SolicitacaoPrincipalId);

        var extra = exames[1];
        Assert.Equal(1, extra.Ordem);
        Assert.Null(extra.SolicitacaoPrincipalId);
        Assert.Equal(itemExtra.Id, extra.SolicitacaoProcedimentoId);
        Assert.Equal($"{principal.AccessionNumber}-1", extra.AccessionNumber);
        Assert.NotEqual(principal.StudyInstanceUID, extra.StudyInstanceUID);
        Assert.Equal(itemExtra.ProcedimentoTexto, extra.TipoExame!.Nome);
        // Nada vai ao PACS antes da recepção.
        Assert.Null(extra.ProximaTentativaEm);
    }

    [Fact]
    public async Task Garantir_de_novo_nao_duplica_o_exame_extra()
    {
        await using var db = fixture.CriarDbContext();
        var (principal, _) = await SemearPedidoComDoisCodigosAsync(db);
        var satelite = CriarSatelite(db);

        await satelite.GarantirAsync(principal.Solicitacao!, null, default);
        await db.SaveChangesAsync();
        Assert.Equal(ResultadoSatelite.NadaAFazer, await satelite.GarantirAsync(principal.Solicitacao!, null, default));

        Assert.Equal(2, await db.ExamesImagem.CountAsync(e => e.SolicitacaoId == principal.SolicitacaoId));
    }

    [Fact]
    public async Task Autorizar_um_exame_envia_todos_os_exames_do_pedido()
    {
        await using var db = fixture.CriarDbContext();
        var (principal, extra) = await SemearComExtraAsync(db);

        // Recepção abre a linha do EXTRA e autoriza: o pedido inteiro vai à worklist.
        await CriarSolicitacoes(db, principal.Solicitacao!.PacienteId).AutorizarAsync(extra.Id, "12345");

        var exames = await db.ExamesImagem.AsNoTracking()
            .Where(e => e.SolicitacaoId == principal.SolicitacaoId).ToListAsync();
        Assert.All(exames, e => Assert.NotNull(e.ProximaTentativaEm));
        Assert.All(exames, e => Assert.Null(e.ErroIntegracaoPacs));
        Assert.NotNull((await db.Solicitacoes.AsNoTracking().SingleAsync(s => s.Id == principal.SolicitacaoId)).AutorizadoEm);
    }

    [Fact]
    public async Task Procedimento_que_chega_com_o_pedido_ja_autorizado_entra_na_fila()
    {
        await using var db = fixture.CriarDbContext();
        var (principal, _) = await SemearPedidoComDoisCodigosAsync(db);
        var solicitacao = await db.Solicitacoes.SingleAsync(s => s.Id == principal.SolicitacaoId);
        solicitacao.AutorizadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await CriarSatelite(db).GarantirAsync(solicitacao, null, default);
        await db.SaveChangesAsync();

        var extra = await db.ExamesImagem.AsNoTracking().SingleAsync(e => e.SolicitacaoId == principal.SolicitacaoId && e.Ordem == 1);
        Assert.NotNull(extra.ProximaTentativaEm);
    }

    [Fact]
    public async Task Cancelar_pelo_exame_extra_cancela_o_pedido_inteiro()
    {
        await using var db = fixture.CriarDbContext();
        var (principal, extra) = await SemearComExtraAsync(db);

        await CriarSolicitacoes(db, principal.Solicitacao!.PacienteId)
            .CancelarAsync(extra.Id, new CancelarSolicitacaoRequest("Paciente desistiu"));

        var exames = await db.ExamesImagem.AsNoTracking()
            .Where(e => e.SolicitacaoId == principal.SolicitacaoId).ToListAsync();
        Assert.All(exames, e => Assert.Equal(StatusSolicitacaoExame.Cancelada, e.Status));
        Assert.Equal(StatusSolicitacao.Cancelada,
            (await db.Solicitacoes.AsNoTracking().SingleAsync(s => s.Id == principal.SolicitacaoId)).Status);
    }

    [Fact]
    public async Task Pedido_com_um_exame_ja_feito_nao_se_cancela_pela_tela_do_outro()
    {
        await using var db = fixture.CriarDbContext();
        var (principal, extra) = await SemearComExtraAsync(db);
        var feito = await db.ExamesImagem.SingleAsync(e => e.Id == principal.Id);
        feito.Status = StatusSolicitacaoExame.Realizada;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var ex = await Assert.ThrowsAsync<SMSMais.Core.Common.Excecoes.ConflitoException>(() =>
            CriarSolicitacoes(db, principal.Solicitacao!.PacienteId)
                .CancelarAsync(extra.Id, new CancelarSolicitacaoRequest("Paciente desistiu")));

        Assert.Contains(principal.AccessionNumber, ex.Message);
        Assert.Equal(StatusSolicitacaoExame.Solicitada,
            (await db.ExamesImagem.AsNoTracking().SingleAsync(e => e.Id == extra.Id)).Status);
    }

    [Fact]
    public async Task Anamnese_salva_pelo_extra_e_a_do_pedido_e_aparece_nos_dois()
    {
        await using var db = fixture.CriarDbContext();
        var (principal, extra) = await SemearComExtraAsync(db);
        var anamneses = new AnamnesesService(
            db, Resolver(principal.Solicitacao!.PacienteId),
            Substitute.For<SMSMais.Core.Common.Cid.ICidCatalogoService>(),
            new UsuarioAtualAccessorFake(Guid.NewGuid()),
            Substitute.For<ISolicitacoesService>());

        await anamneses.SalvarAsync(extra.Id, new SalvarAnamneseDto("RX", 1, "{\"queixa\":\"dor\"}", null));

        // Grava no exame PRINCIPAL — uma anamnese por pedido.
        Assert.True(await db.Anamneses.AnyAsync(a => a.ExameImagemId == principal.Id));
        Assert.False(await db.Anamneses.AnyAsync(a => a.ExameImagemId == extra.Id));
        Assert.NotNull((await anamneses.ObterContextoAsync(extra.Id, null)).Anamnese);
        Assert.NotNull((await anamneses.ObterContextoAsync(principal.Id, null)).Anamnese);
    }

    [Fact]
    public async Task Lista_mostra_uma_linha_por_exame_com_o_selo_da_outra()
    {
        await using var db = fixture.CriarDbContext();
        var (principal, extra) = await SemearComExtraAsync(db);
        var solicitacao = await db.Solicitacoes.AsNoTracking()
            .Include(s => s.ExameImagem).ThenInclude(e => e!.TipoExame)
            .Include(s => s.ExamesImagem).ThenInclude(e => e.TipoExame)
            .Include(s => s.ExamesImagem).ThenInclude(e => e.SolicitacaoProcedimento)
            .SingleAsync(s => s.Id == principal.SolicitacaoId);

        var linhas = SolicitacoesService.LinhasDoPedido(solicitacao, new FiltroSolicitacoesDto(), null).ToList();

        Assert.Equal([principal.Id, extra.Id], linhas.Select(l => l.Id));
        Assert.Equal([extra.AccessionNumber], linhas[0].AccessionsVinculados!);
        Assert.Equal([principal.AccessionNumber], linhas[1].AccessionsVinculados!);
        // Cada linha mostra o procedimento do SEU exame — a do -1 é o joelho esquerdo.
        Assert.StartsWith("RADIOGRAFIA DE JOELHO (AP + LATERAL) ESQUERDO TESTE", linhas[1].ProcedimentoTexto);
        Assert.Equal("RADIOGRAFIA DE JOELHO (AP + LATERAL) DIREITO", linhas[0].ProcedimentoTexto);
    }

    // ---- semeadura ----

    /// <summary>Pedido de joelho D (principal, já com exame) + joelho E (2ª linha do SISREG, ainda
    /// só item) — como fica logo depois da importação acrescentar o procedimento (ticket #149).</summary>
    private static async Task<(ExameImagem Principal, SolicitacaoProcedimento ItemExtra)> SemearPedidoComDoisCodigosAsync(
        SmsMaisDbContext db)
    {
        var principal = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid());
        var solicitacao = principal.Solicitacao!;
        var sufixo = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        solicitacao.Categoria = CategoriaSolicitacao.Imagem;
        solicitacao.ProcedimentoCodigoSisreg = "1304085";
        solicitacao.ProcedimentoTexto = "RADIOGRAFIA DE JOELHO (AP + LATERAL) DIREITO";

        var agora = DateTime.UtcNow;
        db.SolicitacaoProcedimentos.Add(new SolicitacaoProcedimento
        {
            Id = Guid.CreateVersion7(), SolicitacaoId = solicitacao.Id, Ordem = 1,
            ProcedimentoCodigoSisreg = "1304085", ProcedimentoTexto = solicitacao.ProcedimentoTexto, CriadoEm = agora,
        });
        var extra = new SolicitacaoProcedimento
        {
            Id = Guid.CreateVersion7(), SolicitacaoId = solicitacao.Id, Ordem = 2,
            ProcedimentoCodigoSisreg = "1304086", ProcedimentoSigtapCodigo = "0204060125",
            // Nome único por execução: o tipo de exame é chaveado pelo nome em toda a base.
            ProcedimentoTexto = $"RADIOGRAFIA DE JOELHO (AP + LATERAL) ESQUERDO TESTE {sufixo}",
            CriadoEm = agora,
        };
        db.SolicitacaoProcedimentos.Add(extra);
        await db.SaveChangesAsync();
        return (principal, extra);
    }

    /// <summary>O pedido acima já com o exame extra criado e o envio à worklist ligado para o tipo
    /// dele na unidade (o par nasce desligado, como em produção).</summary>
    private async Task<(ExameImagem Principal, ExameImagem Extra)> SemearComExtraAsync(SmsMaisDbContext db)
    {
        var (principal, _) = await SemearPedidoComDoisCodigosAsync(db);
        await CriarSatelite(db).GarantirAsync(principal.Solicitacao!, null, default);
        await db.SaveChangesAsync();

        var extra = await db.ExamesImagem.SingleAsync(e => e.SolicitacaoId == principal.SolicitacaoId && e.Ordem == 1);
        var escopo = await db.TiposExameUnidade.SingleAsync(
            t => t.TipoExameId == extra.TipoExameId && t.UnidadeId == principal.Solicitacao!.UnidadeExecutanteId);
        escopo.EnviarParaWorklist = true;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return (principal, extra);
    }

    private static SateliteImagemSisreg CriarSatelite(SmsMaisDbContext db) => new(
        db,
        new GeradorIdentificadores(db),
        new ResolvedorTipoExameSisreg(db, NullLogger<ResolvedorTipoExameSisreg>.Instance),
        new EscopoExameUnidade(db),
        new UsuarioAtualAccessorFake(Guid.NewGuid()));

    private static IPacienteResolver Resolver(Guid pacienteId)
    {
        var resolver = Substitute.For<IPacienteResolver>();
        resolver.ResolverAsync(pacienteId, Arg.Any<CancellationToken>())
            .Returns(new PacienteResumo(
                pacienteId, "PACIENTE TESTE", SeedSolicitacao.CpfAleatorio(), null, null,
                Sexo.NaoInformado, SeedSolicitacao.TelefoneAleatorio()));
        return resolver;
    }

    private static SolicitacoesService CriarSolicitacoes(SmsMaisDbContext db, Guid pacienteId) => new(
        db,
        Substitute.For<IGeradorIdentificadores>(),
        Substitute.For<IDcm4cheeMwlClient>(),
        new ResolvedorEstacaoWorklist(db, new EscopoExameUnidade(db), NullLogger<ResolvedorEstacaoWorklist>.Instance),
        new EscopoExameUnidade(db),
        Substitute.For<INotificadorExame>(),
        new UsuarioAtualAccessorFake(Guid.NewGuid()),
        Resolver(pacienteId),
        Substitute.For<SMSMais.Core.Pacientes.IPacientesService>(),
        new DispensaContatoService(db, Substitute.For<IPacienteFhirClient>(),
            new UsuarioAtualAccessorFake(Guid.NewGuid()), NullLogger<DispensaContatoService>.Instance),
        new Lazy<ILaudoAssinaturaService>(() => Substitute.For<ILaudoAssinaturaService>()),
        new Lazy<IComunicacaoPacienteService>(() => Substitute.For<IComunicacaoPacienteService>()),
        Substitute.For<IRegistroErroService>(),
        Substitute.For<SMSMais.Core.Auditoria.IAuditoriaService>(),
        Substitute.For<SMSMais.Core.Common.Cid.ICidCatalogoService>(),
        NullLogger<SolicitacoesService>.Instance);
}
