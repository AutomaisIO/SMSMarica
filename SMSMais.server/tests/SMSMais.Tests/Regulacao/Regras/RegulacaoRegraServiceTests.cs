using System.Text;

using Microsoft.EntityFrameworkCore;

using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Regulacao.Regras;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Regulacao;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Regulacao.Regras;

/// <summary>
/// Cadastro e importação das regras (plano 03, tarefa 4.5).
///
/// <para>O que prendem: a <b>seção do manual</b> decide qual resposta bloqueia (deduzir do texto
/// invertia 295 das 1.169 regras — medido no spike e); a importação cria tudo <b>inativo</b>,
/// porque ativar 974 perguntas de uma vez transformaria o wizard num interrogatório; e corrigir
/// uma regra <b>versiona</b> em vez de sobrescrever, senão atualizar o manual reescreveria
/// retroativamente por que um pedido foi barrado.</para>
/// </summary>
[Collection(nameof(PostgresCollection))]
public class RegulacaoRegraServiceTests(PostgresFixture fixture)
{
    private static string Sufixo() => Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    private static RegulacaoRegraService Servico(SmsMaisDbContext db) =>
        new(db, new UsuarioAtualAccessorFake(Guid.NewGuid(), null));

    private static async Task<(Guid ProcedimentoId, string Rotulo)> CatalogoAsync(SmsMaisDbContext db)
    {
        var sufixo = Sufixo();
        var rotulo = $"CONSULTA EM CARDIOLOGIA {sufixo}";

        var procedimento = new RegulacaoProcedimento
        {
            Id = Guid.CreateVersion7(),
            NomeCanonico = rotulo,
            NomeNormalizado = rotulo,
            Tipo = TipoProcedimentoRegulacao.Consulta,
            CriadoEm = DateTime.UtcNow,
        };
        db.RegulacaoProcedimentos.Add(procedimento);
        await db.SaveChangesAsync();

        db.RegulacaoProcedimentoOrigens.Add(new RegulacaoProcedimentoOrigem
        {
            Id = Guid.CreateVersion7(),
            ProcedimentoId = procedimento.Id,
            Sistema = SistemaRegulacao.Ser,
            ChaveExterna = $"1|{sufixo}|AE",
            RotuloExterno = rotulo,
            Ramo = "AE",
            CriadoEm = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        return (procedimento.Id, rotulo);
    }

    private static Stream Csv(params string[] linhas)
    {
        var cabecalho =
            "recurso;sistema;manual;ramo_ser;recurso_catalogo;pareamento;secao;tipo;"
            + "texto_original;idade_min;idade_max;sexo;pergunta;documento;fonte";
        var texto = string.Join("\n", [cabecalho, .. linhas]);
        return new MemoryStream(Encoding.UTF8.GetBytes(texto));
    }

    [Fact]
    public async Task A_secao_do_manual_decide_qual_resposta_bloqueia()
    {
        await using var db = fixture.CriarDbContext();
        var (procedimentoId, rotulo) = await CatalogoAsync(db);

        // Inclusão: quem NÃO tem o critério fica de fora. Exclusão: quem TEM fica de fora.
        // Deduzir isso do texto invertia 295 das 1.169 regras do manual.
        var csv = Csv(
            $"X;SER;CRECE;AmbulatorioEstadual;{rotulo};celula;inclusao;NaoDedutivel;Tem laudo;;;;Tem laudo?;;CRECE p.1",
            $"X;SER;CRECE;AmbulatorioEstadual;{rotulo};celula;exclusao;NaoDedutivel;Gestante;;;;Está grávida?;;CRECE p.2");

        var r = await Servico(db).ImportarCsvAsync(csv, CancellationToken.None);

        r.Criadas.Should().Be(2);
        r.SemRecurso.Should().Be(0);

        var regras = await db.RegulacaoRegras.AsNoTracking()
            .Where(x => x.ProcedimentoId == procedimentoId).ToListAsync();

        regras.Single(x => x.Pergunta == "Tem laudo?")
            .RespostaBloqueia.Should().Be(RespostaRegraRegulacao.Nao);
        regras.Single(x => x.Pergunta == "Está grávida?")
            .RespostaBloqueia.Should().Be(RespostaRegraRegulacao.Sim);
    }

    [Fact]
    public async Task A_importacao_cria_tudo_inativo()
    {
        await using var db = fixture.CriarDbContext();
        var (procedimentoId, rotulo) = await CatalogoAsync(db);

        var csv = Csv(
            $"X;SER;CRECE;AmbulatorioEstadual;{rotulo};celula;inclusao;NaoDedutivel;Criterio;;;;Criterio?;;CRECE");

        var r = await Servico(db).ImportarCsvAsync(csv, CancellationToken.None);

        // Ativar 974 perguntas de uma vez transformaria o wizard num interrogatório. A curadoria
        // decide o que vira pergunta de verdade e o que é só texto do manual.
        (await db.RegulacaoRegras.AsNoTracking()
            .Where(x => x.ProcedimentoId == procedimentoId).AllAsync(x => !x.Ativo))
            .Should().BeTrue();

        r.Avisos.Should().Contain(a => a.Contains("INATIVAS"));
    }

    [Fact]
    public async Task Recurso_fora_do_catalogo_vira_aviso_e_nao_erro()
    {
        await using var db = fixture.CriarDbContext();
        await CatalogoAsync(db);

        var csv = Csv(
            "X;SER;CRECE;AmbulatorioEstadual;RECURSO QUE NAO EXISTE;SEM_PAR;inclusao;NaoDedutivel;Y;;;;Y?;;CRECE");

        // 369 das 1.169 linhas vêm sem par no catálogo. Falhar a importação por causa delas
        // deixaria as outras 800 de fora.
        var r = await Servico(db).ImportarCsvAsync(csv, CancellationToken.None);

        r.Lidas.Should().Be(1);
        r.Criadas.Should().Be(0);
        r.SemRecurso.Should().Be(1);
        r.Avisos.Should().Contain(a => a.Contains("não existe no catálogo"));
    }

    [Fact]
    public async Task O_rotulo_casa_mesmo_com_acento_e_espaco_a_mais()
    {
        await using var db = fixture.CriarDbContext();
        var (procedimentoId, rotulo) = await CatalogoAsync(db);

        // Os catálogos variam a grafia entre si e em relação ao manual.
        var comRuido = rotulo.Replace("CARDIOLOGIA", "CARDIOLOGÍA  ");
        var csv = Csv(
            $"X;SER;CRECE;AmbulatorioEstadual;{comRuido};celula;inclusao;Documental;Laudo;;;;;Laudo do cardiologista;CRECE");

        var r = await Servico(db).ImportarCsvAsync(csv, CancellationToken.None);

        r.Criadas.Should().Be(1);
        (await db.RegulacaoRegras.AsNoTracking()
            .FirstAsync(x => x.ProcedimentoId == procedimentoId))
            .DocumentoRotulo.Should().Be("Laudo do cardiologista");
    }

    [Fact]
    public async Task Ponto_e_virgula_dentro_de_aspas_nao_parte_a_regra_ao_meio()
    {
        await using var db = fixture.CriarDbContext();
        var (procedimentoId, rotulo) = await CatalogoAsync(db);

        // 98 das 1.169 linhas do manual vêm entre aspas, porque o texto clínico usa ponto e
        // vírgula. Partindo por `;` sem olhar as aspas, o critério entra cortado e o pedaço final
        // vai parar na coluna da FONTE — que é a linha que a unidade lê para saber de onde veio a
        // exigência. Ela leria "infecções de repetição (mais de 8 no último ano)" como se fosse a
        // página do manual.
        var csv = Csv(
            $"X;SER;CRECE;AmbulatorioEstadual;{rotulo};celula;inclusao;NaoDedutivel;"
            + "\"Imunodeficiência primária: pneumonia de repetição (mais de 2 no último ano); "
            + "infecções de repetição (mais de 8 no último ano)\";;;;;;CRECE p.12");

        var r = await Servico(db).ImportarCsvAsync(csv, CancellationToken.None);

        r.Criadas.Should().Be(1);

        var regra = await db.RegulacaoRegras.AsNoTracking()
            .FirstAsync(x => x.ProcedimentoId == procedimentoId);

        regra.Descricao.Should().Contain("infecções de repetição (mais de 8 no último ano)");
        regra.Descricao.Should().NotStartWith("\"");
        regra.Fonte.Should().Be("CRECE p.12");
    }

    [Fact]
    public async Task Aspas_duplicadas_viram_uma_aspa_e_nao_quebram_o_registro()
    {
        await using var db = fixture.CriarDbContext();
        var (procedimentoId, rotulo) = await CatalogoAsync(db);

        var csv = Csv(
            $"X;SER;CRECE;AmbulatorioEstadual;{rotulo};celula;inclusao;NaoDedutivel;"
            + "\"Laudo dito \"\"recente\"\"; até 6 meses\";;;;;;CRECE p.9");

        var r = await Servico(db).ImportarCsvAsync(csv, CancellationToken.None);

        r.Criadas.Should().Be(1);
        (await db.RegulacaoRegras.AsNoTracking().FirstAsync(x => x.ProcedimentoId == procedimentoId))
            .Descricao.Should().Be("Laudo dito \"recente\"; até 6 meses");
    }

    [Fact]
    public async Task Nova_versao_desativa_a_anterior_em_vez_de_sobrescrever()
    {
        await using var db = fixture.CriarDbContext();
        var (procedimentoId, _) = await CatalogoAsync(db);
        var servico = Servico(db);

        var pedido = new SalvarRegulacaoRegraRequest(
            procedimentoId, null, SistemaRegulacao.Ser, TipoRegraRegulacao.NaoDedutivel,
            SeveridadeRegraRegulacao.Bloqueia, "texto do manual", null, null, null, null, false,
            null, null, "Tem laudo?", RespostaRegraRegulacao.Nao, null, null, null, null, true, 0);

        var v1 = await servico.CriarAsync(pedido, CancellationToken.None);
        var v2 = await servico.NovaVersaoAsync(
            v1.Id, pedido with { Descricao = "texto corrigido" }, CancellationToken.None);

        v2.Versao.Should().Be(2);
        v2.Ativo.Should().BeTrue();

        // A anterior fica — é ela que explica as respostas já dadas.
        var anterior = await db.RegulacaoRegras.AsNoTracking().FirstAsync(x => x.Id == v1.Id);
        anterior.Ativo.Should().BeFalse();
        anterior.Descricao.Should().Be("texto do manual");
    }

    [Fact]
    public async Task Regra_nao_dedutivel_sem_a_resposta_que_bloqueia_e_recusada()
    {
        await using var db = fixture.CriarDbContext();
        var (procedimentoId, _) = await CatalogoAsync(db);

        var semResposta = new SalvarRegulacaoRegraRequest(
            procedimentoId, null, null, TipoRegraRegulacao.NaoDedutivel,
            SeveridadeRegraRegulacao.Bloqueia, "texto", null, null, null, null, false,
            null, null, "Pergunta?", null, null, null, null, null, true, 0);

        // Sem saber qual resposta barra, a regra não decide nada — e o pior: pareceria decidir.
        var acao = () => Servico(db).CriarAsync(semResposta, CancellationToken.None);
        await acao.Should().ThrowAsync<ValidacaoException>();
    }

    [Fact]
    public async Task Regra_dedutivel_sem_criterio_nenhum_e_recusada()
    {
        await using var db = fixture.CriarDbContext();
        var (procedimentoId, _) = await CatalogoAsync(db);

        var vazia = new SalvarRegulacaoRegraRequest(
            procedimentoId, null, null, TipoRegraRegulacao.Dedutivel,
            SeveridadeRegraRegulacao.Bloqueia, "texto", null, null, null, null, false,
            null, null, null, null, null, null, null, null, true, 0);

        var acao = () => Servico(db).CriarAsync(vazia, CancellationToken.None);
        await acao.Should().ThrowAsync<ValidacaoException>();
    }
}
