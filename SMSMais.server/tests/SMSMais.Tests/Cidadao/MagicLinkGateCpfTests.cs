using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SMSMais.Core.Cidadao;
using SMSMais.Core.Identidade;
using SMSMais.Core.Laudos.Configuracao;
using SMSMais.Core.Pacientes;
using SMSMais.Core.Telefones;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Cidadao;

/// <summary>
/// Gate de CPF do magic-link. O que está sob teste é a inversão que fecha o furo: possuir o
/// link deixou de SER a credencial nos links que carregam RESULTADO clínico.
///
/// A regra tem duas metades que precisam valer juntas: o desafio <b>não consome</b> o token
/// (senão o titular digitaria o CPF certo e perderia o link) e o CPF errado <b>queima</b> o
/// link na 3ª tentativa. Precisa de Postgres real — a contagem e o consumo usam
/// <c>ExecuteUpdateAsync</c>.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class MagicLinkGateCpfTests(PostgresFixture fixture)
{
    private const string CpfTitular = "04528822733";
    private const string CpfOutro = "01074588703";

    private static CidadaoLoginLinkService CriarService(SmsMaisDbContext db) =>
        new(db,
            Substitute.For<ILaudoConfiguracaoService>(),
            Substitute.For<IPacientesService>(),
            Substitute.For<ICidadaoSessaoService>(),
            Substitute.For<ITelefoneValidacaoService>(),
            new UsuarioAtualAccessorFake(),
            Substitute.For<Microsoft.Extensions.Configuration.IConfiguration>());

    private static async Task<Guid> SemearLinkAsync(SmsMaisDbContext db, bool exigeCpf)
    {
        var link = new CidadaoLoginLink
        {
            Id = Guid.CreateVersion7(),
            PatientId = Guid.CreateVersion7(),
            Cpf = CpfTitular,
            Destino = "/exames?exame=" + Guid.CreateVersion7(),
            ExigeConfirmacaoCpf = exigeCpf,
            ExpiraEm = DateTime.UtcNow.AddDays(3),
            CriadoEm = DateTime.UtcNow,
        };
        db.CidadaoLoginLinks.Add(link);
        await db.SaveChangesAsync();
        return link.Id;
    }

    [Fact]
    public async Task Sem_cpf_devolve_desafio_sem_consumir_o_token_nem_revelar_destino()
    {
        await using var db = fixture.CriarDbContext();
        var token = await SemearLinkAsync(db, exigeCpf: true);

        var r = await CriarService(db).TrocarAsync(token, "ua", "1.2.3.4");

        Assert.NotNull(r);
        Assert.True(r!.RequerConfirmacaoCpf);
        Assert.Null(r.Token);
        Assert.Null(r.Paciente);
        // Nem o destino sai: ele carrega o id do exame e ajudaria a identificar o titular.
        Assert.Equal(string.Empty, r.Destino);
        Assert.Equal(3, r.TentativasRestantes);

        // O token continua VIRGEM — é o que permite o titular voltar e acertar.
        var link = await db.CidadaoLoginLinks.AsNoTracking().SingleAsync(x => x.Id == token);
        Assert.Null(link.UsadoEm);
        Assert.Equal(0, link.TentativasCpf);
        Assert.True(link.ExpiraEm > DateTime.UtcNow);
    }

    [Fact]
    public async Task Cpf_errado_conta_tentativa_e_queima_o_link_na_terceira()
    {
        await using var db = fixture.CriarDbContext();
        var token = await SemearLinkAsync(db, exigeCpf: true);
        var service = CriarService(db);

        var primeira = await service.TrocarAsync(token, null, null, CpfOutro);
        Assert.True(primeira!.RequerConfirmacaoCpf);
        Assert.Equal(2, primeira.TentativasRestantes);

        var segunda = await service.TrocarAsync(token, null, null, CpfOutro);
        Assert.True(segunda!.RequerConfirmacaoCpf);
        Assert.Equal(1, segunda.TentativasRestantes);

        // 3ª: link queimado → null (410) e a recepção precisa reenviar.
        var terceira = await service.TrocarAsync(token, null, null, CpfOutro);
        Assert.Null(terceira);

        var link = await db.CidadaoLoginLinks.AsNoTracking().SingleAsync(x => x.Id == token);
        Assert.Equal(3, link.TentativasCpf);
        Assert.True(link.ExpiraEm <= DateTime.UtcNow);
        // Queimar ≠ usar: ninguém autenticou.
        Assert.Null(link.UsadoEm);

        // E depois de queimado não há segunda chance nem com o CPF certo.
        Assert.Null(await service.TrocarAsync(token, null, null, CpfTitular));
    }

    [Fact]
    public async Task Link_clinico_ja_usado_nao_devolve_nem_o_destino()
    {
        await using var db = fixture.CriarDbContext();
        var token = await SemearLinkAsync(db, exigeCpf: true);
        await db.CidadaoLoginLinks.Where(x => x.Id == token)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.UsadoEm, DateTime.UtcNow));

        // Diferente do link de agendamento, que devolve o destino como facilitador no
        // aparelho original: o link de RESULTADO morre por completo.
        Assert.Null(await CriarService(db).TrocarAsync(token, null, null, CpfTitular));
    }

    [Fact]
    public async Task Link_nao_clinico_segue_em_um_clique_sem_pedir_cpf()
    {
        await using var db = fixture.CriarDbContext();
        var token = await SemearLinkAsync(db, exigeCpf: false);

        try
        {
            await CriarService(db).TrocarAsync(token, null, null);
        }
        catch (NullReferenceException)
        {
            // O fluxo segue para abrir a sessão e resolver o nome no hub FHIR, que aqui são
            // substitutes vazios. Irrelevante para o que este caso mede: o que importa é se o
            // gate ENTROU. Montar um PacienteDto completo (35 campos posicionais) só para o
            // método terminar não acrescentaria cobertura nenhuma.
        }

        // Consumiu de cara = não passou pelo desafio. A confirmação de agendamento depende de
        // ser 1 clique, e é isso que esta asserção protege contra uma regressão futura.
        var link = await db.CidadaoLoginLinks.AsNoTracking().SingleAsync(x => x.Id == token);
        Assert.NotNull(link.UsadoEm);
        Assert.Equal(0, link.TentativasCpf);
    }
}
