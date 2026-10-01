using Microsoft.EntityFrameworkCore;

using NSubstitute;

using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Core.Integracoes.SerWeb.Profissionais;
using SMSMais.Core.Medicos;
using SMSMais.Core.Ser.Background;
using SMSMais.Core.Ser.Profissionais;
using SMSMais.Data;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Ser;

/// <summary>
/// Espelho dos profissionais do SER (ADR-0065): a importação atualiza sem apagar, junta o
/// cadastro duplicado do SER numa linha só e não confunde leitura parcial com "saíram do SER".
/// Dados inventados.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class SerProfissionalServiceTests(PostgresFixture fixture)
{
    private static (SerProfissionalService Servico, ISerProfissionalLeitor Leitor) Montar(SmsMaisDbContext db)
    {
        var leitor = Substitute.For<ISerProfissionalLeitor>();
        return (new SerProfissionalService(
            db, leitor, Substitute.For<IMedicosService>(), Substitute.For<IUsuarioAtualAccessor>(),
            new SerProfissionalImportacaoFila()), leitor);
    }

    private static async Task LimparAsync(SmsMaisDbContext db) =>
        await db.SerProfissionais.ExecuteDeleteAsync();

    [Fact]
    public async Task Importa_junta_duplicados_e_marca_quem_saiu_sem_apagar()
    {
        await using var db = fixture.CriarDbContext();
        await LimparAsync(db);
        var (servico, leitor) = Montar(db);

        leitor.LerTodosAsync(Arg.Any<CancellationToken>()).Returns(
            [
                new SerProfissionalLinha("12345678909", "5212345", "CRM", "MARIA EXEMPLO", true),
                // Cadastro duplicado no SER, mesmo CPF: vira uma linha só, com 2 ocorrências.
                new SerProfissionalLinha("12345678909", null, "CNS", "MARIA EXEMPLO", false),
                new SerProfissionalLinha(null, null, "CNS", "JOSÉ EXEMPLO", true),
            ]);

        var r1 = await servico.ImportarAsync(CancellationToken.None);
        r1.Novos.Should().Be(2);

        var maria = await db.SerProfissionais.AsNoTracking().SingleAsync(p => p.Cpf == "12345678909");
        maria.Ocorrencias.Should().Be(2);
        maria.Ativo.Should().BeTrue("ativo em qualquer das linhas do SER");

        // Segunda leitura: o José sumiu da pesquisa do SER.
        leitor.LerTodosAsync(Arg.Any<CancellationToken>()).Returns(
            [new SerProfissionalLinha("12345678909", "5212345", "CRM", "MARIA EXEMPLO", true)]);

        var r2 = await servico.ImportarAsync(CancellationToken.None);
        r2.Sumiram.Should().Be(1);

        var jose = await db.SerProfissionais.AsNoTracking().SingleAsync(p => p.NomeNormalizado == "JOSE EXEMPLO");
        jose.PresenteNoSer.Should().BeFalse("sumiu da pesquisa — mas a linha (e a ligação) fica");
    }

    [Fact]
    public async Task Leitura_parcial_nao_mexe_em_nada()
    {
        await using var db = fixture.CriarDbContext();
        await LimparAsync(db);
        var (servico, leitor) = Montar(db);

        leitor.LerTodosAsync(Arg.Any<CancellationToken>()).Returns(
            [.. Enumerable.Range(1, 10).Select(i => new SerProfissionalLinha(null, $"{i}", "CRM", $"MEDICO {i}", true))]);
        await servico.ImportarAsync(CancellationToken.None);

        // Voltou com 2 de 10: sessão caiu ou a tela mudou. Não é "8 saíram do SER".
        leitor.LerTodosAsync(Arg.Any<CancellationToken>()).Returns(
            [new SerProfissionalLinha(null, "1", "CRM", "MEDICO 1", true), new SerProfissionalLinha(null, "2", "CRM", "MEDICO 2", true)]);

        var acao = () => servico.ImportarAsync(CancellationToken.None);
        await acao.Should().ThrowAsync<ValidacaoException>();

        (await db.SerProfissionais.CountAsync(p => p.PresenteNoSer)).Should().Be(10);
    }

    [Fact]
    public void Normaliza_sem_acento_maiusculo_e_espacos_colapsados()
    {
        SerProfissionalService.Normalizar("  José   da  Conceição ").Should().Be("JOSE DA CONCEICAO");
    }
}
