using Microsoft.EntityFrameworkCore;

using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Regulacao.Medicos;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Regulacao.Medicos;

/// <summary>
/// A junção dos nomes das fichas do SISREG. O que prendem: só se junta o que é seguro (grafia,
/// CPF com o mesmo primeiro nome, variante de UM nome mais completo); o ambíguo e o de CPF
/// diferente ficam separados, porque juntar duas pessoas é pior que deixar um repetido.
/// </summary>
public class ConsolidacaoMedicosTests
{
    private static OcorrenciaMedico O(string nome, int n = 1, string? cpf = null) => new(nome, cpf, null, null, null, n);

    [Fact]
    public void Junta_acento_pontuacao_abreviacao_e_erro_de_digitacao_no_nome_mais_completo()
    {
        var r = ConsolidacaoMedicos.Consolidar(
        [
            O("OTAVIO FRANCISCO SANTOS", 40),
            O("Otavio F. Santos", 10),
            O("OTAVIO FRANCICO SANTOS", 2),
            O("OTAVIO  FRANCISCO DOS SANTOS", 3),
        ]);

        r.Should().ContainSingle();
        r[0].Nome.Should().Be("OTAVIO FRANCISCO SANTOS");
        r[0].Ocorrencias.Should().Be(55);
        r[0].Chaves.Should().BeEquivalentTo(["OTAVIO FRANCISCO SANTOS", "OTAVIO F SANTOS", "OTAVIO FRANCICO SANTOS"]);
    }

    [Fact]
    public void Variante_de_dois_nomes_diferentes_fica_separada()
    {
        var r = ConsolidacaoMedicos.Consolidar(
        [
            O("ANA M SOUZA", 5),
            O("ANA MARIA SOUZA", 5),
            O("ANA MARTA SOUZA", 5),
        ]);

        r.Should().HaveCount(3, "ANA M SOUZA pode ser qualquer uma das duas — a pessoa decide");
    }

    [Fact]
    public void Nome_de_duas_palavras_nao_se_junta_sozinho()
    {
        var r = ConsolidacaoMedicos.Consolidar([O("MARIA SILVA", 5), O("MARIA SILVA SOUZA", 5)]);
        r.Should().HaveCount(2);
    }

    [Fact]
    public void Mesmo_CPF_junta_grafias_e_CPF_diferente_nunca_junta()
    {
        var cpf1 = Cpf(123456789);
        var cpf2 = Cpf(987654321);
        var r = ConsolidacaoMedicos.Consolidar(
        [
            O("PAULO ROBERTO LIMA", 10, cpf1),
            O("PAULO R. LIMA FILHO", 3, cpf1),
            O("JOAO PEDRO COSTA", 1, cpf1), // CPF do Paulo digitado por engano: não arrasta o João
            O("CARLOS ALBERTO NUNES", 4, cpf2),
            O("CARLOS A NUNES", 4, Cpf(111444777)),
        ]);

        r.Single(m => m.Chave == "PAULO R LIMA FILHO").Chaves.Should().Contain("PAULO ROBERTO LIMA");
        r.Single(m => m.Chave == "PAULO R LIMA FILHO").Cpf.Should().Be(cpf1);
        r.Should().Contain(m => m.Chave == "JOAO PEDRO COSTA");
        r.Count(m => m.Chave.StartsWith("CARLOS")).Should().Be(2, "dois CPFs diferentes são duas pessoas");
    }

    [Fact]
    public void Descarta_CPF_invalido_e_nome_que_nao_e_nome()
    {
        var r = ConsolidacaoMedicos.Consolidar([O("---", 9), O("DR", 3), O("RITA CASSIA MOURA", 1, "11111111111")]);
        r.Should().ContainSingle().Which.Cpf.Should().BeNull();
    }

    [Theory]
    [InlineData("529.982.247-25", "52998224725")]
    [InlineData("52998224724", null)]
    [InlineData("00000000000", null)]
    public void CPF_valido_confere_os_digitos(string cpf, string? esperado) =>
        ConsolidacaoMedicos.CpfValido(cpf).Should().Be(esperado);

