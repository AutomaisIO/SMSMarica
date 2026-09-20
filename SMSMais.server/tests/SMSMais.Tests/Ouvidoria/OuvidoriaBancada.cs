using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SMSMais.Core.Auditoria;
using SMSMais.Core.Identidade;
using SMSMais.Core.Identidade.Dtos;
using SMSMais.Core.Institucional;
using SMSMais.Core.Notificacoes.WhatsApp;
using SMSMais.Core.Ouvidoria;
using SMSMais.Core.Ouvidoria.Dtos;
using SMSMais.Core.Pacientes;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Ouvidoria;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Ouvidoria;

/// <summary>
/// Seed e fábrica de services da ouvidoria para os testes de integração. Cada chamada cria
/// dados próprios (nome/e-mail com <c>Guid</c>) porque a bancada guarda o que rodadas anteriores
/// deixaram. As dependências externas (identidade, auditoria, pacientes, WhatsApp, instituição)
/// são <c>NSubstitute</c>: o que se testa aqui é a máquina de estados, não o hub FHIR nem o zap.
/// </summary>
internal static class OuvidoriaBancada
{
    public const string Teor = "Fui mal atendido na recepcao da unidade e ninguem me deu explicacao sobre a fila.";

    // ===================== services =====================

    /// <summary>Permissão de um módulo, com as ações desejadas.</summary>
    public sealed record Perm(ModuloPermissao Modulo, AcoesPermissao Acoes);

    public static Perm PermOuvidoria(AcoesPermissao acoes = AcoesPermissao.Todas) => new(ModuloPermissao.Ouvidoria, acoes);
    public static Perm PermSigilo(AcoesPermissao acoes = AcoesPermissao.Todas) => new(ModuloPermissao.OuvidoriaSigilo, acoes);
    public static Perm PermGestao(AcoesPermissao acoes = AcoesPermissao.Todas) => new(ModuloPermissao.OuvidoriaGestao, acoes);
    public static Perm PermPonto(AcoesPermissao acoes = AcoesPermissao.Todas) => new(ModuloPermissao.OuvidoriaPontoResposta, acoes);

    /// <summary>Tudo que o teste precisa enxergar das dependências mockadas depois de agir.</summary>
    public sealed record Ambiente(
        OuvidoriaManifestacaoService Manifestacoes,
        OuvidoriaPublicoService Publico,
        IAuditoriaService Auditoria,
        IWhatsAppCliente WhatsApp);

    /// <summary>
    /// Service como SISTEMA (sem usuário no contexto): vê tudo, autor "Cidadão". É o contexto do
    /// site público e das rotinas.
    /// </summary>
    public static Ambiente Sistema(SmsMaisDbContext db) => Criar(db, usuarioId: null, []);

    /// <summary>Service como um usuário autenticado com as permissões dadas (fail-closed para o resto).</summary>
    public static Ambiente Como(SmsMaisDbContext db, Guid usuarioId, params Perm[] permissoes)
        => Criar(db, usuarioId, permissoes);

    private static Ambiente Criar(SmsMaisDbContext db, Guid? usuarioId, Perm[] permissoes)
    {
        var accessor = new UsuarioAtualAccessorFake(usuarioId, ip: "127.0.0.1");

        var identidade = Substitute.For<IIdentidadeService>();
        var lista = permissoes.Select(p => new PermissaoModuloDto(p.Modulo, p.Acoes)).ToList();
        identidade.ObterPermissoesResolvidasAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new PermissoesResolvidasDto(lista, [], lista));

        var auditoria = Substitute.For<IAuditoriaService>();

