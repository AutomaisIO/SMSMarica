using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SMSMarica.Core.Estatisticas;
using SMSMarica.Core.Estatisticas.Dtos;
using SMSMarica.Core.Pacientes.Fhir;
using SMSMarica.Data;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Enums;
using SMSMarica.Tests.Infraestrutura;

namespace SMSMarica.Tests.Estatisticas;

/// <summary>
/// Agregados gerenciais de exames de imagem. O que estes testes protegem é o CASAMENTO
/// exame ↔ laudo, que não é um simples "campo igual a campo": um exame pode ser conhecido por mais
/// de um StudyInstanceUID — o que geramos ao publicar o item na worklist e o REAL do equipamento,
/// quando ele não honra o da worklist e o estudo precisa ser conciliado depois (ExameAssociacao).
///
/// Em julho/2026 esse detalhe custou caro: o mamógrafo do CDT passou três dias gerando UID próprio,
/// e o painel deu 197 exames "aguardando laudo" que já estavam laudados — 46,8% de laudados onde o
/// real era 88,3%. O relatório casava só pelo UID gravado no exame; a tela de Laudos, que já
/// consultava a associação, mostrava a verdade. Se estes testes quebrarem, o painel voltou a mentir.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class EstatisticasExamesImagemTests(PostgresFixture fixture)
{
    /// <summary>
    /// O caso de julho/2026: laudo gravado sobre o UID REAL do equipamento, exame conciliado por
    /// ExameAssociacao. Conta como laudado — e, por consequência, sai de "aguardando laudo".
    /// </summary>
    [Fact]
    public async Task Laudo_no_estudo_conciliado_conta_como_laudado()
    {
        await using var db = fixture.CriarDbContext();
        var exame = await CriarExameRealizadoAsync(db);

        var uidDoEquipamento = $"1.2.392.200036.{Random.Shared.NextInt64(1_000_000_000):D10}";
        await ConciliarAsync(db, exame, uidDoEquipamento);
        await FinalizarLaudoAsync(db, uidDoEquipamento);

        var resumo = await ResumoAsync(db, exame);

        Assert.Equal(1, resumo.Realizados);
        Assert.Equal(1, resumo.Laudados);
        Assert.Equal(0, resumo.AguardandoLaudo);
        Assert.Equal(100, resumo.PercentualLaudados);
        Assert.Equal(1, resumo.MedicosLaudando);
    }

    /// <summary>Caminho feliz — equipamento honrou o UID da worklist, sem associação nenhuma.</summary>
    [Fact]
    public async Task Laudo_no_proprio_uid_do_exame_continua_contando()
    {
        await using var db = fixture.CriarDbContext();
        var exame = await CriarExameRealizadoAsync(db);
        await FinalizarLaudoAsync(db, exame.StudyInstanceUID);

        var resumo = await ResumoAsync(db, exame);

        Assert.Equal(1, resumo.Laudados);
        Assert.Equal(0, resumo.AguardandoLaudo);
    }

    /// <summary>
    /// Sem laudo finalizado o exame é pendente de verdade — rascunho aberto não vale como laudo.
    /// É o outro lado da invariante: corrigir o falso pendente não pode criar o falso laudado.
    /// </summary>
    [Fact]
    public async Task Rascunho_nao_conta_como_laudado()
    {
        await using var db = fixture.CriarDbContext();
        var exame = await CriarExameRealizadoAsync(db);
        await CriarLaudoAsync(db, exame.StudyInstanceUID, StatusLaudo.Rascunho, finalizadoEm: null);

        var resumo = await ResumoAsync(db, exame);

        Assert.Equal(0, resumo.Laudados);
        Assert.Equal(1, resumo.AguardandoLaudo);
    }

    /// <summary>Desassociar (soft-delete da associação) devolve o exame para a fila de laudo.</summary>
    [Fact]
    public async Task Associacao_desfeita_nao_traz_mais_o_laudo()
    {
        await using var db = fixture.CriarDbContext();
        var exame = await CriarExameRealizadoAsync(db);
        var uidDoEquipamento = $"1.2.392.200036.{Random.Shared.NextInt64(1_000_000_000):D10}";
        var associacao = await ConciliarAsync(db, exame, uidDoEquipamento);
        await FinalizarLaudoAsync(db, uidDoEquipamento);

        Assert.Equal(1, (await ResumoAsync(db, exame)).Laudados);

        associacao.ExcluidoEm = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var resumo = await ResumoAsync(db, exame);
        Assert.Equal(0, resumo.Laudados);
        Assert.Equal(1, resumo.AguardandoLaudo);
    }

    /// <summary>
    /// "Laudados" conta EXAME coberto; "Laudos emitidos" conta o trabalho do médico, retificação
    /// inclusa. Os dois números divergirem é o correto — foi o que levantou a investigação, e
    /// achatar um no outro esconderia o volume de retificação.
    /// </summary>
    [Fact]
    public async Task Retificacao_soma_em_laudos_emitidos_mas_nao_em_laudados()
    {
        await using var db = fixture.CriarDbContext();
        var exame = await CriarExameRealizadoAsync(db);
        var v1 = await FinalizarLaudoAsync(db, exame.StudyInstanceUID, versao: 1,
            finalizadoEm: DateTime.UtcNow.AddHours(-2));
        await FinalizarLaudoAsync(db, exame.StudyInstanceUID, versao: 2,
            finalizadoEm: DateTime.UtcNow.AddHours(-1), anteriorId: v1.Id);

        var resumo = await ResumoAsync(db, exame);

        Assert.Equal(1, resumo.Laudados);
        Assert.Equal(2, resumo.LaudosEmitidos);
        Assert.Equal(1, resumo.MedicosLaudando);
    }

    // ===================== assinatura (produção de quem lauda) =====================

    /// <summary>Laudo escrito só vira entrega quando assinado — é o que separa os dois eixos.</summary>
    [Fact]
    public async Task Laudo_sem_assinatura_conta_como_laudado_mas_nao_como_assinado()
    {
        await using var db = fixture.CriarDbContext();
        var exame = await CriarExameRealizadoAsync(db);
        await FinalizarLaudoAsync(db, exame.StudyInstanceUID);

        var resumo = await ResumoAsync(db, exame);

        Assert.Equal(1, resumo.Laudados);
        Assert.Equal(0, resumo.Assinados);
        Assert.Equal(1, resumo.AguardandoAssinatura);
        Assert.Equal(0, resumo.PercentualAssinados);
    }

    [Fact]
    public async Task Laudo_com_assinatura_concluida_conta_como_assinado()
    {
        await using var db = fixture.CriarDbContext();
        var exame = await CriarExameRealizadoAsync(db);
        var laudo = await FinalizarLaudoAsync(db, exame.StudyInstanceUID);
        await AssinarAsync(db, laudo, StatusAssinatura.Concluida);

        var resumo = await ResumoAsync(db, exame);

        Assert.Equal(1, resumo.Assinados);
        Assert.Equal(0, resumo.AguardandoAssinatura);
        Assert.Equal(100, resumo.PercentualAssinados);
        Assert.NotNull(resumo.TempoMedioLaudoAssinaturaHoras);
        Assert.Equal(1, resumo.AmostraLaudoAssinatura);
    }

    /// <summary>
    /// Assinatura em curso (ou cancelada) NÃO é assinatura. Contá-la mostraria como entregue um
    /// laudo que o médico ainda nem aprovou — o pior erro possível num painel de produção.
    /// </summary>
    [Theory]
    [InlineData(StatusAssinatura.Iniciada)]
    [InlineData(StatusAssinatura.AguardandoAssinatura)]
    [InlineData(StatusAssinatura.AguardandoAprovacao)]
    [InlineData(StatusAssinatura.Cancelada)]
    [InlineData(StatusAssinatura.Falhou)]
    public async Task Assinatura_nao_concluida_nao_conta(StatusAssinatura status)
    {
        await using var db = fixture.CriarDbContext();
        var exame = await CriarExameRealizadoAsync(db);
        var laudo = await FinalizarLaudoAsync(db, exame.StudyInstanceUID);
        await AssinarAsync(db, laudo, status);

        var resumo = await ResumoAsync(db, exame);

        Assert.Equal(0, resumo.Assinados);
        Assert.Equal(1, resumo.AguardandoAssinatura);
    }

    /// <summary>
    /// Retificar depois de assinar devolve o exame para a fila de assinatura: quem vale é a versão
    /// VIGENTE. A assinatura da versão superada continua contando como trabalho feito (LaudosAssinados),
    /// mas não como exame entregue.
    /// </summary>
    [Fact]
    public async Task Retificacao_apos_assinar_volta_a_aguardar_assinatura()
    {
        await using var db = fixture.CriarDbContext();
        var exame = await CriarExameRealizadoAsync(db);
        var v1 = await FinalizarLaudoAsync(db, exame.StudyInstanceUID, versao: 1,
            finalizadoEm: DateTime.UtcNow.AddHours(-4));
        await AssinarAsync(db, v1, StatusAssinatura.Concluida, DateTime.UtcNow.AddHours(-3));
        await FinalizarLaudoAsync(db, exame.StudyInstanceUID, versao: 2,
            finalizadoEm: DateTime.UtcNow.AddHours(-1), anteriorId: v1.Id);

        var resumo = await ResumoAsync(db, exame);

        Assert.Equal(1, resumo.Laudados);
        Assert.Equal(0, resumo.Assinados);
        Assert.Equal(1, resumo.AguardandoAssinatura);
        Assert.Equal(2, resumo.LaudosEmitidos);
        Assert.Equal(1, resumo.LaudosAssinados);
    }

    /// <summary>A linha da médica: exame coberto, laudo escrito e laudo assinado são três números.</summary>
    [Fact]
    public async Task Producao_por_medico_separa_exame_de_laudo_e_de_assinatura()
    {
        await using var db = fixture.CriarDbContext();
        var exame = await CriarExameRealizadoAsync(db);
        var v1 = await FinalizarLaudoAsync(db, exame.StudyInstanceUID, versao: 1,
            finalizadoEm: DateTime.UtcNow.AddHours(-4));
        var v2 = await FinalizarLaudoAsync(db, exame.StudyInstanceUID, versao: 2,
            finalizadoEm: DateTime.UtcNow.AddHours(-1), anteriorId: v1.Id);
        await AssinarAsync(db, v2, StatusAssinatura.Concluida);

        var dto = await ObterAsync(db, exame);
        var linha = Assert.Single(dto.PorMedico);

        Assert.Equal("DRA TESTE", linha.Medico);
        Assert.Equal(1, linha.ExamesLaudados);
        Assert.Equal(2, linha.LaudosEmitidos);
        Assert.Equal(1, linha.LaudosAssinados);
    }

    // ===================== apoio =====================

    private async Task<ExamesImagemResumoDto> ResumoAsync(SmsMaricaDbContext db, ExameImagem exame) =>
        (await ObterAsync(db, exame)).Resumo;

    private async Task<EstatisticasExamesImagemDto> ObterAsync(SmsMaricaDbContext db, ExameImagem exame)
    {
        var unidadeId = (await db.Solicitacoes.FindAsync(exame.SolicitacaoId))!.UnidadeExecutanteId;
        var dia = DateOnly.FromDateTime(exame.RealizadoEm!.Value);

        // Sem usuário no contexto o escopo é "tudo" (EscopoUnidade §1); o recorte vem da unidade.
        var resolver = Substitute.For<IPacienteResolver>();
        var servico = new EstatisticasService(
            db, new UsuarioAtualAccessorFake(), resolver, NullLogger<EstatisticasService>.Instance);

        return await servico.ObterExamesImagemAsync(
            dia, dia, unidadeId, ModalidadeDicom.MG, tipoExameId: null);
    }

    private static async Task<LaudoAssinatura> AssinarAsync(
        SmsMaricaDbContext db, Laudo laudo, StatusAssinatura status, DateTime? assinadoEm = null)
    {
        var a = new LaudoAssinatura
        {
            Id = Guid.NewGuid(),
            LaudoId = laudo.Id,
            MedicoId = laudo.MedicoId,
            Status = status,
            // Só a Concluida carimba o instante — é o que o agregado lê.
            AssinadoEm = status == StatusAssinatura.Concluida
                ? assinadoEm ?? DateTime.UtcNow.AddMinutes(-30)
                : null,
            CriadoEm = DateTime.UtcNow.AddHours(-1),
        };
        db.LaudoAssinaturas.Add(a);
        await db.SaveChangesAsync();
        return a;
    }

    private static async Task<ExameImagem> CriarExameRealizadoAsync(SmsMaricaDbContext db)
    {
        var exame = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid());
        exame.Status = StatusSolicitacaoExame.Realizada;
        exame.RealizadoEm = DateTime.UtcNow.AddHours(-6);
        await db.SaveChangesAsync();
        return exame;
    }

    private static async Task<ExameAssociacao> ConciliarAsync(
        SmsMaricaDbContext db, ExameImagem exame, string uidReal)
    {
        var a = new ExameAssociacao
        {
            Id = Guid.NewGuid(),
            StudyInstanceUID = uidReal,
            ExameImagemId = exame.Id,
            PacienteId = Guid.NewGuid(),
            Origem = OrigemAssociacaoExame.Automatica,
            CriadoEm = DateTime.UtcNow,
        };
        db.ExameAssociacoes.Add(a);
        await db.SaveChangesAsync();
        return a;
    }

    private static Task<Laudo> FinalizarLaudoAsync(
        SmsMaricaDbContext db, string uid, int versao = 1,
        DateTime? finalizadoEm = null, Guid? anteriorId = null) =>
        CriarLaudoAsync(db, uid, StatusLaudo.Finalizado,
            finalizadoEm ?? DateTime.UtcNow.AddHours(-1), versao, anteriorId);

    private static async Task<Laudo> CriarLaudoAsync(
        SmsMaricaDbContext db, string uid, StatusLaudo status, DateTime? finalizadoEm,
        int versao = 1, Guid? anteriorId = null)
    {
        var l = new Laudo
        {
            Id = Guid.NewGuid(),
            StudyInstanceUID = uid,
            Versao = versao,
            LaudoAnteriorId = anteriorId,
            MedicoId = MedicoFixo,
            MedicoNomeSnapshot = "DRA TESTE",
            MedicoCrmSnapshot = "CRM-RJ 000000",
            Titulo = "Mamografia bilateral",
            ConteudoHtml = "<p>laudo de teste</p>",
            Status = status,
            FinalizadoEm = finalizadoEm,
            CriadoEm = DateTime.UtcNow.AddHours(-2),
        };
        db.Laudos.Add(l);
        await db.SaveChangesAsync();
        return l;
    }

    /// <summary>Mesmo médico em todos os laudos: "Médicos laudando" é contagem DISTINTA.</summary>
    private static readonly Guid MedicoFixo = Guid.Parse("00000000-0000-0000-0000-0000000000d1");
}
