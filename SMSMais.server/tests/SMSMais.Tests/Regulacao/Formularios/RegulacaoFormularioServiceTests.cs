using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using NSubstitute;

using SMSMais.Core.Regulacao.Formularios;
using SMSMais.Core.Ser;
using SMSMais.Core.Ser.Dtos;
using SMSMais.Core.Sernit;
using SMSMais.Core.Sernit.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Regulacao;
using SMSMais.Data.Entities.Ser;
using SMSMais.Data.Entities.Sernit;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Regulacao.Formularios;

/// <summary>
/// União SER ∪ SERNIT do formulário Externo (plano 02) — onde a régua medida no spike c vira
/// código.
///
/// <para>Três coisas não podem quebrar: obrigatoriedade é <b>OU</b> (preencher pelo menor
/// exigente faria o destino mais exigente recusar); "Observação"/"Observações" é o <b>mesmo</b>
/// campo (sem isso, o par VIDEOLARINGOSCOPIA ficaria com zero campo em comum); e conflito de
/// tipo <b>não</b> vira escolha automática, porque não há caso real para calibrar a regra.</para>
/// </summary>
[Collection(nameof(PostgresCollection))]
public class RegulacaoFormularioServiceTests(PostgresFixture fixture)
{
    private static string Sufixo() => Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    private static (RegulacaoFormularioService Servico, ISerCatalogoService Ser, ISernitCatalogoService Sernit)
        Montar(SmsMaisDbContext db)
    {
        var ser = Substitute.For<ISerCatalogoService>();
        var sernit = Substitute.For<ISernitCatalogoService>();
        return (new RegulacaoFormularioService(db, ser, sernit), ser, sernit);
    }

    /// <summary>Um canônico com origem nos dois sistemas — o cenário da união.</summary>
    private static async Task<Guid> ProcedimentoComAsDuasOrigensAsync(SmsMaisDbContext db)
    {
        var sufixo = Sufixo();
        var p = new RegulacaoProcedimento
        {
            Id = Guid.CreateVersion7(),
            NomeCanonico = $"CARDIOLOGIA {sufixo}",
            NomeNormalizado = $"CARDIOLOGIA {sufixo}",
            Tipo = TipoProcedimentoRegulacao.Consulta,
            CriadoEm = DateTime.UtcNow,
        };
        db.RegulacaoProcedimentos.Add(p);
        db.RegulacaoProcedimentoOrigens.AddRange(
            new RegulacaoProcedimentoOrigem
            {
                Id = Guid.CreateVersion7(),
                ProcedimentoId = p.Id,
                Sistema = SistemaRegulacao.Ser,
                ChaveExterna = $"1|{sufixo}|NAO_AE",
                RotuloExterno = "CONSULTA EM CARDIOLOGIA",
                Ramo = "NAO_AE",
                CriadoEm = DateTime.UtcNow,
            },
            new RegulacaoProcedimentoOrigem
            {
                Id = Guid.CreateVersion7(),
                ProcedimentoId = p.Id,
                Sistema = SistemaRegulacao.Sernit,
                ChaveExterna = $"1|{sufixo}",
                RotuloExterno = "Cardiologia",
                CriadoEm = DateTime.UtcNow,
            });
        await db.SaveChangesAsync();
        return p.Id;
    }

    private static SerCampoDinamicoDto CampoSer(
        string numero, string campo, string rotulo, string tipo, bool obrigatorio,
        params (string Valor, string Rotulo)[] opcoes) =>
        new(numero, campo, rotulo, tipo, obrigatorio,
            opcoes.Length == 0 ? null : [.. opcoes.Select(o => new SerOpcaoDto(o.Valor, o.Rotulo))]);

    private static SernitCampoDinamicoDto CampoSernit(
        string numero, string campo, string rotulo, string tipo, bool obrigatorio,
        params (string Valor, string Rotulo)[] opcoes) =>
        new(numero, campo, rotulo, tipo, obrigatorio,
            opcoes.Length == 0 ? null : [.. opcoes.Select(o => new SernitOpcaoDto(o.Valor, o.Rotulo))]);

