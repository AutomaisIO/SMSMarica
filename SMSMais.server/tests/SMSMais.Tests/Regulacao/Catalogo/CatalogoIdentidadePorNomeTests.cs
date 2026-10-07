using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using SMSMais.Core.Common.Cid;
using SMSMais.Core.Regulacao.Catalogo;
using SMSMais.Core.Ser;
using SMSMais.Core.Ser.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Regulacao;
using SMSMais.Data.Entities.Ser;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Regulacao.Catalogo;

/// <summary>
/// A identidade de um recurso do SER/SERNIT/ESUS SG é o NOME, não o número do combo (07/10/2026).
///
/// <para>O caso que motivou: no ensaio da PR-20, o nosso espelho dizia 1130 = "CONSULTA EM
/// ODONTOLOGIA - CIRURGIA BUCO-MAXILO FACIAL" e o SER, no mesmo dia, tinha 1130 = Odontopediatria e
/// o Buco-Maxilo em 1134. As 422 origens do SER apontavam para números que já eram de outro
/// recurso. Os combos abaixo são os do SER naquele dia.</para>
/// </summary>
public class IdentidadePorNomeTests
{
    private static readonly SerOpcaoDto[] OdontoEm07Out =
    [
        new("1130", "CONSULTA EM ODONTOLOGIA - ODONTOPEDIATRIA"),
        new("1131", "CONSULTA EM ODONTOLOGIA - PACIENTE COM NECESSIDADE ESPECIAL"),
        new("1132", "CONSULTA EM ODONTOLOGIA - ESTOMATOLOGIA"),
        new("1133", "CONSULTA EM ODONTOLOGIA - ENDODONTIA"),
        new("1134", "CONSULTA EM ODONTOLOGIA - CIRURGIA BUCO-MAXILO FACIAL"),
        new("1135", "CONSULTA EM ODONTOLOGIA - CIRURGIA ORAL MENOR"),
    ];

    [Fact]
    public void Acha_pelo_nome_o_numero_de_hoje_e_nao_o_guardado()
    {
        var achados = IdentidadePorNome.Achar(
            OdontoEm07Out, o => o.Rotulo, "CONSULTA EM ODONTOLOGIA - CIRURGIA BUCO-MAXILO FACIAL");

        achados.Should().ContainSingle().Which.Valor.Should().Be("1134",
            "o 1130 guardado em 30/09 é a Odontopediatria hoje");
    }

    [Fact]
    public void Acento_espaco_e_caixa_nao_separam_o_mesmo_nome()
    {
        // O dado dos sistemas é irregular: espaço duplo e acento ora sim, ora não.
        var achados = IdentidadePorNome.Achar(
            OdontoEm07Out, o => o.Rotulo, "Consulta em Odontologia  -  Cirurgia Bucó-Maxilo Facial");

        achados.Should().ContainSingle().Which.Valor.Should().Be("1134");
    }

    [Fact]
    public void Nome_que_saiu_do_combo_nao_vira_um_parecido()
    {
        IdentidadePorNome.Achar(OdontoEm07Out, o => o.Rotulo, "CONSULTA EM ODONTOLOGIA - PERIODONTIA")
            .Should().BeEmpty("nome que não está no combo é recusa, nunca o mais parecido");
        IdentidadePorNome.Achar(OdontoEm07Out, o => o.Rotulo, "   ")
            .Should().BeEmpty();
    }

    private sealed record Linha(string Valor, string Rotulo, DateTime Em);

    [Fact]
    public void Consolidar_fica_com_a_linha_da_listagem_mais_recente_de_cada_nome()
    {
        var ontem = DateTime.UtcNow.AddDays(-1);
        var hoje = DateTime.UtcNow;
        var velha = new Linha("1130", "CONSULTA EM ODONTOLOGIA - CIRURGIA BUCO-MAXILO FACIAL", ontem.AddDays(-7));
        var nova = new Linha("1134", "CONSULTA EM ODONTOLOGIA - CIRURGIA BUCO-MAXILO FACIAL", hoje);
        var outra = new Linha("1131", "CONSULTA EM ODONTOLOGIA - ODONTOPEDIATRIA", ontem);

        var (porNome, fundidas) = IdentidadePorNome.Consolidar([velha, nova, outra], l => l.Rotulo, l => l.Em);

        porNome.Should().HaveCount(2);
        porNome[IdentidadePorNome.Chave(nova.Rotulo)].Should().BeSameAs(nova);
        fundidas.Should().ContainSingle().Which.Should().Be((velha, nova));
    }
}

