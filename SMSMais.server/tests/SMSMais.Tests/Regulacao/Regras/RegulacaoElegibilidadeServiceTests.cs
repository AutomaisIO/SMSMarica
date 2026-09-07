using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

using NSubstitute;

using SMSMais.Core.Cidadao;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Cidadao.Dtos;
using SMSMais.Core.Identidade;
using SMSMais.Core.Identidade.Dtos;
using SMSMais.Core.Pacientes;
using SMSMais.Core.Regulacao.Anexos;
using SMSMais.Core.Regulacao.Comum;
using SMSMais.Core.Regulacao.Configuracao;
using SMSMais.Core.Regulacao.Regras;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Conversas;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Regulacao;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Regulacao.Regras;

/// <summary>
/// O motor ligado ao banco: busca as regras, avalia e <b>persiste</b> o veredito (plano 03).
///
/// <para>O que prendem: reavaliar <b>reescreve</b> os destinos em vez de empilhar (dois vereditos
/// para o mesmo sistema fariam a tela dizer "bloqueado" e "liberado" ao mesmo tempo); a resposta
/// do solicitante <b>sobrevive</b> à reavaliação; e a regra documental vira caixinha <b>uma vez
/// só</b> — recriar a caixinha deixaria os anexos já postos numa que ninguém mais vê.</para>
/// </summary>
[Collection(nameof(PostgresCollection))]
public class RegulacaoElegibilidadeServiceTests(PostgresFixture fixture)
{
    private static string Sufixo() => Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    private sealed class StoreFake : IArquivoExigenciaStore
    {
        public string MontarChave(Guid p, Guid a, string e) => $"Regulacao/{p:D}/{a:D}.{e}";
        public Task SalvarAsync(string c, byte[] b, CancellationToken ct) => Task.CompletedTask;
        public Task<byte[]?> LerAsync(string c, CancellationToken ct) => Task.FromResult<byte[]?>(null);
        public Task ExcluirAsync(string c, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed record Cenario(Guid SolicitacaoId, Guid ProcedimentoId, Guid UsuarioId, Guid UnidadeId);

    private static RegulacaoElegibilidadeService Montar(
        SmsMaisDbContext db, Cenario c, DateOnly? nascimento = null, Sexo sexo = Sexo.Feminino)
    {
        var acessor = new UsuarioAtualAccessorFake(c.UsuarioId, c.UnidadeId);
        var config = new RegulacaoConfiguracaoService(db, new MemoryCache(new MemoryCacheOptions()), acessor);

        var pacientes = Substitute.For<IPacientesService>();
        pacientes.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(PacienteDtoFabrica.Criar(
                Guid.NewGuid(), "PACIENTE REGRA", "52998224725", "700000000000000",
                nascimento: nascimento ?? new DateOnly(1986, 5, 10), sexo: sexo));

        var clinico = Substitute.For<ICidadaoClinicoService>();
        clinico.ListarExamesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<ExameResumoDto>());

        var identidade = Substitute.For<IIdentidadeService>();
        identidade.ObterPermissoesResolvidasAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new PermissoesResolvidasDto([], [], []));

        var escopo = new RegulacaoEscopo(db, acessor, identidade);
        var exigencias = new RegulacaoExigenciaService(db, new StoreFake(), config, acessor);

        return new RegulacaoElegibilidadeService(
            db, escopo, config, exigencias, pacientes, clinico, acessor);
    }

    private static async Task<Cenario> CenarioAsync(SmsMaisDbContext db)
    {
        var sufixo = Sufixo();
        var unidade = new Unidade { Id = Guid.NewGuid(), Nome = $"UNID REGRA {sufixo}", CriadoEm = DateTime.UtcNow };
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            NomeCompleto = "SOLICITANTE REGRA",
            Email = $"{Guid.NewGuid():N}@teste.local",
            SenhaHash = "x",
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
        };
        var procedimento = new RegulacaoProcedimento
        {
            Id = Guid.CreateVersion7(),
            NomeCanonico = $"PROC REGRA {sufixo}",
            NomeNormalizado = "PROC REGRA",
            Tipo = TipoProcedimentoRegulacao.Consulta,
            CriadoEm = DateTime.UtcNow,
        };
        db.Unidades.Add(unidade);
        db.Usuarios.Add(usuario);
        db.RegulacaoProcedimentos.Add(procedimento);
        await db.SaveChangesAsync();

