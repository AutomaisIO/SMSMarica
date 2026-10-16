using System.Collections.Concurrent;
using System.Text;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

using NSubstitute;

using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Core.Identidade.Dtos;
using SMSMais.Core.Regulacao.Anexos;
using SMSMais.Core.Regulacao.Comum;
using SMSMais.Core.Regulacao.Configuracao;
using SMSMais.Core.Regulacao.Solicitacoes;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Regulacao;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Regulacao.Anexos;

/// <summary>
/// Caixinhas de exigência e seus anexos.
///
/// <para>O que estes testes guardam: tipo e tamanho recusados antes de o arquivo chegar ao
/// armazenamento; caixinha de <b>regra</b> versiona (documento criticado não é apagado, vira
/// versão anterior); caixinha de <b>anexos gerais</b> acumula, porque ali são documentos
/// diferentes; e arquivo já enviado ao sistema de regulação não se remove — é prova do que foi
/// mandado.</para>
/// </summary>
[Collection(nameof(PostgresCollection))]
public class RegulacaoExigenciaServiceTests(PostgresFixture fixture)
{
    /// <summary>Store em memória: o teste é do serviço, não do Spaces.</summary>
    private sealed class StoreFake : IArquivoExigenciaStore
    {
        public ConcurrentDictionary<string, byte[]> Arquivos { get; } = new();

        public string MontarChave(Guid pacienteId, Guid arquivoId, string extensao) =>
            $"Regulacao/{pacienteId:D}/{arquivoId:D}.{extensao}";

        public Task SalvarAsync(string chave, byte[] conteudo, CancellationToken ct)
        {
            Arquivos[chave] = conteudo;
            return Task.CompletedTask;
        }

        public Task<byte[]?> LerAsync(string chave, CancellationToken ct) =>
            Task.FromResult(Arquivos.TryGetValue(chave, out var c) ? c : null);

        public Task ExcluirAsync(string chave, CancellationToken ct)
        {
            Arquivos.TryRemove(chave, out _);
            return Task.CompletedTask;
        }
    }

    private static (RegulacaoExigenciaService Servico, StoreFake Store) Servico(SmsMaisDbContext db)
    {
        var store = new StoreFake();
        var config = new RegulacaoConfiguracaoService(
            db, new MemoryCache(new MemoryCacheOptions()), new UsuarioAtualAccessorFake());
        // Sem usuário no contexto o escopo é "tudo" (como num job) e nunca consulta a identidade:
        // estes testes são das regras de anexo, não do alcance.
        var acessor = new UsuarioAtualAccessorFake();
        var escopo = new RegulacaoEscopo(db, acessor, Substitute.For<IIdentidadeService>());
        var eventos = new RegulacaoEventoService(db, acessor);
        return (new RegulacaoExigenciaService(db, store, config, acessor, escopo, eventos), store);
    }