/// <summary>
/// A cópia do catálogo do SER contra um SER falso que RENUMERA de um dia para o outro — o que a SES
/// fez em 22/09, 30/09 e 07/10/2026.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class SerCatalogoSyncPorNomeTests(PostgresFixture fixture)
{
    private static string Sufixo() => Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    private static async IAsyncEnumerable<SerAssinaturaCidDto> Nenhuma()
    {
        await Task.CompletedTask;
        yield break;
    }

    private static (SerCatalogoSyncService Sync, ISerNovaSolicitacaoService Ser) Montar(SmsMaisDbContext db)
    {
        var ser = Substitute.For<ISerNovaSolicitacaoService>();
        ser.ObterFormularioAsync(Arg.Any<CancellationToken>())
            .Returns(new SerFormularioNovaDto([], [], [], [], []));
        ser.ListarRecursosAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<SerOpcaoDto>>([]));
        ser.ObterCamposDinamicosAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<SerCampoDinamicoDto>>(
                [new SerCampoDinamicoDto("1", "form0:dinamico_id_1", "Queixa Principal", "textarea", true, null)]));
        ser.MedirAssinaturasCidAsync(Arg.Any<CancellationToken>()).Returns(_ => Nenhuma());

        var sync = new SerCatalogoSyncService(
            db, ser, Substitute.For<IRegulacaoCatalogoService>(), Substitute.For<ICidCatalogoSyncService>(),
            NullLogger<SerCatalogoSyncService>.Instance);
        return (sync, ser);
    }

    private static void ListarHoje(ISerNovaSolicitacaoService ser, params SerOpcaoDto[] recursos) =>
        ser.ListarRecursosAsync("CONSULTA", false, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<SerOpcaoDto>>(recursos));

    [Fact]
    public async Task Renumeracao_mantem_a_linha_do_recurso_e_so_troca_o_numero()
    {
        await using var db = fixture.CriarDbContext();
        var s = Sufixo();
        var buco = $"CONSULTA EM ODONTOLOGIA - CIRURGIA BUCO-MAXILO FACIAL {s}";
        var odontoped = $"CONSULTA EM ODONTOLOGIA - ODONTOPEDIATRIA {s}";
        var (sync, ser) = Montar(db);

        // Dia 1: o Buco-Maxilo está no 1130.
        ListarHoje(ser, new SerOpcaoDto($"9{s[..3]}0", buco));
        await sync.SincronizarAsync(false, CancellationToken.None);
        var linha = await db.SerCatalogoRecursos.AsNoTracking().SingleAsync(r => r.Rotulo == buco);
        linha.CamposLidos.Should().BeTrue();

        // Dia 2: a SES acrescentou a Odontopediatria ANTES dele — ela ganha o número dele, e ele
        // desliza.
        db.ChangeTracker.Clear();
        ser.ClearReceivedCalls();
        ListarHoje(ser, new SerOpcaoDto($"9{s[..3]}0", odontoped), new SerOpcaoDto($"9{s[..3]}4", buco));
        await sync.SincronizarAsync(false, CancellationToken.None);

        var depois = await db.SerCatalogoRecursos.AsNoTracking().SingleAsync(r => r.Rotulo == buco);
        depois.Id.Should().Be(linha.Id, "é o mesmo recurso — a linha não pode ir para a Odontopediatria");
        depois.Valor.Should().Be($"9{s[..3]}4", "o número é o de hoje");
        depois.CamposLidos.Should().BeTrue("campos são do recurso, não da posição — não se relê à toa");

        var nova = await db.SerCatalogoRecursos.AsNoTracking().SingleAsync(r => r.Rotulo == odontoped);
        nova.Id.Should().NotBe(linha.Id, "a Odontopediatria é outro recurso, com linha própria");
        nova.Valor.Should().Be($"9{s[..3]}0");

        // No dia 2 só a Odontopediatria teve os campos lidos.
        await ser.Received(1).ObterCamposDinamicosAsync(
            "CONSULTA", Arg.Any<string>(), false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Linhas_do_mesmo_nome_em_numeros_antigos_sao_fundidas_e_a_origem_segue()
    {
        await using var db = fixture.CriarDbContext();
        var s = Sufixo();
        var rotulo = $"CONSULTA EM CARDIOLOGIA - TESTE FUSAO {s}";

        // O regime antigo deixava para trás uma linha por número em que o recurso já esteve.
        var velha = new SerCatalogoRecurso
        {
            Id = Guid.NewGuid(), Tipo = TipoRecursoSer.Consulta, Valor = $"8{s[..3]}1", Rotulo = rotulo,
            SincronizadoEm = DateTime.UtcNow.AddDays(-20), CamposLidos = true,
        };
        var recente = new SerCatalogoRecurso
        {
            Id = Guid.NewGuid(), Tipo = TipoRecursoSer.Consulta, Valor = $"8{s[..3]}2", Rotulo = rotulo,
            SincronizadoEm = DateTime.UtcNow.AddDays(-7), CamposLidos = true,
        };
        db.SerCatalogoRecursos.AddRange(velha, recente);
        var procedimento = new RegulacaoProcedimento
        {
            Id = Guid.CreateVersion7(), NomeCanonico = rotulo, NomeNormalizado = rotulo,
            Tipo = TipoProcedimentoRegulacao.Consulta, Ativo = true, CriadoEm = DateTime.UtcNow,
        };
        db.RegulacaoProcedimentos.Add(procedimento);
        var origem = new RegulacaoProcedimentoOrigem
        {
            Id = Guid.CreateVersion7(), ProcedimentoId = procedimento.Id, Sistema = SistemaRegulacao.Ser,
            ChaveExterna = $"1|{velha.Valor}|NAO_AE", RotuloExterno = rotulo, Ramo = "NAO_AE",
            SerCatalogoRecursoId = velha.Id, Ativo = true, CriadoEm = DateTime.UtcNow,
        };
        db.RegulacaoProcedimentoOrigens.Add(origem);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var (sync, ser) = Montar(db);
        ListarHoje(ser, new SerOpcaoDto($"8{s[..3]}3", rotulo));
        await sync.SincronizarAsync(false, CancellationToken.None);

        var linhas = await db.SerCatalogoRecursos.AsNoTracking().Where(r => r.Rotulo == rotulo).ToListAsync();
        linhas.Should().ContainSingle("um nome, uma linha");
        linhas[0].Id.Should().Be(recente.Id, "fica a da listagem mais recente");
        linhas[0].Valor.Should().Be($"8{s[..3]}3");
        linhas[0].RotuloChave.Should().Be(IdentidadePorNome.Chave(rotulo));

        var depois = await db.RegulacaoProcedimentoOrigens.AsNoTracking().SingleAsync(o => o.Id == origem.Id);
        depois.SerCatalogoRecursoId.Should().Be(recente.Id, "quem apontava para a linha fundida segue a que ficou");
    }
}
