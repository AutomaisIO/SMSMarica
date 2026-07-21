using Microsoft.Extensions.Logging.Abstractions;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Worklist;
using SMSMarica.Data;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Enums;
using SMSMarica.Tests.Infraestrutura;

namespace SMSMarica.Tests.Worklist;

/// <summary>
/// Estação (ScheduledStationAETitle) resolvida pelo equipamento da unidade executante.
/// Sem equipamento cadastrado o envio FALHA ("Sem equipamento configurado") — nunca cai
/// num AE genérico, que mandaria o exame de uma unidade para a estação de outra.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class ResolvedorEstacaoWorklistTests(PostgresFixture fixture)
{
    private static ResolvedorEstacaoWorklist CriarResolvedor(SmsMaricaDbContext db) =>
        new(db, NullLogger<ResolvedorEstacaoWorklist>.Instance);

    private static async Task AdicionarEquipamentoAsync(
        SmsMaricaDbContext db,
        Guid unidadeId,
        ModalidadeDicom modalidade,
        string? identificadorDicom,
        bool ativo = true,
        string? nome = null)
    {
        db.Equipamentos.Add(new Equipamento
        {
            Id = Guid.CreateVersion7(),
            Nome = nome ?? $"EQUIP {Guid.NewGuid().ToString("N")[..8]}",
            UnidadeId = unidadeId,
            ModalidadeDicom = modalidade,
            IdentificadorDicom = identificadorDicom,
            Ativo = ativo,
            CriadoEm = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private static async Task<ConflitoException> AssertSemEquipamentoAsync(
        SmsMaricaDbContext db, ExameImagem exame)
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
    public async Task Com_dois_equipamentos_a_escolha_e_estavel_pelo_nome()
    {
        await using var db = fixture.CriarDbContext();
        var exame = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid());
        var unidadeId = exame.Solicitacao!.UnidadeExecutanteId;
        await AdicionarEquipamentoAsync(db, unidadeId, ModalidadeDicom.MG, "SALA_B", nome: "Sala B");
        await AdicionarEquipamentoAsync(db, unidadeId, ModalidadeDicom.MG, "SALA_A", nome: "Sala A");

        var ae = await CriarResolvedor(db).ResolverAsync(exame);

        Assert.Equal("SALA_A", ae);
    }
}