        db.UsuarioUnidades.Add(new UsuarioUnidade
        {
            UsuarioId = usuario.Id,
            UnidadeId = unidade.Id,
            Principal = true,
            CriadoEm = DateTime.UtcNow,
        });

        // Duas origens externas: é entre elas que a ressalva de destino se manifesta.
        foreach (var sistema in new[] { SistemaRegulacao.Ser, SistemaRegulacao.Sernit })
        {
            db.RegulacaoProcedimentoOrigens.Add(new RegulacaoProcedimentoOrigem
            {
                Id = Guid.CreateVersion7(),
                ProcedimentoId = procedimento.Id,
                Sistema = sistema,
                ChaveExterna = $"{sistema}|{sufixo}",
                RotuloExterno = $"RECURSO {sufixo}",
                CriadoEm = DateTime.UtcNow,
            });
        }

        var solicitacao = new RegulacaoSolicitacao
        {
            Id = Guid.CreateVersion7(),
            Fluxo = FluxoRegulacao.Externo,
            UnidadeSolicitanteId = unidade.Id,
            CriadoPorUsuarioId = usuario.Id,
            PacienteId = Guid.NewGuid(),
            PacienteNome = "PACIENTE REGRA",
            PacienteCpf = "52998224725",
            ProcedimentoId = procedimento.Id,
            FormularioJson = """{"canonico":{"cid10":"C50.4"}}""",
            Status = StatusRegulacao.Rascunho,
            CriadoEm = DateTime.UtcNow,
        };
        db.RegulacaoSolicitacoes.Add(solicitacao);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        return new Cenario(solicitacao.Id, procedimento.Id, usuario.Id, unidade.Id);
    }

    private static RegulacaoRegra NovaRegra(
        Guid procedimentoId, TipoRegraRegulacao tipo, SistemaRegulacao? sistema = null,
        int? idadeMin = null, string? pergunta = null,
        RespostaRegraRegulacao? respostaBloqueia = null, string? documento = null) => new()
        {
            Id = Guid.CreateVersion7(),
            ProcedimentoId = procedimentoId,
            Tipo = tipo,
            Severidade = SeveridadeRegraRegulacao.Bloqueia,
            Sistema = sistema,
            Descricao = "regra do manual",
            IdadeMinAnos = idadeMin,
            Pergunta = pergunta,
            RespostaBloqueia = respostaBloqueia,
            DocumentoRotulo = documento,
            Obrigatorio = true,
            Versao = 1,
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
        };

    [Fact]
    public async Task Avaliar_grava_os_destinos_e_reavaliar_os_reescreve()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CenarioAsync(db);

        // Bloqueia só o SER: o pedido segue pelo SERNIT — a ressalva de destino.
        db.RegulacaoRegras.Add(NovaRegra(
            c.ProcedimentoId, TipoRegraRegulacao.Dedutivel, SistemaRegulacao.Ser, idadeMin: 90));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var servico = Montar(db, c);
        var primeira = await servico.AvaliarAsync(c.SolicitacaoId, CancellationToken.None);

        primeira.DestinosPermitidos.Should().ContainSingle()
            .Which.Should().Be(SistemaRegulacao.Sernit);

        var destinos = await db.RegulacaoSolicitacaoDestinos.AsNoTracking()
            .Where(d => d.SolicitacaoId == c.SolicitacaoId).ToListAsync();
        destinos.Should().HaveCount(2);
        destinos.Single(d => d.Sistema == SistemaRegulacao.Ser)
            .Situacao.Should().Be(SituacaoDestinoRegulacao.Bloqueado);

        // Reavaliar não pode empilhar veredito: dois para o mesmo sistema fariam a tela dizer
        // "bloqueado" e "liberado" ao mesmo tempo.
        db.ChangeTracker.Clear();
        await Montar(db, c).AvaliarAsync(c.SolicitacaoId, CancellationToken.None);

        (await db.RegulacaoSolicitacaoDestinos.AsNoTracking()
            .CountAsync(d => d.SolicitacaoId == c.SolicitacaoId))
            .Should().Be(2);
    }

    [Fact]
    public async Task A_resposta_do_solicitante_sobrevive_a_reavaliacao()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CenarioAsync(db);

        var regra = NovaRegra(
            c.ProcedimentoId, TipoRegraRegulacao.NaoDedutivel,
            pergunta: "Tem marca-passo?", respostaBloqueia: RespostaRegraRegulacao.Sim);
        db.RegulacaoRegras.Add(regra);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var comPergunta = await Montar(db, c).AvaliarAsync(c.SolicitacaoId, CancellationToken.None);
        comPergunta.PerguntasPendentes.Should().ContainSingle();
        comPergunta.BloqueiaEnvio.Should().BeTrue();

        db.ChangeTracker.Clear();
        var respondida = await Montar(db, c).ResponderAsync(
            c.SolicitacaoId,
            new Dictionary<Guid, RespostaRegraRegulacao> { [regra.Id] = RespostaRegraRegulacao.Nao },
            CancellationToken.None);
        respondida.PerguntasPendentes.Should().BeEmpty();
        respondida.BloqueiaEnvio.Should().BeFalse();

        // Reavaliar depois (por outro motivo qualquer) não pode reabrir a pergunta já respondida.
        db.ChangeTracker.Clear();
        var deNovo = await Montar(db, c).AvaliarAsync(c.SolicitacaoId, CancellationToken.None);
        deNovo.PerguntasPendentes.Should().BeEmpty();

        var resposta = await db.RegulacaoSolicitacaoRespostasRegra.AsNoTracking()
            .FirstAsync(r => r.SolicitacaoId == c.SolicitacaoId && r.RegraId == regra.Id);
        resposta.Resposta.Should().Be(RespostaRegraRegulacao.Nao);
        resposta.RegraVersao.Should().Be(1, "a resposta fica presa à versão da regra que foi vista");
    }

    [Fact]
    public async Task A_regra_documental_vira_caixinha_uma_vez_so()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CenarioAsync(db);

        db.RegulacaoRegras.Add(NovaRegra(
            c.ProcedimentoId, TipoRegraRegulacao.Documental, documento: "Laudo do cardiologista"));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        await Montar(db, c).AvaliarAsync(c.SolicitacaoId, CancellationToken.None);
        db.ChangeTracker.Clear();
        await Montar(db, c).AvaliarAsync(c.SolicitacaoId, CancellationToken.None);

        // Recriar a caixinha deixaria os anexos já postos numa que ninguém mais vê.
        var caixinhas = await db.RegulacaoSolicitacaoExigencias.AsNoTracking()
            .Where(e => e.SolicitacaoId == c.SolicitacaoId && e.RegraId != null)
            .ToListAsync();
        caixinhas.Should().ContainSingle().Which.Titulo.Should().Be("Laudo do cardiologista");
    }

    [Fact]
    public async Task Exame_de_outro_paciente_nao_entra_na_solicitacao()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CenarioAsync(db);

        db.RegulacaoRegras.Add(NovaRegra(
            c.ProcedimentoId, TipoRegraRegulacao.Documental, documento: "Raio-X"));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var servico = Montar(db, c);
        await servico.AvaliarAsync(c.SolicitacaoId, CancellationToken.None);

        var exigencia = await db.RegulacaoSolicitacaoExigencias.AsNoTracking()
            .FirstAsync(e => e.SolicitacaoId == c.SolicitacaoId && e.RegraId != null);

        // O dublê do serviço clínico devolve lista vazia: nenhum exame é deste paciente. A
        // checagem tem de ser essa — sem ela, um id adivinhado anexaria o exame de outra pessoa
        // à solicitação, e o erro só apareceria na mesa da regulação.
        var acao = () => servico.UsarExameInternoAsync(
            c.SolicitacaoId, exigencia.Id, Guid.NewGuid(), null, CancellationToken.None);

        await acao.Should().ThrowAsync<ValidacaoException>();
    }

    [Fact]
    public async Task O_CID_da_solicitacao_alimenta_a_regra()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CenarioAsync(db);

        // O cenário grava `cid10 = C50.4` no formulário canônico.
        var regra = NovaRegra(c.ProcedimentoId, TipoRegraRegulacao.Dedutivel);
        regra.CidsExcluidosJson = JsonSerializer.Serialize(new[] { "C50" });
        db.RegulacaoRegras.Add(regra);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var r = await Montar(db, c).AvaliarAsync(c.SolicitacaoId, CancellationToken.None);

        r.DestinosPermitidos.Should().BeEmpty("C50.4 casa por prefixo com o excluído C50");
        r.BloqueiaEnvio.Should().BeTrue();
    }
}