        // Sem hub FHIR: o vínculo com Patient é best-effort e não é o objeto destes testes.
        var pacientes = Substitute.For<IPacientesService>();
        pacientes.ObterPorCpfAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<PacienteExistenciaDto?>(null));

        var whats = Substitute.For<IWhatsAppCliente>();
        whats.EnviarTextoAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>(),
                Arg.Any<OrigemEnvioWhatsApp>())
            .Returns(new EnvioWhatsAppResultado(true, $"wamid.teste.{Guid.NewGuid():N}", null));

        var instituicao = Substitute.For<IInstituicaoService>();
        instituicao.ObterAsync(Arg.Any<CancellationToken>()).Returns(IInstituicaoService.ObterPadrao());

        var catalogo = new OuvidoriaCatalogoService(db, accessor);
        var notificador = new OuvidoriaNotificador(whats, instituicao, NullLogger<OuvidoriaNotificador>.Instance);
        var manifestacoes = new OuvidoriaManifestacaoService(
            db, accessor, identidade, auditoria, pacientes, catalogo, notificador,
            NullLogger<OuvidoriaManifestacaoService>.Instance);
        var publico = new OuvidoriaPublicoService(db, manifestacoes, catalogo);

        return new Ambiente(manifestacoes, publico, auditoria, whats);
    }

    // ===================== seed =====================

    public static async Task<Guid> CriarUnidadeAsync(SmsMaisDbContext db)
    {
        var u = new Unidade
        {
            Id = Guid.NewGuid(),
            Nome = $"UNIDADE OUV {Guid.NewGuid():N}"[..30],
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
        };
        db.Unidades.Add(u);
        await db.SaveChangesAsync();
        return u.Id;
    }

    public static async Task<Guid> CriarUsuarioAsync(SmsMaisDbContext db, string nome = "TECNICO OUVIDORIA")
    {
        var u = new Usuario
        {
            Id = Guid.NewGuid(),
            NomeCompleto = $"{nome} {Guid.NewGuid():N}"[..40],
            Email = $"ouv-{Guid.NewGuid():N}@teste.local",
            SenhaHash = "x",
            Ativo = true,
            AcessoGlobal = true,
            CriadoEm = DateTime.UtcNow,
        };
        db.Usuarios.Add(u);
        await db.SaveChangesAsync();
        return u.Id;
    }

    /// <summary>Ponto de resposta (área central por padrão) com os membros dados.</summary>
    public static async Task<OuvidoriaPontoResposta> CriarPontoAsync(
        SmsMaisDbContext db,
        OuvidoriaTipoPontoResposta tipo = OuvidoriaTipoPontoResposta.AreaCentral,
        int? prazoDias = null,
        bool ativo = true,
        params Guid[] membros)
    {
        var agora = DateTime.UtcNow;
        var ponto = new OuvidoriaPontoResposta
        {
            Id = Guid.CreateVersion7(),
            Nome = $"PONTO {tipo} {Guid.NewGuid():N}"[..40],
            Tipo = tipo,
            PrazoDias = prazoDias,
            Ativo = ativo,
            CriadoEm = agora,
        };
        foreach (var membro in membros)
        {
            ponto.Membros.Add(new OuvidoriaPontoRespostaMembro
            {
                Id = Guid.CreateVersion7(),
                PontoRespostaId = ponto.Id,
                UsuarioId = membro,
                Titular = true,
                CriadoEm = agora,
            });
        }
        db.OuvidoriaPontosResposta.Add(ponto);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return ponto;
    }

    /// <summary>CPF de 11 dígitos único por chamada (não precisa ter DV válido: o service só conta dígitos).</summary>
    public static string CpfUnico() => Random.Shared.NextInt64(10_000_000_000, 99_999_999_999).ToString();

    public static ManifestanteDto ManifestanteIdentificado(string? nome = null) => new(
        nome ?? $"MANIFESTANTE {Guid.NewGuid():N}"[..30],
        CpfUnico(),
        "21999990000",
        null,
        null);

    public static RegistrarManifestacaoRequest Pedido(
        OuvidoriaTipo tipo,
        OuvidoriaIdentificacao identificacao = OuvidoriaIdentificacao.Identificada,
        ManifestanteDto? manifestante = null,
        Guid? unidadeId = null,
        Guid? assuntoId = null,
        string? teor = null)
        => new(
            tipo,
            identificacao,
            OuvidoriaCanal.Painel,
            OuvidoriaOrigem.Cidadao,
            teor ?? Teor,
            Resumo: null,
            assuntoId,
            SubassuntoId: null,
            unidadeId,
            DataFato: null,
            LocalFato: null,
            identificacao == OuvidoriaIdentificacao.Anonima ? null : manifestante ?? ManifestanteIdentificado(),
            Referido: null,
            EnvolvidoDescricao: null,
            ProtocoloExterno: null,
            SistemaExterno: null,
            RegulacaoSolicitacaoId: null,
            Anexos: []);

    /// <summary>Registra pelo service dado e devolve o retorno (com o código de acesso, quando houver).</summary>
    public static Task<ManifestacaoCriadaDto> RegistrarAsync(
        Ambiente amb,
        OuvidoriaTipo tipo,
        OuvidoriaIdentificacao identificacao = OuvidoriaIdentificacao.Identificada,
        ManifestanteDto? manifestante = null,
        Guid? unidadeId = null)
        => amb.Manifestacoes.RegistrarAsync(Pedido(tipo, identificacao, manifestante, unidadeId));

    /// <summary>Lê a manifestação direto do banco, sem cache do tracker.</summary>
    public static async Task<OuvidoriaManifestacao> LerAsync(SmsMaisDbContext db, Guid id)
    {
        db.ChangeTracker.Clear();
        return await db.OuvidoriaManifestacoes
            .AsNoTracking()
            .Include(m => m.Eventos.OrderBy(e => e.CriadoEm))
            .FirstAsync(m => m.Id == id);
    }

    /// <summary>Configuração vigente na bancada (pode não ser o default se alguma rodada a alterou).</summary>
    public static async Task<OuvidoriaConfiguracao> ConfigAsync(SmsMaisDbContext db)
    {
        var amb = Sistema(db);
        // Qualquer leitura garante o singleton; ListarAssuntos do público chama GarantirCatalogoBase.
        await amb.Publico.ListarAssuntosAsync();
        db.ChangeTracker.Clear();
        return await db.OuvidoriaConfiguracoes.AsNoTracking().FirstAsync(c => c.Id == OuvidoriaConfiguracao.IdSingleton);
    }
}
