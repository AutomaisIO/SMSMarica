using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SMSMais.Core.Integracoes.SisregWeb.Importacao;
using SMSMais.Core.Solicitacoes.Identificadores;
using SMSMais.Core.Worklist;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Solicitacoes;

/// <summary>
/// MAPA, Holter e ECG são gráfico funcional no SIGTAP (0211) e no CDT saem de aparelhos que consultam
/// a worklist. Pedido que não é imagem ganha exame quando — e só quando — o tipo dele está no escopo
/// da unidade executante com a worklist ligada. Medido em 19/10/2026: 151 MAPAs agendadas sem exame.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class ExameGraficoFuncionalTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Grafico_funcional_no_escopo_com_worklist_ganha_exame_do_tipo_configurado()
    {
        await using var db = fixture.CriarDbContext();
        var (solicitacao, tipo) = await SemearAsync(db, comEscopo: true, enviarParaWorklist: true);

        Assert.Equal(ResultadoSatelite.Criado, await CriarSatelite(db).GarantirAsync(solicitacao, null, default));
        await db.SaveChangesAsync();

        var exame = await db.ExamesImagem.AsNoTracking().SingleAsync(e => e.SolicitacaoId == solicitacao.Id);
        Assert.Equal(tipo.Id, exame.TipoExameId);
        Assert.Equal(solicitacao.Id, exame.SolicitacaoPrincipalId);
        Assert.Equal(StatusSolicitacaoExame.Solicitada, exame.Status);
        // Nada vai ao PACS antes da recepção.
        Assert.Null(exame.ProximaTentativaEm);
    }

    [Fact]
    public async Task Grafico_funcional_com_worklist_desligada_na_unidade_fica_sem_exame()
    {
        await using var db = fixture.CriarDbContext();
        var (solicitacao, _) = await SemearAsync(db, comEscopo: true, enviarParaWorklist: false);

        Assert.Equal(ResultadoSatelite.NadaAFazer, await CriarSatelite(db).GarantirAsync(solicitacao, null, default));
        Assert.False(await db.ExamesImagem.AnyAsync(e => e.SolicitacaoId == solicitacao.Id));
    }

    [Fact]
    public async Task Grafico_funcional_fora_do_escopo_nao_cria_tipo_nem_exame()
    {
        await using var db = fixture.CriarDbContext();
        var (solicitacao, _) = await SemearAsync(db, comEscopo: false, enviarParaWorklist: false);
        // Procedimento que nenhum tipo conhece: o que é imagem criaria o tipo; o que não é, não.
        solicitacao.ProcedimentoTexto = $"ESPIROMETRIA TESTE {Guid.NewGuid():N}".ToUpperInvariant();
        await db.SaveChangesAsync();

        Assert.Equal(ResultadoSatelite.NadaAFazer, await CriarSatelite(db).GarantirAsync(solicitacao, null, default));
        await db.SaveChangesAsync();

        Assert.False(await db.ExamesImagem.AnyAsync(e => e.SolicitacaoId == solicitacao.Id));
        Assert.False(await db.TiposExame.AnyAsync(t => t.Nome == solicitacao.ProcedimentoTexto));
    }

    [Fact]
    public async Task Escopo_com_worklist_de_outra_unidade_nao_vale()
    {
        await using var db = fixture.CriarDbContext();
        var (solicitacao, tipo) = await SemearAsync(db, comEscopo: false, enviarParaWorklist: false);
        var outra = new Unidade { Id = Guid.NewGuid(), Nome = $"OUTRA {Guid.NewGuid():N}", CriadoEm = DateTime.UtcNow };
        db.Unidades.Add(outra);
        db.TiposExameUnidade.Add(new TipoExameUnidade
        {
            Id = Guid.CreateVersion7(), TipoExameId = tipo.Id, UnidadeId = outra.Id,
            EnviarParaWorklist = true, Ativo = true, CriadoEm = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        Assert.Null(await CriarSatelite(db).TipoComWorklistAsync(
            solicitacao.ProcedimentoTexto, solicitacao.UnidadeExecutanteId, default));
        Assert.Equal(tipo.Id, await CriarSatelite(db).TipoComWorklistAsync(
            solicitacao.ProcedimentoTexto, outra.Id, default));
    }

    // ---- semeadura ----

    /// <summary>Pedido de MAPA como a varredura diária grava: gráfico funcional, agendado, sem exame.
    /// O tipo existe (configurado por gente); o par com a unidade é opcional.</summary>
    private static async Task<(Solicitacao Solicitacao, TipoExame Tipo)> SemearAsync(
        SmsMaisDbContext db, bool comEscopo, bool enviarParaWorklist)
    {
        var sufixo = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        // Nome único por execução: o tipo de exame é chaveado pelo nome em toda a base.
        var nome = $"MONITORIZACAO AMBULATORIAL DE PRESSAO ARTERIAL (M.A.P.A) {sufixo}";
        var tipo = new TipoExame
        {
            Id = Guid.CreateVersion7(), Nome = nome, CodigoSisreg = "9290002",
            ModalidadeDicom = ModalidadeDicom.OT, RequestedProcedureDescription = nome,
            ScheduledProcedureStepDescription = nome, CriadoEm = DateTime.UtcNow,
        };
        var unidade = new Unidade { Id = Guid.NewGuid(), Nome = $"CDT TESTE {sufixo}", CriadoEm = DateTime.UtcNow };
        var solicitacao = new Solicitacao
        {
            Id = Guid.NewGuid(),
            PacienteId = Guid.NewGuid(),
            Categoria = CategoriaSolicitacao.GraficoFuncional,
            ProcedimentoTexto = nome,
            ProcedimentoCodigoSisreg = "9290002",
            ProcedimentoSigtapCodigo = "0211020052",
            UnidadeExecutanteId = unidade.Id,
            SolicitanteNome = "DR TESTE",
            Status = StatusSolicitacao.Agendada,
            Prioridade = PrioridadeSolicitacao.Eletiva,
            DataAgendada = DateTime.UtcNow.AddDays(3),
            CriadoEm = DateTime.UtcNow,
        };
        db.TiposExame.Add(tipo);
        db.Unidades.Add(unidade);
        if (comEscopo)
            db.TiposExameUnidade.Add(new TipoExameUnidade
            {
                Id = Guid.CreateVersion7(), TipoExameId = tipo.Id, UnidadeId = unidade.Id,
                EnviarParaWorklist = enviarParaWorklist, Ativo = true, CriadoEm = DateTime.UtcNow,
            });
        db.Solicitacoes.Add(solicitacao);
        await db.SaveChangesAsync();
        return (solicitacao, tipo);
    }

    private static SateliteImagemSisreg CriarSatelite(SmsMaisDbContext db) => new(
        db,
        new GeradorIdentificadores(db),
        new ResolvedorTipoExameSisreg(db, NullLogger<ResolvedorTipoExameSisreg>.Instance),
        new EscopoExameUnidade(db),
        new UsuarioAtualAccessorFake(Guid.NewGuid()));
}
