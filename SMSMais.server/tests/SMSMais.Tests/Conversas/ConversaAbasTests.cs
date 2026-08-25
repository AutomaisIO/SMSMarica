using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NSubstitute;
using SMSMais.Core.Conversas;
using SMSMais.Core.Conversas.Dtos;
using SMSMais.Core.Identidade;
using SMSMais.Core.Identidade.Dtos;
using SMSMais.Core.Institucional;
using SMSMais.Core.Notificacoes.WhatsApp;
using SMSMais.Core.Pacientes;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Conversas;
using SMSMais.Data.Entities.Enums;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Conversas;

/// <summary>
/// Semântica das abas da lista: <c>Minhas</c> (dono == eu) e <c>Unidade</c>/fila (sem dono, das
/// minhas unidades + triagem geral) são DISJUNTAS — a conversa puxada sai da fila de todo mundo
/// e passa a existir só na lista pessoal do dono (+ Todas, destravada para todo operador pelo
/// ADR-0048). Se este arquivo quebrar, o roteamento do SignalR (Grupos) tem de mudar junto — um
/// espelha o outro.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class ConversaAbasTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Conversa_com_dono_aparece_em_Minhas_e_some_da_fila()
    {
        await using var db = fixture.CriarDbContext();
        var (dono, unidade) = await CriarOperadorAsync(db);
        var colega = await CriarUsuarioVinculadoAsync(db, unidade);
        var conversa = await CriarConversaAsync(db, operadorId: dono, unidadeId: unidade);

        var doDono = CriarServico(db, dono);
        var minhas = await doDono.ListarAsync(AbaConversas.Minhas, null);
        var filaDono = await doDono.ListarAsync(AbaConversas.Unidade, null);
        var filaColega = await CriarServico(db, colega).ListarAsync(AbaConversas.Unidade, null);

        Assert.Contains(minhas, c => c.Id == conversa.Id);
        Assert.DoesNotContain(filaDono, c => c.Id == conversa.Id);
        Assert.DoesNotContain(filaColega, c => c.Id == conversa.Id);
    }

    [Fact]
    public async Task Fila_traz_sem_dono_da_minha_unidade_e_da_triagem_geral_mas_nao_de_outra_unidade()
    {
        await using var db = fixture.CriarDbContext();
        var (usuario, unidade) = await CriarOperadorAsync(db);
        var (_, outraUnidade) = await CriarOperadorAsync(db);
        var daMinha = await CriarConversaAsync(db, unidadeId: unidade);
        var daGeral = await CriarConversaAsync(db);
        var daOutra = await CriarConversaAsync(db, unidadeId: outraUnidade);

        var fila = await CriarServico(db, usuario).ListarAsync(AbaConversas.Unidade, null);

        Assert.Contains(fila, c => c.Id == daMinha.Id);
        Assert.Contains(fila, c => c.Id == daGeral.Id);
        Assert.DoesNotContain(fila, c => c.Id == daOutra.Id);
    }

    [Fact]
    public async Task NaoAtribuidas_e_alias_da_fila()
    {
        await using var db = fixture.CriarDbContext();
        var (usuario, unidade) = await CriarOperadorAsync(db);
        await CriarConversaAsync(db, unidadeId: unidade);
        await CriarConversaAsync(db, operadorId: usuario, unidadeId: unidade);

        var servico = CriarServico(db, usuario);
        var fila = await servico.ListarAsync(AbaConversas.Unidade, null);
        var naoAtribuidas = await servico.ListarAsync(AbaConversas.NaoAtribuidas, null);

        Assert.Equal(fila.Select(c => c.Id).ToHashSet(), naoAtribuidas.Select(c => c.Id).ToHashSet());
    }

    [Fact]
    public async Task Todas_e_destravada_todo_operador_ve_conversa_de_outra_unidade()
    {
        // ADR-0048: a aba Todas deixou de exigir supervisão — qualquer operador do módulo vê a
        // conversa de outra unidade/dono. É visibilidade de leitura; a posse segue trava de ação.
        await using var db = fixture.CriarDbContext();
        var (dono, unidade) = await CriarOperadorAsync(db);
        var (deFora, _) = await CriarOperadorAsync(db); // vinculado só à própria unidade, sem supervisão
        var comDono = await CriarConversaAsync(db, operadorId: dono, unidadeId: unidade);

        // Some da fila do de-fora (unidades disjuntas)…
        var filaDeFora = await CriarServico(db, deFora).ListarAsync(AbaConversas.Unidade, null);
        Assert.DoesNotContain(filaDeFora, c => c.Id == comDono.Id);

        // …mas aparece em Todas, mesmo SEM permissão de supervisão.
        var todasDeFora = await CriarServico(db, deFora).ListarAsync(AbaConversas.Todas, null);
        Assert.Contains(todasDeFora, c => c.Id == comDono.Id);
    }

    // ===================== HELPERS =====================

    private static ConversaService CriarServico(
        SmsMaisDbContext db, Guid usuarioId, params ModuloPermissao[] modulos)
    {
        if (modulos.Length == 0) modulos = [ModuloPermissao.Conversas];

        var identidade = Substitute.For<IIdentidadeService>();
        identidade.ObterPermissoesResolvidasAsync(usuarioId, Arg.Any<CancellationToken>())
            .Returns(Perms(modulos));

        // O hub FHIR não é o objeto destes testes: nome de paciente é decoração da lista.
        var resolver = Substitute.For<IPacienteResolver>();
        resolver.ResolverManyAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, PacienteResumo>());
        var accessor = new UsuarioAtualAccessorFake(usuarioId);

        return new ConversaService(
            db,
            Substitute.For<IWhatsAppCliente>(),
            Substitute.For<IPacientesService>(),
            accessor,
            identidade,
            new UsuarioUnidadeService(db, accessor),
            new NotificadorConversaNulo(),
            resolver,
            Substitute.For<IInstituicaoService>(),
            Options.Create(new ConversasOptions()),
            new ConfigurationBuilder().Build());
    }

    private static PermissoesResolvidasDto Perms(params ModuloPermissao[] modulos)
    {
        var lista = modulos.Select(m => new PermissaoModuloDto(m, AcoesPermissao.Todas)).ToList();
        return new PermissoesResolvidasDto(lista, [], lista);
    }

    private static async Task<(Guid Usuario, Guid Unidade)> CriarOperadorAsync(SmsMaisDbContext db)
    {
        var unidade = new Unidade
        {
            Id = Guid.NewGuid(),
            Nome = $"UNID {Guid.NewGuid():N}"[..24],
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
        };
        db.Unidades.Add(unidade);
        await db.SaveChangesAsync();
        var usuario = await CriarUsuarioVinculadoAsync(db, unidade.Id);
        return (usuario, unidade.Id);
    }

    private static async Task<Guid> CriarUsuarioVinculadoAsync(SmsMaisDbContext db, Guid unidadeId)
    {
        var u = new Usuario
        {
            Id = Guid.NewGuid(),
            NomeCompleto = $"ATENDENTE {Guid.NewGuid():N}"[..20],
            Email = $"{Guid.NewGuid():N}@teste.local",
            SenhaHash = "x",
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
        };
        db.Usuarios.Add(u);
        db.UsuarioUnidades.Add(new UsuarioUnidade
        {
            UsuarioId = u.Id,
            UnidadeId = unidadeId,
            CriadoEm = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        return u.Id;
    }

    private static async Task<Conversa> CriarConversaAsync(
        SmsMaisDbContext db, Guid? operadorId = null, Guid? unidadeId = null)
    {
        var c = new Conversa
        {
            Id = Guid.CreateVersion7(),
            Canal = CanalConversa.WhatsApp,
            TelefoneCanonical = $"5521{Random.Shared.NextInt64(100_000_000, 999_999_999)}",
            Status = StatusConversa.Aberta,
            OperadorResponsavelId = operadorId,
            UnidadeId = unidadeId,
            PrimeiroContatoEm = DateTime.UtcNow,
            CriadoEm = DateTime.UtcNow,
        };
        db.Conversas.Add(c);
        await db.SaveChangesAsync();
        return c;
    }
}
