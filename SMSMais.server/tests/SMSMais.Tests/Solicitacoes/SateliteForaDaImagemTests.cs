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
/// Adendo de 20/10/2026 ao ADR-0021: Holter e MAPA seguem Gráfico/funcional, mas ganham exame
/// (accession, Study, worklist) porque o TIPO deles tem modalidade DICOM — o laudo volta em PDF pelo
/// PACS, casado pelo accession/Study. Fora da imagem o tipo nunca é criado; e o reparo retroativo
/// é só para o que não é imagem (a carga de 08/09 deixou ~512 mil imagens antigas sem satélite).
/// </summary>
[Collection(nameof(PostgresCollection))]
public class SateliteForaDaImagemTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Holter_grafico_funcional_ganha_exame_com_o_tipo_que_ja_existe()
    {
        await using var db = fixture.CriarDbContext();
        var sufixo = Sufixo();
        var tipo = await SemearTipoAsync(db, $"HOLTER 24 HORAS TESTE {sufixo}", ModalidadeDicom.ECG);
        var solicitacao = await SemearSolicitacaoAsync(db, CategoriaSolicitacao.GraficoFuncional, tipo.Nome, DateTime.UtcNow.AddDays(3));

        Assert.Equal(ResultadoSatelite.Criado, await CriarSatelite(db).GarantirAsync(solicitacao, null, default));
        await db.SaveChangesAsync();

        var exame = await db.ExamesImagem.AsNoTracking().SingleAsync(e => e.SolicitacaoId == solicitacao.Id);
        Assert.Equal(tipo.Id, exame.TipoExameId);
        Assert.StartsWith("2.25.", exame.StudyInstanceUID);
        Assert.False(string.IsNullOrEmpty(exame.AccessionNumber));
        // Nada vai ao PACS antes da recepção.
        Assert.Null(exame.ProximaTentativaEm);
        // A categoria não muda: Holter é gráfico/funcional — só ganhou o exame.
        Assert.Equal(CategoriaSolicitacao.GraficoFuncional,
            (await db.Solicitacoes.AsNoTracking().SingleAsync(s => s.Id == solicitacao.Id)).Categoria);
    }

    [Fact]
    public async Task Grafico_funcional_sem_tipo_nao_ganha_exame_nem_cria_tipo()
    {
        await using var db = fixture.CriarDbContext();
        var nome = $"AUDIOMETRIA TESTE {Sufixo()}";
        var solicitacao = await SemearSolicitacaoAsync(db, CategoriaSolicitacao.GraficoFuncional, nome, DateTime.UtcNow.AddDays(3));

        Assert.Equal(ResultadoSatelite.NadaAFazer, await CriarSatelite(db).GarantirAsync(solicitacao, null, default));
        await db.SaveChangesAsync();

        Assert.False(await db.ExamesImagem.AnyAsync(e => e.SolicitacaoId == solicitacao.Id));
        // Fora da imagem o resolvedor nunca cria tipo — senão cada EEG importado viraria um.
        Assert.False(await db.TiposExame.AnyAsync(t => t.Nome == nome));
    }

    [Fact]
    public async Task Grafico_funcional_com_tipo_sem_modalidade_nao_ganha_exame()
    {
        await using var db = fixture.CriarDbContext();
        var tipo = await SemearTipoAsync(db, $"ELETROENCEFALOGRAMA TESTE {Sufixo()}", ModalidadeDicom.Indefinida);
        var solicitacao = await SemearSolicitacaoAsync(db, CategoriaSolicitacao.GraficoFuncional, tipo.Nome, DateTime.UtcNow.AddDays(3));

        Assert.Equal(ResultadoSatelite.NadaAFazer, await CriarSatelite(db).GarantirAsync(solicitacao, null, default));
        await db.SaveChangesAsync();

        Assert.False(await db.ExamesImagem.AnyAsync(e => e.SolicitacaoId == solicitacao.Id));
    }

    [Fact]
    public async Task Reparo_retroativo_pega_o_mapa_passado_e_deixa_a_imagem_passada()
    {
        await using var db = fixture.CriarDbContext();
        var sufixo = Sufixo();
        var tipoMapa = await SemearTipoAsync(db, $"MAPA TESTE {sufixo}", ModalidadeDicom.OT);
        var passado = DateTime.UtcNow.AddDays(-60);
        var mapaPassado = await SemearSolicitacaoAsync(db, CategoriaSolicitacao.GraficoFuncional, tipoMapa.Nome, passado);
        var imagemPassada = await SemearSolicitacaoAsync(db, CategoriaSolicitacao.Imagem, $"RADIOGRAFIA TESTE {sufixo}", passado);

        // Sem o pedido explícito, o passado fica de fora — inclusive fora da imagem.
        await RepararTudoAsync(incluirAnterioresForaDaImagem: false);
        await using (var leitura = fixture.CriarDbContext())
            Assert.False(await leitura.ExamesImagem.AnyAsync(e => e.SolicitacaoId == mapaPassado.Id));

        await RepararTudoAsync(incluirAnterioresForaDaImagem: true);

        await using var depois = fixture.CriarDbContext();
        var exameMapa = await depois.ExamesImagem.AsNoTracking().SingleAsync(e => e.SolicitacaoId == mapaPassado.Id);
        Assert.Equal(tipoMapa.Id, exameMapa.TipoExameId);
        // Na imagem o corte "de hoje em diante" vale sempre.
        Assert.False(await depois.ExamesImagem.AnyAsync(e => e.SolicitacaoId == imagemPassada.Id));
    }

    // ---- apoio ----

    private async Task RepararTudoAsync(bool incluirAnterioresForaDaImagem)
    {
        Guid? cursor = null;
        ReparoSatelitesResultado resultado;
        do
        {
            await using var db = fixture.CriarDbContext();
            resultado = await CriarSatelite(db).RepararAsync(2000, cursor, incluirAnterioresForaDaImagem, default);
            cursor = resultado.Cursor;
        }
        while (resultado.Restantes > 0);
    }

    private static string Sufixo() => Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    /// <summary>Tipo como o resolvedor da importação o cria — o nome é a chave, em toda a base.</summary>
    private static async Task<TipoExame> SemearTipoAsync(SmsMaisDbContext db, string nome, ModalidadeDicom modalidade)
    {
        var tipo = new TipoExame
        {
            Id = Guid.CreateVersion7(),
            Nome = nome,
            ModalidadeDicom = modalidade,
            RequestedProcedureDescription = nome,
            ScheduledProcedureStepDescription = nome,
            CodigosProtocolo = [],
            Ativo = true,
            EnviarParaWorklist = false,
            CriadoEm = DateTime.UtcNow,
        };
        db.TiposExame.Add(tipo);
        await db.SaveChangesAsync();
        return tipo;
    }

    /// <summary>Solicitação importada do SISREG ainda SEM exame (o caso do Holter de antes de 05/10).</summary>
    private static async Task<Solicitacao> SemearSolicitacaoAsync(
        SmsMaisDbContext db, CategoriaSolicitacao categoria, string procedimento, DateTime dataAgendada)
    {
        var unidade = new Unidade { Id = Guid.NewGuid(), Nome = $"UNIDADE TESTE {Sufixo()}", CriadoEm = DateTime.UtcNow };
        var solicitacao = new Solicitacao
        {
            Id = Guid.CreateVersion7(),
            PacienteId = Guid.NewGuid(),
            Categoria = categoria,
            ProcedimentoTexto = procedimento,
            UnidadeExecutanteId = unidade.Id,
            SolicitanteNome = "DR TESTE",
            Status = StatusSolicitacao.Agendada,
            Prioridade = PrioridadeSolicitacao.Eletiva,
            DataAgendada = dataAgendada,
            CriadoEm = DateTime.UtcNow,
        };
        db.Unidades.Add(unidade);
        db.Solicitacoes.Add(solicitacao);
        await db.SaveChangesAsync();
        return solicitacao;
    }

    private static SateliteImagemSisreg CriarSatelite(SmsMaisDbContext db) => new(
        db,
        new GeradorIdentificadores(db),
        new ResolvedorTipoExameSisreg(db, NullLogger<ResolvedorTipoExameSisreg>.Instance),
        new EscopoExameUnidade(db),
        new UsuarioAtualAccessorFake(Guid.NewGuid()));
}
