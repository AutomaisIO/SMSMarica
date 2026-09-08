using Microsoft.Extensions.Logging.Abstractions;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Worklist;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Worklist;

/// <summary>
/// Estação (ScheduledStationAETitle) resolvida pelo equipamento da unidade executante.
/// Sem equipamento cadastrado o envio FALHA ("Sem equipamento configurado") — nunca cai
/// num AE genérico, que mandaria o exame de uma unidade para a estação de outra.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class ResolvedorEstacaoWorklistTests(PostgresFixture fixture)
{
    private static ResolvedorEstacaoWorklist CriarResolvedor(SmsMaisDbContext db) =>
        new(db, new EscopoExameUnidade(db), NullLogger<ResolvedorEstacaoWorklist>.Instance);

    private static async Task<Equipamento> AdicionarEquipamentoAsync(
        SmsMaisDbContext db,
        Guid unidadeId,
        ModalidadeDicom modalidade,
        string? identificadorDicom,
        bool ativo = true,
        string? nome = null)
    {
        var equipamento = new Equipamento
        {
            Id = Guid.CreateVersion7(),
            Nome = nome ?? $"EQUIP {Guid.NewGuid().ToString("N")[..8]}",
            UnidadeId = unidadeId,
            ModalidadeDicom = modalidade,
            IdentificadorDicom = identificadorDicom,
            Ativo = ativo,
            CriadoEm = DateTime.UtcNow,
        };
        db.Equipamentos.Add(equipamento);
        await db.SaveChangesAsync();
        return equipamento;
    }

    /// <summary>Outra unidade qualquer (FK real — equipamento não aceita unidade inexistente).</summary>
    private static async Task<Guid> OutraUnidadeAsync(SmsMaisDbContext db)
    {
        var unidade = new Unidade
        {
            Id = Guid.NewGuid(),
            Nome = $"OUTRA UNIDADE {Guid.NewGuid().ToString("N")[..8]}",
            CriadoEm = DateTime.UtcNow,
        };
        db.Unidades.Add(unidade);
        await db.SaveChangesAsync();
        return unidade.Id;
    }

    private static async Task<ConflitoException> AssertSemEquipamentoAsync(
        SmsMaisDbContext db, ExameImagem exame)
    {
        var ex = await Assert.ThrowsAsync<ConflitoException>(
            () => CriarResolvedor(db).ResolverAsync(exame));
        Assert.Contains("Sem equipamento configurado", ex.Message, StringComparison.OrdinalIgnoreCase);
        return ex;
    }

    [Fact]
    public async Task Sem_equipamento_cadastrado_recusa_o_envio()
    {
        await using var db = fixture.CriarDbContext();
        var exame = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid());

        await AssertSemEquipamentoAsync(db, exame);
    }

    [Fact]
    public async Task Equipamento_da_unidade_na_modalidade_define_o_AE()
    {
        await using var db = fixture.CriarDbContext();
        var exame = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid()); // TipoExame = MG
        await AdicionarEquipamentoAsync(db, exame.Solicitacao!.UnidadeExecutanteId, ModalidadeDicom.MG, "MAMO_X");

        var ae = await CriarResolvedor(db).ResolverAsync(exame);

        Assert.Equal("MAMO_X", ae);
    }

    [Fact]
    public async Task Equipamento_de_outra_modalidade_nao_serve_ao_exame()
    {
        await using var db = fixture.CriarDbContext();
        var exame = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid()); // MG
        await AdicionarEquipamentoAsync(db, exame.Solicitacao!.UnidadeExecutanteId, ModalidadeDicom.US, "US_CMI");

        await AssertSemEquipamentoAsync(db, exame);
    }

    [Theory]
    [InlineData(null)]                    // sem identificador
    [InlineData("AE COM ESPACO")]         // espaço no meio
    [InlineData("AE_LONGO_DEMAIS_123")]   // > 16 caracteres
    public async Task Identificador_ausente_ou_invalido_recusa_o_envio(string? identificador)
    {
        await using var db = fixture.CriarDbContext();
        var exame = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid());
        await AdicionarEquipamentoAsync(db, exame.Solicitacao!.UnidadeExecutanteId, ModalidadeDicom.MG, identificador);

        await AssertSemEquipamentoAsync(db, exame);
    }

    [Fact]
    public async Task Equipamento_inativo_e_ignorado()
    {
        await using var db = fixture.CriarDbContext();
        var exame = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid());
        await AdicionarEquipamentoAsync(db, exame.Solicitacao!.UnidadeExecutanteId, ModalidadeDicom.MG, "MAMO_OFF", ativo: false);

        await AssertSemEquipamentoAsync(db, exame);
    }

    [Fact]
    public async Task Com_dois_equipamentos_e_sem_escolha_recusa_em_vez_de_sortear()
    {
        await using var db = fixture.CriarDbContext();
        var exame = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid());
        var unidadeId = exame.Solicitacao!.UnidadeExecutanteId;
        await AdicionarEquipamentoAsync(db, unidadeId, ModalidadeDicom.MG, "SALA_B", nome: "Sala B");
        await AdicionarEquipamentoAsync(db, unidadeId, ModalidadeDicom.MG, "SALA_A", nome: "Sala A");

        var ex = await Assert.ThrowsAsync<ConflitoException>(() => CriarResolvedor(db).ResolverAsync(exame));

        Assert.Contains("Selecione", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Sala A", ex.Message, StringComparison.Ordinal); // a mensagem diz QUAIS
        Assert.Contains("Sala B", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Equipamento_escolhido_no_exame_manda()
    {
        await using var db = fixture.CriarDbContext();
        var exame = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid());
        var unidadeId = exame.Solicitacao!.UnidadeExecutanteId;
        await AdicionarEquipamentoAsync(db, unidadeId, ModalidadeDicom.MG, "SALA_A", nome: "Sala A");
        var salaB = await AdicionarEquipamentoAsync(db, unidadeId, ModalidadeDicom.MG, "SALA_B", nome: "Sala B");

        exame.EquipamentoId = salaB.Id;   // recepção escolheu a sala B
        await db.SaveChangesAsync();

        var ae = await CriarResolvedor(db).ResolverAsync(exame);

        Assert.Equal("SALA_B", ae); // e não a primeira em ordem alfabética
    }

    [Fact]
    public async Task Escolha_que_saiu_do_ar_volta_a_deduzir()
    {
        // Equipamento escolhido foi desativado depois da autorização: com um único substituto
        // válido, o exame segue em vez de travar.
        await using var db = fixture.CriarDbContext();
        var exame = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid());
        var unidadeId = exame.Solicitacao!.UnidadeExecutanteId;
        var desativado = await AdicionarEquipamentoAsync(db, unidadeId, ModalidadeDicom.MG, "SALA_OFF", ativo: false, nome: "Sala Off");
        await AdicionarEquipamentoAsync(db, unidadeId, ModalidadeDicom.MG, "SALA_OK", nome: "Sala Ok");

        exame.EquipamentoId = desativado.Id;
        await db.SaveChangesAsync();

        var ae = await CriarResolvedor(db).ResolverAsync(exame);

        Assert.Equal("SALA_OK", ae);
    }

    [Fact]
    public async Task Candidatos_lista_apenas_os_da_unidade_e_modalidade()
    {
        await using var db = fixture.CriarDbContext();
        var exame = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid()); // MG
        var unidadeId = exame.Solicitacao!.UnidadeExecutanteId;
        await AdicionarEquipamentoAsync(db, unidadeId, ModalidadeDicom.MG, "SALA_B", nome: "Sala B");
        await AdicionarEquipamentoAsync(db, unidadeId, ModalidadeDicom.MG, "SALA_A", nome: "Sala A");
        await AdicionarEquipamentoAsync(db, unidadeId, ModalidadeDicom.US, "US_X", nome: "Ultrassom");   // outra modalidade
        await AdicionarEquipamentoAsync(db, await OutraUnidadeAsync(db), ModalidadeDicom.MG, "OUTRA_UNID");

        var candidatos = await CriarResolvedor(db).ListarCandidatosAsync(exame);

        Assert.Equal(["Sala A", "Sala B"], candidatos.Select(c => c.Nome)); // ordenado por nome
        Assert.Equal(["SALA_A", "SALA_B"], candidatos.Select(c => c.AeTitle));
    }
}