    [Fact]
    public async Task Uniao_junta_campos_com_mesmo_slug_e_marca_as_origens()
    {
        await using var db = fixture.CriarDbContext();
        var procedimentoId = await ProcedimentoComAsDuasOrigensAsync(db);
        var (servico, ser, sernit) = Montar(db);

        ser.ObterCamposAsync(Arg.Any<TipoRecursoSer>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<SerCampoDinamicoDto>>([
                CampoSer("1", "form0:d1", "Queixa Principal", "textarea", true),
            ]);
        sernit.ObterCamposAsync(Arg.Any<TipoRecursoSernit>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<SernitCampoDinamicoDto>>([
                CampoSernit("9", "form0:x9", "Queixa Principal", "textarea", false),
                CampoSernit("10", "form0:x10", "Peso do Paciente (gramas)", "text", true),
            ]);

        var f = await servico.ObterOuGerarAsync(procedimentoId, FluxoRegulacao.Externo, CancellationToken.None);

        var queixa = f.Campos.Single(c => c.Chave == "queixa_principal");
        queixa.Origens.Should().BeEquivalentTo([SistemaRegulacao.Ser, SistemaRegulacao.Sernit]);

        // Campo que só o SERNIT pede entra mesmo assim — a união não é o menor denominador.
        f.Campos.Should().Contain(c => c.Chave == "peso_do_paciente_gramas");
    }

    [Fact]
    public async Task Obrigatorio_se_obrigatorio_em_qualquer_sistema()
    {
        await using var db = fixture.CriarDbContext();
        var procedimentoId = await ProcedimentoComAsDuasOrigensAsync(db);
        var (servico, ser, sernit) = Montar(db);

        // Exatamente a divergência medida no spike c: obrigatório no SER, opcional no SERNIT.
        ser.ObterCamposAsync(Arg.Any<TipoRecursoSer>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<SerCampoDinamicoDto>>([CampoSer("1", "form0:d1", "Observações", "textarea", true)]);
        sernit.ObterCamposAsync(Arg.Any<TipoRecursoSernit>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<SernitCampoDinamicoDto>>([CampoSernit("9", "form0:x9", "Observações", "textarea", false)]);

        var f = await servico.ObterOuGerarAsync(procedimentoId, FluxoRegulacao.Externo, CancellationToken.None);

        f.Campos.Single(c => c.Chave == "observacoes").Obrigatorio.Should().BeTrue(
            "exigir é o lado seguro: o SERNIT aceita o campo preenchido, o SER recusa sem ele");
    }

    [Fact]
    public async Task Observacao_e_observacoes_sao_o_mesmo_campo()
    {
        await using var db = fixture.CriarDbContext();
        var procedimentoId = await ProcedimentoComAsDuasOrigensAsync(db);
        var (servico, ser, sernit) = Montar(db);

        ser.ObterCamposAsync(Arg.Any<TipoRecursoSer>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<SerCampoDinamicoDto>>([CampoSer("1", "form0:d1", "Observações", "textarea", true)]);
        sernit.ObterCamposAsync(Arg.Any<TipoRecursoSernit>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<SernitCampoDinamicoDto>>([CampoSernit("9", "form0:x9", "Observação", "textarea", false)]);

        var f = await servico.ObterOuGerarAsync(procedimentoId, FluxoRegulacao.Externo, CancellationToken.None);

        // Sem a tabela de sinônimos, o par VIDEOLARINGOSCOPIA do spike c fica com zero campo
        // em comum e a união duplica tudo.
        f.Campos.Should().ContainSingle(c => c.Chave == "observacoes");
        f.Campos.Single().Origens.Should().HaveCount(2);
    }

    [Fact]
    public async Task Tipo_diferente_gera_dois_campos_sufixados()
    {
        await using var db = fixture.CriarDbContext();
        var procedimentoId = await ProcedimentoComAsDuasOrigensAsync(db);
        var (servico, ser, sernit) = Montar(db);

        ser.ObterCamposAsync(Arg.Any<TipoRecursoSer>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<SerCampoDinamicoDto>>([CampoSer("1", "form0:d1", "Risco", "select", true, ("1", "Alto"))]);
        sernit.ObterCamposAsync(Arg.Any<TipoRecursoSernit>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<SernitCampoDinamicoDto>>([CampoSernit("9", "form0:x9", "Risco", "textarea", false)]);

        var f = await servico.ObterOuGerarAsync(procedimentoId, FluxoRegulacao.Externo, CancellationToken.None);

        // Escolher um dos tipos seria adivinhação: nos 23 pares medidos não houve NENHUM
        // conflito de tipo, então não há caso real para calibrar a regra.
        f.Campos.Select(c => c.Chave).Should().Contain(["risco_ser", "risco_sernit"]);
    }

    [Fact]
    public async Task Hash_igual_reusa_a_versao()
    {
        await using var db = fixture.CriarDbContext();
        var procedimentoId = await ProcedimentoComAsDuasOrigensAsync(db);
        var (servico, ser, sernit) = Montar(db);

        ser.ObterCamposAsync(Arg.Any<TipoRecursoSer>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<SerCampoDinamicoDto>>([CampoSer("1", "form0:d1", "Queixa Principal", "textarea", true)]);
        sernit.ObterCamposAsync(Arg.Any<TipoRecursoSernit>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<SernitCampoDinamicoDto>>([]);

        var a = await servico.ObterOuGerarAsync(procedimentoId, FluxoRegulacao.Externo, CancellationToken.None);
        var b = await servico.ObterOuGerarAsync(procedimentoId, FluxoRegulacao.Externo, CancellationToken.None);

        b.VersaoId.Should().Be(a.VersaoId, "mesma definição, mesma linha — senão o histórico vira ruído");
    }

    [Fact]
    public async Task Traduzir_aplica_o_nome_nativo_e_a_data()
    {
        await using var db = fixture.CriarDbContext();
        var procedimentoId = await ProcedimentoComAsDuasOrigensAsync(db);
        var (servico, ser, sernit) = Montar(db);

        ser.ObterCamposAsync(Arg.Any<TipoRecursoSer>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<SerCampoDinamicoDto>>([
                CampoSer("1", "form0:dinamico_id_3", "Data da coleta", "date", false),
                CampoSer("2", "form0:dinamico_id_4", "Sintomas", "checkbox", false, ("a", "Dor"), ("b", "Febre")),
            ]);
        sernit.ObterCamposAsync(Arg.Any<TipoRecursoSernit>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<SernitCampoDinamicoDto>>([]);

        var f = await servico.ObterOuGerarAsync(procedimentoId, FluxoRegulacao.Externo, CancellationToken.None);

        var canonico = JsonDocument.Parse(
            """{"data_da_coleta":"2026-03-15","sintomas":["a","b"]}""").RootElement;

        var traduzido = await servico.TraduzirAsync(
            f.VersaoId, SistemaRegulacao.Ser, canonico, CancellationToken.None);

        // O canônico guarda ISO; os dois sistemas falam dd/MM/yyyy no rich:calendar.
        traduzido["form0:dinamico_id_3"].Should().Be("15/03/2026");
        // Múltipla escolha viaja junta, separada por quebra de linha (contrato do SerValorMultiplo).
        traduzido["form0:dinamico_id_4"].Should().Be("a\nb");
    }

    [Fact]
    public async Task Obrigatorios_faltando_lista_as_chaves()
    {
        await using var db = fixture.CriarDbContext();
        var procedimentoId = await ProcedimentoComAsDuasOrigensAsync(db);
        var (servico, ser, sernit) = Montar(db);

        ser.ObterCamposAsync(Arg.Any<TipoRecursoSer>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<SerCampoDinamicoDto>>([
                CampoSer("1", "form0:d1", "Queixa Principal", "textarea", true),
                CampoSer("2", "form0:d2", "Resultado de Exames", "textarea", true),
            ]);
        sernit.ObterCamposAsync(Arg.Any<TipoRecursoSernit>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<SernitCampoDinamicoDto>>([]);

        var f = await servico.ObterOuGerarAsync(procedimentoId, FluxoRegulacao.Externo, CancellationToken.None);

        // Preenchido com espaço em branco não conta como preenchido.
        var canonico = JsonDocument.Parse("""{"queixa_principal":"   "}""").RootElement;

        servico.ObrigatoriosFaltando(f, canonico)
            .Should().BeEquivalentTo(["queixa_principal", "resultado_de_exames"]);
    }

    [Fact]
    public async Task Procedimento_sem_oferta_externa_recusa_o_formulario()
    {
        await using var db = fixture.CriarDbContext();
        var p = new RegulacaoProcedimento
        {
            Id = Guid.CreateVersion7(),
            NomeCanonico = $"SO SISREG {Sufixo()}",
            NomeNormalizado = "SO SISREG",
            Tipo = TipoProcedimentoRegulacao.Consulta,
            CriadoEm = DateTime.UtcNow,
        };
        db.RegulacaoProcedimentos.Add(p);
        await db.SaveChangesAsync();
        var (servico, _, _) = Montar(db);

        var acao = () => servico.ObterOuGerarAsync(p.Id, FluxoRegulacao.Externo, CancellationToken.None);

        await acao.Should().ThrowAsync<SMSMais.Core.Common.Excecoes.ValidacaoException>();
    }

    [Fact]
    public async Task Interno_usa_o_esquema_do_sisreg_e_mapeia_os_nomes_da_tela()
    {
        await using var db = fixture.CriarDbContext();
        var procedimentoId = await ProcedimentoComAsDuasOrigensAsync(db);
        var (servico, _, _) = Montar(db);

        var f = await servico.ObterOuGerarAsync(procedimentoId, FluxoRegulacao.Interno, CancellationToken.None);

        f.Esquema.Should().Be("sisreg.inclusao");
        f.Campos.Select(c => c.Chave).Should().Contain(["cid10", "profissional_solicitante_cpf", "retorno"]);

        var canonico = JsonDocument.Parse("""{"profissional_solicitante_cpf":"12345678909"}""").RootElement;
        var traduzido = await servico.TraduzirAsync(
            f.VersaoId, SistemaRegulacao.Sisreg, canonico, CancellationToken.None);

        traduzido["cpfprofsol"].Should().Be("12345678909");
    }

    [Fact]
    public async Task Nar_usa_o_mesmo_esquema_do_interno()
    {
        await using var db = fixture.CriarDbContext();
        var procedimentoId = await ProcedimentoComAsDuasOrigensAsync(db);
        var (servico, _, _) = Montar(db);

        var f = await servico.ObterOuGerarAsync(procedimentoId, FluxoRegulacao.Nar, CancellationToken.None);

        f.Esquema.Should().Be("sisreg.inclusao");
    }
}