    /// <summary>Um CPF válido a partir dos 9 primeiros dígitos.</summary>
    internal static string Cpf(int base9)
    {
        var d = base9.ToString("D9");
        for (var t = 9; t < 11; t++)
        {
            var soma = 0;
            for (var i = 0; i < t; i++) soma += (d[i] - '0') * (t + 1 - i);
            d += (soma * 10 % 11 % 10).ToString();
        }
        return d;
    }
}

/// <summary>
/// O cadastro de médicos do SISREG que é só nosso. O que prendem: incluir não duplica (mesmo CPF,
/// mesmo nome ou grafia já conhecida devolvem o que existe); a busca acha por qualquer grafia; e
/// ler as fichas de novo não cria repetido nem desfaz o que a pessoa juntou.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class RegulacaoMedicoLocalTests(PostgresFixture fixture)
{
    private static string Sufixo() =>
        new([.. Guid.NewGuid().ToString("N").Where(char.IsLetter).Take(8).Select(char.ToUpperInvariant)]);

    private static int Base9() => Random.Shared.Next(100_000_000, 999_999_999);

    private static RegulacaoMedicoLocalService Servico(SmsMaisDbContext db) =>
        new(db, new UsuarioAtualAccessorFake(Guid.NewGuid()));

    [Fact]
    public async Task Incluir_nao_duplica_por_nome_nem_por_CPF()
    {
        await using var db = fixture.CriarDbContext();
        var s = Sufixo();
        var cpf = ConsolidacaoMedicosTests.Cpf(Base9());

        var a = await Servico(db).CriarAsync(new(SistemaRegulacao.Sisreg, $"renata  {s} braga", null, "crm", "52-12345", "rj"), default);
        var b = await Servico(db).CriarAsync(new(SistemaRegulacao.Sisreg, $"RENATA {s} BRAGA", cpf, null, null, null), default);
        var c = await Servico(db).CriarAsync(new(SistemaRegulacao.Sisreg, $"OUTRO NOME {s}", null, null, null, null), default);
        var d = await Servico(db).CriarAsync(new(SistemaRegulacao.Sisreg, $"TERCEIRO {s} NOME", cpf, null, null, null), default);

        a.Nome.Should().Be($"RENATA {s} BRAGA");
        a.Conselho.Should().Be("CRM");
        a.UfConselho.Should().Be("RJ");
        b.Id.Should().Be(a.Id, "mesmo nome devolve o cadastro que já existe");
        d.Id.Should().Be(a.Id, "mesmo CPF devolve o cadastro que já tem o CPF");
        c.Id.Should().NotBe(a.Id);
    }

    [Fact]
    public async Task Incluir_valida_nome_completo_CPF_e_so_aceita_SISREG()
    {
        await using var db = fixture.CriarDbContext();

        var semSobrenome = () => Servico(db).CriarAsync(new(SistemaRegulacao.Sisreg, "RENATA", null, null, null, null), default);
        var cpfErrado = () => Servico(db).CriarAsync(new(SistemaRegulacao.Sisreg, $"RENATA {Sufixo()}", "12345678900", null, null, null), default);
        var ser = () => Servico(db).CriarAsync(new(SistemaRegulacao.Ser, $"RENATA {Sufixo()}", null, null, null, null), default);

        await semSobrenome.Should().ThrowAsync<ValidacaoException>();
        await cpfErrado.Should().ThrowAsync<ValidacaoException>();
        await ser.Should().ThrowAsync<ValidacaoException>();
    }

    [Fact]
    public async Task Busca_acha_por_palavras_em_qualquer_grafia_e_pelo_CPF()
    {
        await using var db = fixture.CriarDbContext();
        var s = Sufixo();
        var cpf = ConsolidacaoMedicosTests.Cpf(Base9());
        var principal = await Servico(db).CriarAsync(new(SistemaRegulacao.Sisreg, $"FABIO {s} MONTEIRO", cpf, null, null, null), default);
        var outro = await Servico(db).CriarAsync(new(SistemaRegulacao.Sisreg, $"FABIO {s} MOTA", null, null, null, null), default);
        await Servico(db).JuntarAsync(principal.Id, outro.Id, default);

        (await Servico(db).BuscarAsync(SistemaRegulacao.Sisreg, $"monteiro {s.ToLowerInvariant()[..4]}", 20, default))
            .Should().ContainSingle(m => m.Id == principal.Id);
        (await Servico(db).BuscarAsync(SistemaRegulacao.Sisreg, $"{s} MOTA", 20, default))
            .Should().ContainSingle(m => m.Id == principal.Id, "a grafia juntada continua achando o médico");
        (await Servico(db).BuscarAsync(SistemaRegulacao.Sisreg, cpf[..7], 20, default))
            .Should().Contain(m => m.Id == principal.Id);
    }

    [Fact]
    public async Task Juntar_recusa_CPFs_diferentes()
    {
        await using var db = fixture.CriarDbContext();
        var s = Sufixo();
        var a = await Servico(db).CriarAsync(new(SistemaRegulacao.Sisreg, $"GISELE {s} PRADO", ConsolidacaoMedicosTests.Cpf(Base9()), null, null, null), default);
        var b = await Servico(db).CriarAsync(new(SistemaRegulacao.Sisreg, $"GISELE {s} P", ConsolidacaoMedicosTests.Cpf(Base9()), null, null, null), default);

        var juntar = () => Servico(db).JuntarAsync(a.Id, b.Id, default);
        await juntar.Should().ThrowAsync<ConflitoException>();
    }

    [Fact]
    public async Task Ler_as_fichas_cria_sem_repetir_e_respeita_o_que_foi_juntado()
    {
        await using var db = fixture.CriarDbContext();
        var s = Sufixo();
        var cpf = ConsolidacaoMedicosTests.Cpf(Base9());
        var unidade = new Unidade { Id = Guid.NewGuid(), Nome = $"UNIDADE TESTE {s}", CriadoEm = DateTime.UtcNow };
        db.Unidades.Add(unidade);
        db.Solicitacoes.AddRange(
            Ficha(unidade.Id, $"HUGO {s} CARVALHO NETO", cpf),
            Ficha(unidade.Id, $"HUGO {s} CARVALHO NETO", cpf),
            Ficha(unidade.Id, $"Hugo {s} C. Neto", null),
            Ficha(unidade.Id, $"LIA {s} REIS", null));
        await db.SaveChangesAsync();

        var primeira = await Servico(db).AtualizarDasFichasAsync(SistemaRegulacao.Sisreg, default);
        var hugo = (await Servico(db).BuscarAsync(SistemaRegulacao.Sisreg, $"hugo {s}", 20, default)).Should().ContainSingle().Subject;
        var lia = (await Servico(db).BuscarAsync(SistemaRegulacao.Sisreg, $"lia {s}", 20, default)).Should().ContainSingle().Subject;

        hugo.Nome.Should().Be($"HUGO {s} CARVALHO NETO");
        hugo.Cpf.Should().Be(cpf);
        hugo.Ocorrencias.Should().Be(3);
        hugo.Grafias.Should().Contain($"HUGO {s} C NETO");
        primeira.Criados.Should().BeGreaterThanOrEqualTo(2);

        // A pessoa junta a LIA no HUGO (só para o teste) e lê as fichas de novo: nada volta.
        await Servico(db).JuntarAsync(hugo.Id, lia.Id, default);
        await Servico(db).AtualizarDasFichasAsync(SistemaRegulacao.Sisreg, default);
        await using var db2 = fixture.CriarDbContext();
        var depois = await db2.RegulacaoMedicosLocais.Where(m => m.NomeNormalizado.Contains(s)).ToListAsync();
        depois.Should().ContainSingle().Which.Ocorrencias.Should().Be(4);
    }

    private static Solicitacao Ficha(Guid unidadeId, string solicitante, string? cpf) => new()
    {
        Id = Guid.NewGuid(),
        PacienteId = Guid.NewGuid(),
        Categoria = CategoriaSolicitacao.Consulta,
        UnidadeExecutanteId = unidadeId,
        Status = StatusSolicitacao.Solicitada,
        Prioridade = PrioridadeSolicitacao.Eletiva,
        SolicitanteNome = solicitante,
        SolicitanteCpf = cpf,
        SolicitanteNumConselho = "---",
        SolicitanteUfConselho = "RJ",
        CriadoEm = DateTime.UtcNow,
    };
}
