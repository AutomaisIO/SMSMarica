using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Midias;
using SMSMais.Core.Midias.Dtos;
using SMSMais.Core.Pacientes;
using SMSMais.Core.Regulacao.Anexos;
using SMSMais.Core.Regulacao.Configuracao;
using SMSMais.Core.Regulacao.Formularios;
using SMSMais.Core.Regulacao.Legado;
using SMSMais.Core.Regulacao.Solicitacoes;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Conversas;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Regulacao;
using SMSMais.Data.Entities.Ser;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Regulacao.Legado;

/// <summary>
/// Aposentadoria dos rascunhos por sistema (tarefa 2.9 do plano 02).
///
/// <para>O que estes testes prendem: a migração <b>não duplica</b> quando roda duas vezes (a fila
/// nasceria com o mesmo pedido em dobro); rascunho sem paciente, sem procedimento ou sem unidade
/// <b>não é forçado</b> para dentro do modelo — ele fica onde está com o motivo escrito, porque
/// solicitação com dado adivinhado cai na fila da unidade errada e ninguém consegue triar; e,
/// depois do corte, a tela antiga <b>recusa escrita</b> em vez de aceitar uma edição que se
/// perderia.</para>
/// </summary>
[Collection(nameof(PostgresCollection))]
public class MigradorRascunhosLegadosTests(PostgresFixture fixture)
{
    private static string Sufixo() => Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    private sealed class StoreFake : IArquivoExigenciaStore
    {
        public Dictionary<string, byte[]> Salvos { get; } = [];
        public string MontarChave(Guid p, Guid a, string e) => $"Regulacao/{p:D}/{a:D}.{e}";
        public Task SalvarAsync(string c, byte[] b, CancellationToken ct)
        {
            Salvos[c] = b;
            return Task.CompletedTask;
        }
        public Task<byte[]?> LerAsync(string c, CancellationToken ct) =>
            Task.FromResult(Salvos.TryGetValue(c, out var v) ? v : null);
        public Task ExcluirAsync(string c, CancellationToken ct)
        {
            Salvos.Remove(c);
            return Task.CompletedTask;
        }
    }

    private sealed record Cenario(
        Guid UnidadeId, Guid UsuarioId, Guid ProcedimentoId, Guid VersaoFormularioId, string ChaveExterna);

    /// <summary>Catálogo, unidade, usuário vinculado e a origem SER que casa com o rascunho.</summary>
    private static async Task<Cenario> MontarCenarioAsync(SmsMaisDbContext db, string recursoValor)
    {
        var unidade = new Unidade { Id = Guid.NewGuid(), Nome = $"UNID LEGADO {Sufixo()}", CriadoEm = DateTime.UtcNow };
        db.Unidades.Add(unidade);

        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            NomeCompleto = "AUTOR DO RASCUNHO",
            Email = $"{Guid.NewGuid():N}@teste.local",
            SenhaHash = "x",
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
        };
        db.Usuarios.Add(usuario);

        var procedimento = new RegulacaoProcedimento
        {
            Id = Guid.CreateVersion7(),
            NomeCanonico = $"PROC LEGADO {Sufixo()}",
            NomeNormalizado = "PROC LEGADO",
            Tipo = TipoProcedimentoRegulacao.Consulta,
            CriadoEm = DateTime.UtcNow,
        };
        db.RegulacaoProcedimentos.Add(procedimento);
        await db.SaveChangesAsync();

        db.UsuarioUnidades.Add(new UsuarioUnidade
        {
            UsuarioId = usuario.Id,
            UnidadeId = unidade.Id,
            Principal = true,
            CriadoEm = DateTime.UtcNow,
        });