    /// <summary>
    /// Serviço com um usuário logado que é AGENTE (módulo RegulacaoTriagem) — o escopo devolve
    /// "tudo", então não é preciso montar RBAC de unidade. Usado para exercitar o caminho em que
    /// o download registra o acesso na trilha (que só roda com usuário no contexto).
    /// </summary>
    private static RegulacaoExigenciaService ServicoComAgente(SmsMaisDbContext db, Guid usuarioId)
    {
        var acessor = new UsuarioAtualAccessorFake(usuarioId);
        var config = new RegulacaoConfiguracaoService(
            db, new MemoryCache(new MemoryCacheOptions()), acessor);
        var identidade = Substitute.For<IIdentidadeService>();
        identidade.ObterPermissoesResolvidasAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new PermissoesResolvidasDto(
                Herdadas: [],
                Overrides: [],
                Resolvidas: [new PermissaoModuloDto(ModuloPermissao.RegulacaoTriagem, AcoesPermissao.Edicao)]));
        var escopo = new RegulacaoEscopo(db, acessor, identidade);
        var eventos = new RegulacaoEventoService(db, acessor);
        return new RegulacaoExigenciaService(db, new StoreFake(), config, acessor, escopo, eventos);
    }

    private static byte[] Bytes(int tamanho) => Encoding.UTF8.GetBytes(new string('x', tamanho));

    private static async Task<RegulacaoSolicitacao> SolicitacaoAsync(SmsMaisDbContext db)
    {
        var unidade = new Unidade
        {
            Id = Guid.NewGuid(),
            Nome = $"UNID ANEXO {Guid.NewGuid():N}"[..40],
            CriadoEm = DateTime.UtcNow,
        };
        db.Unidades.Add(unidade);

        var procedimento = new RegulacaoProcedimento
        {
            Id = Guid.CreateVersion7(),
            NomeCanonico = $"PROC ANEXO {Guid.NewGuid():N}"[..40],
            NomeNormalizado = "PROC ANEXO",
            Tipo = TipoProcedimentoRegulacao.Consulta,
            CriadoEm = DateTime.UtcNow,
        };
        db.RegulacaoProcedimentos.Add(procedimento);

        var s = new RegulacaoSolicitacao
        {
            Id = Guid.CreateVersion7(),
            Fluxo = FluxoRegulacao.Externo,
            UnidadeSolicitanteId = unidade.Id,
            CriadoPorUsuarioId = Guid.NewGuid(),
            PacienteId = Guid.NewGuid(),
            PacienteNome = "PACIENTE TESTE",
            ProcedimentoId = procedimento.Id,
            Status = StatusRegulacao.Rascunho,
            CriadoEm = DateTime.UtcNow,
        };
        db.RegulacaoSolicitacoes.Add(s);
        await db.SaveChangesAsync();
        return s;
    }

    private static async Task<RegulacaoSolicitacaoExigencia> CaixinhaDeRegraAsync(
        SmsMaisDbContext db, Guid solicitacaoId)
    {
        var e = new RegulacaoSolicitacaoExigencia
        {
            Id = Guid.CreateVersion7(),
            SolicitacaoId = solicitacaoId,
            RegraId = Guid.NewGuid(),
            Titulo = "Laudo de biópsia",
            Obrigatoria = true,
            Situacao = SituacaoExigenciaRegulacao.Pendente,
            Ordem = 1,
        };
        db.RegulacaoSolicitacaoExigencias.Add(e);
        await db.SaveChangesAsync();
        return e;
    }

    [Fact]
    public async Task Tipo_nao_permitido_e_recusado()
    {
        await using var db = fixture.CriarDbContext();
        var s = await SolicitacaoAsync(db);
        var e = await CaixinhaDeRegraAsync(db, s.Id);
        var (servico, store) = Servico(db);

        var acao = () => servico.AnexarAsync(
            s.Id, e.Id, "planilha.zip", "application/zip", Bytes(10), CancellationToken.None);

        await acao.Should().ThrowAsync<ValidacaoException>();
        store.Arquivos.Should().BeEmpty("recusa acontece antes de tocar o armazenamento");
    }

    [Fact]
    public async Task Acima_do_limite_e_recusado()
    {
        await using var db = fixture.CriarDbContext();
        var s = await SolicitacaoAsync(db);
        var e = await CaixinhaDeRegraAsync(db, s.Id);
        var (servico, store) = Servico(db);

        // Default de 15 MB na configuração.
        var acao = () => servico.AnexarAsync(
            s.Id, e.Id, "foto.jpg", "image/jpeg", Bytes(16 * 1024 * 1024), CancellationToken.None);

        await acao.Should().ThrowAsync<ValidacaoException>();
        store.Arquivos.Should().BeEmpty();
    }

    [Fact]
    public async Task Content_type_com_charset_e_aceito()
    {
        await using var db = fixture.CriarDbContext();
        var s = await SolicitacaoAsync(db);
        var e = await CaixinhaDeRegraAsync(db, s.Id);
        var (servico, _) = Servico(db);

        // O navegador manda "application/pdf; charset=..." em alguns casos; cortar no ';' evita
        // recusar arquivo legítimo por causa do parâmetro.
        var a = await servico.AnexarAsync(
            s.Id, e.Id, "laudo.pdf", "application/pdf; charset=binary", Bytes(10), CancellationToken.None);

        a.ContentType.Should().Be("application/pdf");
    }

    [Fact]
    public async Task Nova_versao_incrementa_e_marca_a_anterior_substituida()
    {
        await using var db = fixture.CriarDbContext();
        var s = await SolicitacaoAsync(db);
        var e = await CaixinhaDeRegraAsync(db, s.Id);
        var (servico, _) = Servico(db);

        var v1 = await servico.AnexarAsync(s.Id, e.Id, "laudo.pdf", "application/pdf", Bytes(10), CancellationToken.None);
        var v2 = await servico.AnexarAsync(s.Id, e.Id, "laudo-novo.pdf", "application/pdf", Bytes(20), CancellationToken.None);

        v1.Versao.Should().Be(1);
        v2.Versao.Should().Be(2);

        var noBanco = await db.RegulacaoExigenciaArquivos.AsNoTracking()
            .Where(a => a.ExigenciaId == e.Id).OrderBy(a => a.Versao).ToListAsync();
        noBanco[0].Situacao.Should().Be(SituacaoArquivoExigencia.Substituido);
        noBanco[1].Situacao.Should().Be(SituacaoArquivoExigencia.Atual);
        noBanco[1].SubstituiArquivoId.Should().Be(noBanco[0].Id, "a trilha diz qual arquivo trocou qual");

        // Anexar resolve a caixinha.
        (await db.RegulacaoSolicitacaoExigencias.AsNoTracking().FirstAsync(x => x.Id == e.Id))
            .Situacao.Should().Be(SituacaoExigenciaRegulacao.Atendida);
    }

    [Fact]
    public async Task Anexos_gerais_acumulam_em_vez_de_substituir()
    {
        await using var db = fixture.CriarDbContext();
        var s = await SolicitacaoAsync(db);
        var (servico, _) = Servico(db);

        var gerais = await servico.GarantirAnexosGeraisAsync(s.Id, CancellationToken.None);
        await servico.AnexarAsync(s.Id, gerais.Id, "a.jpg", "image/jpeg", Bytes(10), CancellationToken.None);
        await servico.AnexarAsync(s.Id, gerais.Id, "b.jpg", "image/jpeg", Bytes(10), CancellationToken.None);

        var noBanco = await db.RegulacaoExigenciaArquivos.AsNoTracking()
            .Where(a => a.ExigenciaId == gerais.Id).ToListAsync();

        noBanco.Should().HaveCount(2);
        noBanco.Should().OnlyContain(a => a.Situacao == SituacaoArquivoExigencia.Atual,
            "em 'Anexos gerais' são documentos diferentes, não versões do mesmo");
        noBanco.Should().OnlyContain(a => a.SubstituiArquivoId == null);
    }

    [Fact]
    public async Task Garantir_anexos_gerais_e_idempotente()
    {
        await using var db = fixture.CriarDbContext();
        var s = await SolicitacaoAsync(db);
        var (servico, _) = Servico(db);

        var a = await servico.GarantirAnexosGeraisAsync(s.Id, CancellationToken.None);
        var b = await servico.GarantirAnexosGeraisAsync(s.Id, CancellationToken.None);

        b.Id.Should().Be(a.Id);
        (await db.RegulacaoSolicitacaoExigencias.CountAsync(x => x.SolicitacaoId == s.Id)).Should().Be(1);
    }

    [Fact]
    public async Task Remover_depois_do_envio_e_recusado()
    {
        await using var db = fixture.CriarDbContext();
        var s = await SolicitacaoAsync(db);
        var e = await CaixinhaDeRegraAsync(db, s.Id);
        var (servico, store) = Servico(db);

        var a = await servico.AnexarAsync(s.Id, e.Id, "laudo.pdf", "application/pdf", Bytes(10), CancellationToken.None);

        var linha = await db.RegulacaoExigenciaArquivos.FirstAsync(x => x.Id == a.Id);
        linha.EnviadoAoSistemaEm = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var acao = () => servico.RemoverArquivoAsync(s.Id, a.Id, CancellationToken.None);

        await acao.Should().ThrowAsync<ConflitoException>();
        store.Arquivos.Should().HaveCount(1, "o conteúdo enviado continua sendo prova do que foi mandado");
    }

    [Fact]
    public async Task Remover_tira_do_armazenamento_e_devolve_a_caixinha_para_pendente()
    {
        await using var db = fixture.CriarDbContext();
        var s = await SolicitacaoAsync(db);
        var e = await CaixinhaDeRegraAsync(db, s.Id);
        var (servico, store) = Servico(db);

        var a = await servico.AnexarAsync(s.Id, e.Id, "laudo.pdf", "application/pdf", Bytes(10), CancellationToken.None);
        await servico.RemoverArquivoAsync(s.Id, a.Id, CancellationToken.None);

        store.Arquivos.Should().BeEmpty("é dado de paciente: retirado da tela, retirado do armazenamento");
        (await db.RegulacaoExigenciaArquivos.AsNoTracking().FirstAsync(x => x.Id == a.Id))
            .Situacao.Should().Be(SituacaoArquivoExigencia.Removido, "a linha fica na trilha");
        (await db.RegulacaoSolicitacaoExigencias.AsNoTracking().FirstAsync(x => x.Id == e.Id))
            .Situacao.Should().Be(SituacaoExigenciaRegulacao.Pendente);
    }

    [Fact]
    public async Task Anexo_de_outra_solicitacao_nao_e_servido()
    {
        await using var db = fixture.CriarDbContext();
        var s1 = await SolicitacaoAsync(db);
        var s2 = await SolicitacaoAsync(db);
        var e1 = await CaixinhaDeRegraAsync(db, s1.Id);
        var (servico, _) = Servico(db);

        var a = await servico.AnexarAsync(s1.Id, e1.Id, "laudo.pdf", "application/pdf", Bytes(10), CancellationToken.None);

        // Adivinhar o GUID do anexo não pode dar acesso pela rota de outra solicitação.
        var acao = () => servico.ObterConteudoAsync(s2.Id, a.Id, CancellationToken.None);

        await acao.Should().ThrowAsync<NaoEncontradoException>();
    }

    [Fact]
    public async Task Download_por_usuario_registra_acesso_uma_vez_na_trilha()
    {
        await using var db = fixture.CriarDbContext();
        var s = await SolicitacaoAsync(db);
        var e = await CaixinhaDeRegraAsync(db, s.Id);
        var usuarioId = Guid.CreateVersion7();
        var servico = ServicoComAgente(db, usuarioId);

        var a = await servico.AnexarAsync(
            s.Id, e.Id, "guia.pdf", "application/pdf", Bytes(10), CancellationToken.None);

        // Baixar duas vezes. Antes do fix, o LIKE sobre a coluna jsonb `detalhe_json` estourava
        // (42883: operator does not exist: jsonb ~~ jsonb) já na primeira chamada. E o acesso
        // repetido do mesmo usuário não pode duplicar o evento na trilha.
        var c1 = await servico.ObterConteudoAsync(s.Id, a.Id, CancellationToken.None);
        var c2 = await servico.ObterConteudoAsync(s.Id, a.Id, CancellationToken.None);

        c1.Conteudo.Should().HaveCount(10);
        c2.Conteudo.Should().HaveCount(10);

        var eventos = await db.RegulacaoEventos.AsNoTracking()
            .Where(x => x.SolicitacaoId == s.Id && x.Tipo == TipoEventoRegulacao.DocumentoAcessado)
            .ToListAsync();

        eventos.Should().ContainSingle("duas aberturas do mesmo usuário geram um único evento");
        eventos[0].UsuarioId.Should().Be(usuarioId);
        eventos[0].Papel.Should().Be(PapelEventoRegulacao.Agente);
    }
}
