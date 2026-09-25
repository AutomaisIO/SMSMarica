using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SMSMais.Core.Associacoes.Dtos;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Pacs;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Associacoes;

/// <summary>
/// Associar de novo o ORIGINAL de uma reescrita não pode gerar outro estudo.
///
/// <para>A rejeição do original deixa nele uma nota KO, e o equipamento às vezes manda instâncias
/// depois da associação — nos dois casos o original volta à lista. Cada nova associação reescrevia
/// o que sobrou num estudo novo: o accession 260811143 chegou a 8 estudos no PACS em 16–17/09/2026,
/// alguns só com KO. Agora a sobra é anexada ao estudo que já existe.</para>
/// </summary>
[Collection(nameof(PostgresCollection))]
public class AssociacaoSobraReescritaTests(PostgresFixture fixture)
{
    private static string NovoUid() => $"2.25.{Random.Shared.NextInt64(1, long.MaxValue)}";

    [Fact]
    public async Task Original_ja_reescrito_para_o_mesmo_exame_anexa_e_nao_cria_estudo_novo()
    {
        await using var db = fixture.CriarDbContext();
        var exame = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid());
        var original = NovoUid();
        var reescrito = NovoUid();
        db.ExameAssociacoes.Add(new ExameAssociacao
        {
            Id = Guid.CreateVersion7(),
            StudyInstanceUID = reescrito,
            StudyInstanceUidOriginal = original,
            ExameImagemId = exame.Id,
            PacienteId = Guid.NewGuid(),
            Origem = OrigemAssociacaoExame.Manual,
            CriadoEm = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var reescritor = Substitute.For<IPacsReescritorEstudoClient>();
        reescritor.AnexarAoEstudoAsync(original, reescrito, Arg.Any<IdentidadeDicom>(), Arg.Any<CancellationToken>())
            .Returns(new EstudoReescrito(reescrito, 2));

        var dto = await AssociacaoCorridaConciliadorTests.CriarService(db, reescritor).AssociarAsync(
            new AssociarExameRequest(original, exame.AccessionNumber, null));

        Assert.Equal(reescrito, dto.StudyInstanceUID);
        await reescritor.DidNotReceiveWithAnyArgs().ReescreverIdentidadeAsync(default!, default!, default);
        await reescritor.Received(1).AnexarAoEstudoAsync(
            original, reescrito, Arg.Any<IdentidadeDicom>(), Arg.Any<CancellationToken>());
        Assert.Equal(1, await db.ExameAssociacoes.CountAsync(
            a => a.ExameImagemId == exame.Id && a.ExcluidoEm == null));
    }

    [Fact]
    public async Task Original_ja_reescrito_para_outro_exame_e_conflito_sem_tocar_no_pacs()
    {
        await using var db = fixture.CriarDbContext();
        var dono = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid());
        var outro = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid());
        var original = NovoUid();
        db.ExameAssociacoes.Add(new ExameAssociacao
        {
            Id = Guid.CreateVersion7(),
            StudyInstanceUID = NovoUid(),
            StudyInstanceUidOriginal = original,
            ExameImagemId = dono.Id,
            PacienteId = Guid.NewGuid(),
            Origem = OrigemAssociacaoExame.Manual,
            CriadoEm = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var reescritor = Substitute.For<IPacsReescritorEstudoClient>();
        var ex = await Assert.ThrowsAsync<ConflitoException>(() =>
            AssociacaoCorridaConciliadorTests.CriarService(db, reescritor).AssociarAsync(
                new AssociarExameRequest(original, outro.AccessionNumber, null)));

        Assert.Equal("associacao.ja_associado", ex.Codigo);
        Assert.Empty(reescritor.ReceivedCalls());
    }
}

/// <summary>Filtro puro, sem banco: a nota de rejeição fica fora da cópia.</summary>
public class ReescritaCopiaveisTests
{
    [Fact]
    public void Nota_de_rejeicao_KO_nunca_e_copiada()
    {
        var instancias = new (string, string, string?)[]
        {
            ("1.1", "1.1.1", "1.2.840.10008.5.1.4.1.1.7"),       // OT (secondary capture)
            ("1.2", "1.2.1", "1.2.840.10008.5.1.4.1.1.88.22"),   // SR
            ("1.3", "1.3.1", "1.2.840.10008.5.1.4.1.1.88.59"),   // KO — nota de rejeição
        };

        var copiaveis = PacsReescritorEstudoClient.Copiaveis(instancias);

        Assert.Equal(["1.1.1", "1.2.1"], copiaveis.Select(i => i.Sop));
    }
}