        // A chave é a MESMA que o sincronismo do catálogo monta: `{tipo}|{valor}|{ramo}`. Se ela
        // divergir, a migração não acha o procedimento e todo rascunho vira "não migrado".
        var chave = $"{(int)TipoRecursoSer.Consulta}|{recursoValor}|NAO_AE";
        db.RegulacaoProcedimentoOrigens.Add(new RegulacaoProcedimentoOrigem
        {
            Id = Guid.CreateVersion7(),
            ProcedimentoId = procedimento.Id,
            Sistema = SistemaRegulacao.Ser,
            ChaveExterna = chave,
            RotuloExterno = "Consulta em Cardiologia",
            Ramo = "NAO_AE",
            CriadoEm = DateTime.UtcNow,
        });

        var versao = new RegulacaoFormularioVersao
        {
            Id = Guid.CreateVersion7(),
            Esquema = "externo.uniao",
            ProcedimentoId = procedimento.Id,
            DefinicaoJson = "[]",
            Hash = Guid.NewGuid().ToString("N"),
            CriadoEm = DateTime.UtcNow,
        };
        db.RegulacaoFormularioVersoes.Add(versao);
        await db.SaveChangesAsync();

        return new Cenario(unidade.Id, usuario.Id, procedimento.Id, versao.Id, chave);
    }

    private static SerSolicitacaoRascunho NovoRascunhoSer(
        Guid? autorId, string recursoValor, string? cns, StatusRascunhoSer status = StatusRascunhoSer.Rascunho) =>
        new()
        {
            Id = Guid.NewGuid(),
            Status = status,
            Tipo = TipoRecursoSer.Consulta,
            AmbulatorioEstadual = false,
            RecursoValor = recursoValor,
            RecursoRotulo = "Consulta em Cardiologia",
            Cns = cns,
            PacienteNome = "MARIA DO TESTE",
            Hipotese = "DOR TORACICA",
            CamposJson = """{"form0:procedimento":"DOR TORACICA"}""",
            CriadoPor = autorId,
            CriadoEm = DateTime.UtcNow.AddDays(-3),
        };

    private static (MigradorRascunhosLegadosService Servico, IPacientesService Pacientes, StoreFake Store)
        Montar(SmsMaisDbContext db, Guid usuarioSessaoId, Guid versaoFormularioId)
    {
        var acessor = new UsuarioAtualAccessorFake(usuarioSessaoId, null);
        var config = new RegulacaoConfiguracaoService(db, new MemoryCache(new MemoryCacheOptions()), acessor);
        var pacientes = Substitute.For<IPacientesService>();
        var midias = Substitute.For<IMidiasService>();
        var store = new StoreFake();

        var form = Substitute.For<IRegulacaoFormularioService>();
        form.ObterOuGerarAsync(Arg.Any<Guid>(), Arg.Any<FluxoRegulacao>(), Arg.Any<CancellationToken>())
            .Returns(new RegulacaoFormularioDto(versaoFormularioId, "externo.uniao", []));

        var servico = new MigradorRascunhosLegadosService(
            db, pacientes, form, store, midias, config, new RegulacaoEventoService(db, acessor), acessor,
            NullLogger<MigradorRascunhosLegadosService>.Instance);

        return (servico, pacientes, store);
    }

    private static void PacienteExiste(IPacientesService pacientes, string cns, Guid id) =>
        pacientes.ObterPorCnsAsync(cns, Arg.Any<CancellationToken>())
            .Returns(new PacienteExistenciaDto(id, "MARIA DO TESTE", "52998224725", true));

    [Fact]
    public async Task Migra_cada_rascunho_uma_vez()
    {
        await using var db = fixture.CriarDbContext();
        var recurso = Sufixo();
        var c = await MontarCenarioAsync(db, recurso);

        const string cns = "700000000000001";
        var rascunho = NovoRascunhoSer(c.UsuarioId, recurso, cns);
        db.SerSolicitacaoRascunhos.Add(rascunho);
        await db.SaveChangesAsync();

        var (servico, pacientes, _) = Montar(db, c.UsuarioId, c.VersaoFormularioId);
        var pacienteId = Guid.NewGuid();
        PacienteExiste(pacientes, cns, pacienteId);

        var primeira = await servico.MigrarAsync(
            new MigrarRascunhosLegadosRequest(null, false), CancellationToken.None);

        // Asserção sobre ESTE rascunho, não sobre o total: a migração varre a base inteira (é o
        // que se quer em produção) e a bancada é compartilhada com os outros testes.
        primeira.Ser.Should().BeGreaterThanOrEqualTo(1);
        primeira.NaoMigrados.Should().NotContain(n => n.RascunhoId == rascunho.Id);

        var criada = await db.RegulacaoSolicitacoes.AsNoTracking()
            .Include(s => s.Exigencias)
            .FirstAsync(s => s.OrigemLegadoId == rascunho.Id);
        criada.Fluxo.Should().Be(FluxoRegulacao.Externo);
        criada.SistemaDestino.Should().Be(SistemaRegulacao.Ser);
        criada.Status.Should().Be(StatusRegulacao.Rascunho);
        criada.UnidadeSolicitanteId.Should().Be(c.UnidadeId);
        criada.PacienteId.Should().Be(pacienteId);
        criada.ProcedimentoId.Should().Be(c.ProcedimentoId);

        // O preenchimento antigo entra no ramo do sistema de origem — as chaves são nomes JSF,
        // não chaves canônicas, e traduzi-las agora seria adivinhação.
        var formulario = JsonDocument.Parse(criada.FormularioJson).RootElement;
        formulario.GetProperty("ser").GetProperty("form0:procedimento").GetString()
            .Should().Be("DOR TORACICA");

        // Toda solicitação nasce com a caixinha "Anexos gerais".
        criada.Exigencias.Should().ContainSingle(e => e.RegraId == null);

        // E com a trilha contando de onde veio: uma solicitação que aparece na fila sem ninguém
        // ter aberto pela tela precisa explicar a própria existência.
        var evento = await db.RegulacaoEventos.AsNoTracking()
            .FirstAsync(e => e.SolicitacaoId == criada.Id);
        evento.Tipo.Should().Be(TipoEventoRegulacao.Criacao);
        evento.Papel.Should().Be(PapelEventoRegulacao.Sistema);
        evento.DetalheJson.Should().Contain(rascunho.Id.ToString());

        // Rodar de novo não duplica: é o `origem_legado_id` (e o único parcial) que segura.
        var segunda = await servico.MigrarAsync(
            new MigrarRascunhosLegadosRequest(null, false), CancellationToken.None);
        segunda.JaMigrados.Should().BeGreaterThanOrEqualTo(1);

        (await db.RegulacaoSolicitacoes.CountAsync(s => s.OrigemLegadoId == rascunho.Id)).Should().Be(1);
    }

    [Fact]
    public async Task Sem_unidade_vinculada_registra_motivo_e_nao_falha()
    {
        await using var db = fixture.CriarDbContext();
        var recurso = Sufixo();
        var c = await MontarCenarioAsync(db, recurso);

        // Autor SEM nenhum vínculo em usuario_unidade — o caso que o plano manda tratar.
        var orfao = new Usuario
        {
            Id = Guid.NewGuid(),
            NomeCompleto = "AUTOR SEM UNIDADE",
            Email = $"{Guid.NewGuid():N}@teste.local",
            SenhaHash = "x",
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
        };
        db.Usuarios.Add(orfao);

        const string cns = "700000000000002";
        var rascunho = NovoRascunhoSer(orfao.Id, recurso, cns);
        db.SerSolicitacaoRascunhos.Add(rascunho);
        await db.SaveChangesAsync();

        var (servico, pacientes, _) = Montar(db, c.UsuarioId, c.VersaoFormularioId);
        PacienteExiste(pacientes, cns, Guid.NewGuid());

        var semFallback = await servico.MigrarAsync(
            new MigrarRascunhosLegadosRequest(null, false), CancellationToken.None);

        // Não explode e não inventa unidade: sobra como pendência, com o motivo legível.
        semFallback.NaoMigrados.Should().Contain(n => n.RascunhoId == rascunho.Id);
        semFallback.NaoMigrados.First(n => n.RascunhoId == rascunho.Id)
            .Motivo.Should().Contain("unidade");

        // Com a unidade informada pelo configurador, o mesmo rascunho passa.
        var comFallback = await servico.MigrarAsync(
            new MigrarRascunhosLegadosRequest(c.UnidadeId, false), CancellationToken.None);

        comFallback.NaoMigrados.Should().NotContain(n => n.RascunhoId == rascunho.Id);
        (await db.RegulacaoSolicitacoes.AsNoTracking()
            .Where(s => s.OrigemLegadoId == rascunho.Id)
            .Select(s => s.UnidadeSolicitanteId)
            .FirstAsync()).Should().Be(c.UnidadeId);
    }

    [Fact]
    public async Task Documento_de_11_digitos_e_tratado_como_CPF()
    {
        await using var db = fixture.CriarDbContext();
        var recurso = Sufixo();
        var c = await MontarCenarioAsync(db, recurso);

        // A coluna chama-se `cns`, mas guarda o que foi digitado no campo de documento do SER —
        // e ali cabe CPF. Metade dos rascunhos reais de produção estava assim (07/09/2026).
        const string cpf = "52998224725";
        var rascunho = NovoRascunhoSer(c.UsuarioId, recurso, cpf);
        db.SerSolicitacaoRascunhos.Add(rascunho);
        await db.SaveChangesAsync();

        var (servico, pacientes, _) = Montar(db, c.UsuarioId, c.VersaoFormularioId);
        var pacienteId = Guid.NewGuid();
        pacientes.ObterPorCpfAsync(cpf, Arg.Any<CancellationToken>())
            .Returns(new PacienteExistenciaDto(pacienteId, "MARIA DO TESTE", cpf, true));

        var r = await servico.MigrarAsync(
            new MigrarRascunhosLegadosRequest(null, false), CancellationToken.None);

        r.NaoMigrados.Should().NotContain(n => n.RascunhoId == rascunho.Id);

        var criada = await db.RegulacaoSolicitacoes.AsNoTracking()
            .FirstAsync(s => s.OrigemLegadoId == rascunho.Id);
        criada.PacienteId.Should().Be(pacienteId);
        criada.PacienteCns.Should().BeNull("um CPF não pode ser copiado para a coluna do CNS");
    }

    [Fact]
    public async Task Sem_paciente_no_cadastro_nao_migra_e_diz_por_que()
    {
        await using var db = fixture.CriarDbContext();
        var recurso = Sufixo();
        var c = await MontarCenarioAsync(db, recurso);

        const string cns = "700000000000003";
        var rascunho = NovoRascunhoSer(c.UsuarioId, recurso, cns);
        db.SerSolicitacaoRascunhos.Add(rascunho);
        await db.SaveChangesAsync();

        var (servico, pacientes, _) = Montar(db, c.UsuarioId, c.VersaoFormularioId);
        pacientes.ObterPorCnsAsync(cns, Arg.Any<CancellationToken>())
            .Returns((PacienteExistenciaDto?)null);

        var r = await servico.MigrarAsync(
            new MigrarRascunhosLegadosRequest(null, false), CancellationToken.None);

        r.NaoMigrados.Should().Contain(n => n.RascunhoId == rascunho.Id && n.Motivo.Contains(cns));
        (await db.RegulacaoSolicitacoes.AnyAsync(s => s.OrigemLegadoId == rascunho.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Recurso_fora_do_catalogo_canonico_nao_migra()
    {
        await using var db = fixture.CriarDbContext();
        var c = await MontarCenarioAsync(db, Sufixo());

        // Rascunho de um recurso que NÃO tem origem no catálogo: sem procedimento canônico não
        // há o que triar, e `procedimento_id` é obrigatório.
        const string cns = "700000000000004";
        var rascunho = NovoRascunhoSer(c.UsuarioId, $"SEM-CATALOGO-{Sufixo()}", cns);
        db.SerSolicitacaoRascunhos.Add(rascunho);
        await db.SaveChangesAsync();

        var (servico, pacientes, _) = Montar(db, c.UsuarioId, c.VersaoFormularioId);
        PacienteExiste(pacientes, cns, Guid.NewGuid());

        var r = await servico.MigrarAsync(
            new MigrarRascunhosLegadosRequest(null, false), CancellationToken.None);

        r.NaoMigrados.Should().Contain(n =>
            n.RascunhoId == rascunho.Id && n.Motivo.Contains("catálogo canônico"));
    }

    [Fact]
    public async Task Previa_nao_grava_nada()
    {
        await using var db = fixture.CriarDbContext();
        var recurso = Sufixo();
        var c = await MontarCenarioAsync(db, recurso);

        const string cns = "700000000000005";
        var rascunho = NovoRascunhoSer(c.UsuarioId, recurso, cns);
        db.SerSolicitacaoRascunhos.Add(rascunho);
        await db.SaveChangesAsync();

        var (servico, pacientes, _) = Montar(db, c.UsuarioId, c.VersaoFormularioId);
        PacienteExiste(pacientes, cns, Guid.NewGuid());

        var previa = await servico.PreviaAsync(CancellationToken.None);

        previa.Ser.Should().BeGreaterThanOrEqualTo(1, "a prévia conta o que migraria");
        previa.NaoMigrados.Should().NotContain(n => n.RascunhoId == rascunho.Id);
        (await db.RegulacaoSolicitacoes.AnyAsync(s => s.OrigemLegadoId == rascunho.Id))
            .Should().BeFalse("prévia não escreve");
    }

    [Fact]
    public async Task Fechar_telas_antigas_com_pendencia_e_recusado()
    {
        await using var db = fixture.CriarDbContext();
        var recurso = Sufixo();
        var c = await MontarCenarioAsync(db, recurso);

        const string cns = "700000000000006";
        var rascunho = NovoRascunhoSer(c.UsuarioId, recurso, cns);
        db.SerSolicitacaoRascunhos.Add(rascunho);
        await db.SaveChangesAsync();

        var (servico, pacientes, _) = Montar(db, c.UsuarioId, c.VersaoFormularioId);
        pacientes.ObterPorCnsAsync(cns, Arg.Any<CancellationToken>())
            .Returns((PacienteExistenciaDto?)null);

        var r = await servico.MigrarAsync(
            new MigrarRascunhosLegadosRequest(null, FecharTelasAntigas: true), CancellationToken.None);

        // Fechar com pendência deixaria alguém sem acesso ao próprio trabalho: a tela antiga não
        // aceita mais escrita e a solicitação nova nunca chegou a existir.
        r.NaoMigrados.Should().NotBeEmpty();
        r.TelasAntigasFechadasEm.Should().BeNull();
    }

    [Fact]
    public async Task Depois_do_corte_a_tela_antiga_recusa_escrita()
    {
        await using var db = fixture.CriarDbContext();
        var acessor = new UsuarioAtualAccessorFake(Guid.NewGuid(), null);
        var config = new RegulacaoConfiguracaoService(db, new MemoryCache(new MemoryCacheOptions()), acessor);
        var gate = new RascunhoLegadoGate(config);

        (await gate.EstadoAsync(CancellationToken.None)).SomenteLeitura.Should().BeFalse();

        await config.DefinirRascunhosLegadosMigradosAsync(DateTime.UtcNow, CancellationToken.None);

        var estado = await gate.EstadoAsync(CancellationToken.None);
        estado.SomenteLeitura.Should().BeTrue();
        estado.Substituto.Should().Be(RascunhoLegadoGate.RotaWizard);

        var acao = () => gate.GarantirEscritaPermitidaAsync("SER", CancellationToken.None);
        (await acao.Should().ThrowAsync<RecursoDescontinuadoException>())
            .Which.Substituto.Should().Be(RascunhoLegadoGate.RotaWizard);

        // Reabrir volta ao estado anterior — o caminho de volta existe.
        await config.DefinirRascunhosLegadosMigradosAsync(null, CancellationToken.None);
        (await gate.EstadoAsync(CancellationToken.None)).SomenteLeitura.Should().BeFalse();
    }
}
