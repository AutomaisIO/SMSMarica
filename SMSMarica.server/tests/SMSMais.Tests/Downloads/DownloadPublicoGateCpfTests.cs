using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SMSMarica.Core.Downloads;
using SMSMarica.Core.Exames;
using SMSMarica.Core.Identidade;
using SMSMarica.Core.Laudos.Configuracao;
using SMSMarica.Core.SolicitacoesExame;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Downloads;

/// <summary>
/// Gate de CPF do link público de download (<c>/documento/{token}</c>). Este era o caminho de
/// menor resistência do sistema: <c>[AllowAnonymous]</c>, sem sessão e sem identificação
/// nenhuma, entregando o PDF completo do exame a quem tivesse a URL.
///
/// Os casos aqui fixam as garantias que não podem regredir: <b>sem liberação não sai arquivo</b>
/// e <b>o status não conta nada sobre o exame</b> antes do CPF. A comparação do CPF em si
/// depende de um <c>SolicitacaoExameDto</c> de ~60 campos posicionais e é exercitada de ponta a
/// ponta na verificação manual; montá-lo aqui não acrescentaria cobertura ao que interessa.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class DownloadPublicoGateCpfTests(PostgresFixture fixture)
{
    private static DownloadTokenService CriarService(SmsMaisDbContext db) =>
        new(db,
            Substitute.For<ILaudoConfiguracaoService>(),
            Substitute.For<IExameCompletoPdfService>(),
            Substitute.For<ISolicitacoesExameService>(),
            new UsuarioAtualAccessorFake(),
            Substitute.For<Microsoft.Extensions.Configuration.IConfiguration>());

    private static async Task<DownloadToken> SemearAsync(
        SmsMaisDbContext db, Guid? liberacao = null, DateTime? liberadoEm = null)
    {
        var t = new DownloadToken
        {
            Id = Guid.CreateVersion7(),
            Tipo = "exame-completo",
            ReferenciaId = Guid.CreateVersion7(),
            ExpiraEm = DateTime.UtcNow.AddDays(7),
            Liberacao = liberacao,
            LiberadoEm = liberadoEm,
            CriadoEm = DateTime.UtcNow,
        };
        db.DownloadTokens.Add(t);
        await db.SaveChangesAsync();
        return t;
    }

    [Fact]
    public async Task Sem_liberacao_nao_baixa_nada()
    {
        await using var db = fixture.CriarDbContext();
        var t = await SemearAsync(db);

        // Exatamente o ataque que o gate fecha: alguém com a URL, sem passar pelo CPF.
        Assert.Null(await CriarService(db).ConsumirAsync(t.Id, null, "1.2.3.4"));
        Assert.Null(await CriarService(db).ConsumirAsync(t.Id, Guid.CreateVersion7(), "1.2.3.4"));

        var atual = await db.DownloadTokens.AsNoTracking().SingleAsync(x => x.Id == t.Id);
        Assert.Null(atual.UsadoEm); // e o token não foi gasto à toa
    }

    [Fact]
    public async Task Liberacao_de_outro_token_nao_serve()
    {
        await using var db = fixture.CriarDbContext();
        var meu = await SemearAsync(db, liberacao: Guid.CreateVersion7(), liberadoEm: DateTime.UtcNow);
        var alheio = await SemearAsync(db, liberacao: Guid.CreateVersion7(), liberadoEm: DateTime.UtcNow);

        Assert.Null(await CriarService(db).ConsumirAsync(meu.Id, alheio.Liberacao, null));
    }

    [Fact]
    public async Task Liberacao_vencida_nao_serve()
    {
        await using var db = fixture.CriarDbContext();
        var lib = Guid.CreateVersion7();
        // Janela é de 15 min: confirmar o CPF hoje não vale para baixar amanhã.
        var t = await SemearAsync(db, liberacao: lib, liberadoEm: DateTime.UtcNow.AddMinutes(-16));

        Assert.Null(await CriarService(db).ConsumirAsync(t.Id, lib, null));
    }

    [Fact]
    public async Task Status_pede_cpf_e_nao_revela_o_exame()
    {
        await using var db = fixture.CriarDbContext();
        var t = await SemearAsync(db);

        var s = await CriarService(db).ObterStatusAsync(t.Id);

        Assert.Equal(DownloadTokenEstado.Valido, s.Estado);
        Assert.True(s.RequerCpf);
        // O nome do procedimento é dado de saúde: junto com o número que recebeu a mensagem,
        // identificaria o titular para quem pegou o link por engano.
        Assert.Null(s.Descricao);
        Assert.Equal(3, s.TentativasRestantes);
    }
}
