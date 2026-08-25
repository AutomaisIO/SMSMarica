using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NSubstitute;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Conversas;
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
using ConversaDtos = SMSMais.Core.Conversas.Dtos;

namespace SMSMais.Tests.Conversas;

/// <summary>
/// Modelo de posse das conversas da Central de Atendimento: fila da unidade × lista pessoal.
/// As invariantes protegidas aqui: (1) responder ou assumir conversa SEM dono gera claim; (2)
/// posse de terceiro NUNCA muda por acidente — só por encaminhar/devolver/transferir explícitos;
/// (3) o escopo de acesso por id vale como trava (outra unidade → 404), não só como filtro de
/// listagem; (4) o resumo do sino espelha exatamente os predicados das filas.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class ConversaPosseTests(PostgresFixture fixture)
{
    // ===================== ASSUMIR =====================

    [Fact]
    public async Task Assumir_sem_dono_vira_dono_zera_badge_e_grava_Assumida()
    {
        await using var db = fixture.CriarDbContext();
        var (usuario, unidade) = await CriarOperadorAsync(db);
        var conversa = await CriarConversaAsync(db, unidadeId: unidade, naoLidas: 3);

        await CriarServico(db, usuario).AssumirAsync(conversa.Id);

        Assert.Equal(usuario, conversa.OperadorResponsavelId);
        Assert.Equal(unidade, conversa.UnidadeId);
        Assert.Equal(0, conversa.NaoLidas);
        var evento = await UltimoEventoAsync(db, conversa.Id);
        Assert.Equal(TipoEventoConversa.Assumida, evento.Tipo);
        Assert.Equal(usuario, evento.AtorUsuarioId);
        Assert.Equal(usuario, evento.ParaUsuarioId);
    }

    [Fact]
    public async Task Assumir_da_triagem_geral_herda_a_unidade_principal_do_operador()
    {
        await using var db = fixture.CriarDbContext();
        var (usuario, unidade) = await CriarOperadorAsync(db);
        var conversa = await CriarConversaAsync(db); // sem dono e sem unidade (balde geral)

        await CriarServico(db, usuario).AssumirAsync(conversa.Id);

        Assert.Equal(usuario, conversa.OperadorResponsavelId);
        Assert.Equal(unidade, conversa.UnidadeId);
    }

    [Fact]
    public async Task Assumir_conversa_de_terceiro_recusa_409_com_o_nome_do_dono_sem_mudar_posse()
    {
        await using var db = fixture.CriarDbContext();
        var (dono, unidade) = await CriarOperadorAsync(db);
        var colega = await CriarUsuarioVinculadoAsync(db, unidade);
        var conversa = await CriarConversaAsync(db, operadorId: dono, unidadeId: unidade);

        var ex = await Assert.ThrowsAsync<ConflitoException>(() =>
            CriarServico(db, colega).AssumirAsync(conversa.Id));

        Assert.Contains("assumida", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(dono, conversa.OperadorResponsavelId);
    }

    [Fact]
    public async Task Assumir_ja_sendo_dono_e_idempotente()
    {
        await using var db = fixture.CriarDbContext();
        var (usuario, unidade) = await CriarOperadorAsync(db);
        var conversa = await CriarConversaAsync(db, operadorId: usuario, unidadeId: unidade, naoLidas: 2);

        var servico = CriarServico(db, usuario);
        await servico.AssumirAsync(conversa.Id);
        await servico.AssumirAsync(conversa.Id); // segunda vez: no-op

        Assert.Equal(usuario, conversa.OperadorResponsavelId);
        Assert.Equal(0, conversa.NaoLidas);
        // Idempotente também na trilha: assumir o que já é meu não grava evento.
        Assert.Equal(0, await db.ConversaEventos.CountAsync(e => e.ConversaId == conversa.Id));
    }

    // ===================== RESPONDER (claim implícito) =====================

    [Fact]
    public async Task Responder_conversa_sem_dono_gera_claim_e_zera_o_badge()
    {
        await using var db = fixture.CriarDbContext();
        var (usuario, unidade) = await CriarOperadorAsync(db);
        var conversa = await CriarConversaAsync(db, unidadeId: unidade, naoLidas: 4, janelaAberta: true);

        await CriarServico(db, usuario).EnviarTextoAsync(
            conversa.Id, new ConversaDtos.EnviarMensagemRequest("Olá! Em que posso ajudar?"));

        Assert.Equal(usuario, conversa.OperadorResponsavelId);
        Assert.Equal(0, conversa.NaoLidas);
        Assert.Equal(TipoEventoConversa.Assumida, (await UltimoEventoAsync(db, conversa.Id)).Tipo);
    }

    [Fact]
    public async Task Responder_conversa_de_terceiro_nao_rouba_a_posse()
    {
        await using var db = fixture.CriarDbContext();
        var (dono, unidade) = await CriarOperadorAsync(db);
        var colega = await CriarUsuarioVinculadoAsync(db, unidade);
        var conversa = await CriarConversaAsync(db, operadorId: dono, unidadeId: unidade, naoLidas: 2, janelaAberta: true);

        await CriarServico(db, colega).EnviarTextoAsync(
            conversa.Id, new ConversaDtos.EnviarMensagemRequest("Respondendo pelo colega"));

        Assert.Equal(dono, conversa.OperadorResponsavelId); // posse fica onde estava
        Assert.Equal(0, conversa.NaoLidas);                 // respondida = lida
        Assert.Equal(0, await db.ConversaEventos.CountAsync(e => e.ConversaId == conversa.Id));
    }

    // ===================== MARCAR LIDA =====================

    [Fact]
    public async Task Marcar_lida_zera_o_badge_mas_nao_gera_claim()
    {
        await using var db = fixture.CriarDbContext();
        var (usuario, unidade) = await CriarOperadorAsync(db);
        var conversa = await CriarConversaAsync(db, unidadeId: unidade, naoLidas: 5);

        await CriarServico(db, usuario).MarcarLidaAsync(conversa.Id);

        Assert.Null(conversa.OperadorResponsavelId); // o claim explícito é AssumirAsync
        Assert.Equal(0, conversa.NaoLidas);
    }

    [Fact]
    public async Task Supervisor_marca_lida_de_conversa_de_terceiro_sem_roubar_a_posse()
    {
        await using var db = fixture.CriarDbContext();
        var (dono, unidade) = await CriarOperadorAsync(db);
        var (supervisor, _) = await CriarOperadorAsync(db); // de OUTRA unidade
        var conversa = await CriarConversaAsync(db, operadorId: dono, unidadeId: unidade, naoLidas: 1);

        await CriarServico(db, supervisor, ModuloPermissao.Conversas, ModuloPermissao.ConversasSupervisao)
            .MarcarLidaAsync(conversa.Id);

        Assert.Equal(dono, conversa.OperadorResponsavelId);
        Assert.Equal(0, conversa.NaoLidas);
    }

    // ===================== ESCOPO DE ACESSO POR ID =====================

    [Fact]
    public async Task Conversa_de_outra_unidade_e_LEGIVEL_por_qualquer_operador()
    {
        // ADR-0048: leitura destravada — ver o cabeçalho e as mensagens não depende mais de posse.
        await using var db = fixture.CriarDbContext();
        var (dono, unidadeDele) = await CriarOperadorAsync(db);
        var (intruso, _) = await CriarOperadorAsync(db); // vinculado só à própria unidade
        var conversa = await CriarConversaAsync(db, operadorId: dono, unidadeId: unidadeDele, naoLidas: 1, janelaAberta: true);

        var servico = CriarServico(db, intruso);
        var dto = await servico.ObterAsync(conversa.Id);
        Assert.Equal(conversa.Id, dto.Id);
        // Não lança (histórico legível a qualquer operador).
        await servico.ObterMensagensAsync(conversa.Id);
    }

    [Fact]
    public async Task Conversa_de_outra_unidade_ainda_e_404_para_AGIR()
    {
        // A trava de posse continua valendo para as AÇÕES: marcar lida / responder / assumir /
        // devolver / encaminhar / transferir de conversa fora do escopo → 404 (não vaza).
        await using var db = fixture.CriarDbContext();
        var (dono, unidadeDele) = await CriarOperadorAsync(db);
        var (intruso, _) = await CriarOperadorAsync(db); // vinculado só à própria unidade
        var conversa = await CriarConversaAsync(db, operadorId: dono, unidadeId: unidadeDele, naoLidas: 1, janelaAberta: true);

        var servico = CriarServico(db, intruso);
        await Assert.ThrowsAsync<NaoEncontradoException>(() => servico.MarcarLidaAsync(conversa.Id));
        await Assert.ThrowsAsync<NaoEncontradoException>(() =>
            servico.EnviarTextoAsync(conversa.Id, new ConversaDtos.EnviarMensagemRequest("oi")));
    }

    [Fact]
    public async Task Conversa_com_dono_continua_acessivel_ao_colega_da_mesma_unidade()
    {
        await using var db = fixture.CriarDbContext();
        var (dono, unidade) = await CriarOperadorAsync(db);
        var colega = await CriarUsuarioVinculadoAsync(db, unidade);
        var conversa = await CriarConversaAsync(db, operadorId: dono, unidadeId: unidade);

        var dto = await CriarServico(db, colega).ObterAsync(conversa.Id);

        Assert.Equal(conversa.Id, dto.Id);
        Assert.Equal(dono, dto.OperadorResponsavelId);
    }

    [Fact]
    public async Task Conversa_da_triagem_geral_e_acessivel_a_qualquer_operador()
    {
        await using var db = fixture.CriarDbContext();
        var (usuario, _) = await CriarOperadorAsync(db);
        var conversa = await CriarConversaAsync(db); // sem dono e sem unidade

        var dto = await CriarServico(db, usuario).ObterAsync(conversa.Id);

        Assert.Equal(conversa.Id, dto.Id);
    }

    // ===================== DEVOLVER =====================

    [Fact]
    public async Task Devolver_limpa_o_dono_mantem_a_unidade_e_grava_Devolvida()
    {
        await using var db = fixture.CriarDbContext();
        var (usuario, unidade) = await CriarOperadorAsync(db);
        var conversa = await CriarConversaAsync(db, operadorId: usuario, unidadeId: unidade, naoLidas: 2);

        await CriarServico(db, usuario).DevolverAsync(conversa.Id);

        Assert.Null(conversa.OperadorResponsavelId);
        Assert.Equal(unidade, conversa.UnidadeId);
        Assert.Equal(2, conversa.NaoLidas); // a pendência de leitura volta para a fila junto
        var evento = await UltimoEventoAsync(db, conversa.Id);
        Assert.Equal(TipoEventoConversa.Devolvida, evento.Tipo);
        Assert.Equal(usuario, evento.DeUsuarioId);
        Assert.Equal(usuario, evento.AtorUsuarioId);
    }

    [Fact]
    public async Task Devolver_conversa_sem_unidade_volta_ao_balde_geral()
    {
        await using var db = fixture.CriarDbContext();
        var usuario = await CriarUsuarioSemVinculoAsync(db);
        var conversa = await CriarConversaAsync(db, operadorId: usuario); // dono sem unidade (D13)

        await CriarServico(db, usuario).DevolverAsync(conversa.Id);

        Assert.Null(conversa.OperadorResponsavelId);
        Assert.Null(conversa.UnidadeId);
    }

    [Fact]
    public async Task Devolver_conversa_de_terceiro_exige_supervisao()
    {
        await using var db = fixture.CriarDbContext();
        var (dono, unidade) = await CriarOperadorAsync(db);
        var colega = await CriarUsuarioVinculadoAsync(db, unidade);
        var conversa = await CriarConversaAsync(db, operadorId: dono, unidadeId: unidade);

        await Assert.ThrowsAsync<ConflitoException>(() =>
            CriarServico(db, colega).DevolverAsync(conversa.Id));
        Assert.Equal(dono, conversa.OperadorResponsavelId);

        await CriarServico(db, colega, ModuloPermissao.Conversas, ModuloPermissao.ConversasSupervisao)
            .DevolverAsync(conversa.Id);
        Assert.Null(conversa.OperadorResponsavelId);
    }

    // ===================== ENCAMINHAR =====================

    [Fact]
    public async Task Encaminhar_muda_o_dono_e_grava_Transferida_com_De_Para_e_ator()
    {
        await using var db = fixture.CriarDbContext();
        var (usuario, unidade) = await CriarOperadorAsync(db);
        var colega = await CriarUsuarioVinculadoAsync(db, unidade);
        var conversa = await CriarConversaAsync(db, operadorId: usuario, unidadeId: unidade, naoLidas: 3);

        await CriarServico(db, usuario).EncaminharAsync(
            conversa.Id, new ConversaDtos.EncaminharConversaRequest(colega, "paciente do programa dele"));

        Assert.Equal(colega, conversa.OperadorResponsavelId);
        Assert.Equal(3, conversa.NaoLidas); // a pendência de leitura agora é do novo responsável
        var evento = await UltimoEventoAsync(db, conversa.Id);
        Assert.Equal(TipoEventoConversa.Transferida, evento.Tipo);
        Assert.Equal(usuario, evento.DeUsuarioId);
        Assert.Equal(colega, evento.ParaUsuarioId);
        Assert.Equal(usuario, evento.AtorUsuarioId);
        Assert.Equal("paciente do programa dele", evento.Observacao);
    }

    [Fact]
    public async Task Encaminhar_valida_alvo_inativo_sem_modulo_e_sem_vinculo()
    {
        await using var db = fixture.CriarDbContext();
        var (usuario, unidade) = await CriarOperadorAsync(db);
        var conversa = await CriarConversaAsync(db, operadorId: usuario, unidadeId: unidade);
        var servico = CriarServico(db, usuario);

        // Alvo inativo.
        var inativo = await CriarUsuarioVinculadoAsync(db, unidade);
        (await db.Usuarios.FirstAsync(u => u.Id == inativo)).Ativo = false;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<ValidacaoException>(() => servico.EncaminharAsync(
            conversa.Id, new ConversaDtos.EncaminharConversaRequest(inativo, null)));

        // Alvo sem o módulo Conversas.
        var semModulo = await CriarUsuarioVinculadoAsync(db, unidade);
        await Assert.ThrowsAsync<ValidacaoException>(() => CriarServicoComAlvoSemModulo(db, usuario, semModulo)
            .EncaminharAsync(conversa.Id, new ConversaDtos.EncaminharConversaRequest(semModulo, null)));

        // Alvo de outra unidade (sem vínculo com a unidade da conversa).
        var (deOutraUnidade, _) = await CriarOperadorAsync(db);
        await Assert.ThrowsAsync<ValidacaoException>(() => servico.EncaminharAsync(
            conversa.Id, new ConversaDtos.EncaminharConversaRequest(deOutraUnidade, null)));

        Assert.Equal(usuario, conversa.OperadorResponsavelId); // nada mudou
    }

    [Fact]
    public async Task Encaminhar_da_triagem_geral_leva_a_conversa_para_a_unidade_do_alvo()
    {
        await using var db = fixture.CriarDbContext();
        var (usuario, _) = await CriarOperadorAsync(db);
        var (alvo, unidadeDoAlvo) = await CriarOperadorAsync(db);
        var conversa = await CriarConversaAsync(db); // balde geral

        await CriarServico(db, usuario).EncaminharAsync(
            conversa.Id, new ConversaDtos.EncaminharConversaRequest(alvo, null));

        Assert.Equal(alvo, conversa.OperadorResponsavelId);
        Assert.Equal(unidadeDoAlvo, conversa.UnidadeId);
    }

    // ===================== TRANSFERIR PARA OUTRA UNIDADE =====================

    [Fact]
    public async Task Transferir_limpa_o_dono_muda_a_unidade_e_grava_EncaminhadaUnidade()
    {
        await using var db = fixture.CriarDbContext();
        var (usuario, unidadeOrigem) = await CriarOperadorAsync(db);
        var destino = await CriarUnidadeAsync(db);
        var conversa = await CriarConversaAsync(db, operadorId: usuario, unidadeId: unidadeOrigem, naoLidas: 4);

        await CriarServico(db, usuario).TransferirUnidadeAsync(
            conversa.Id, new ConversaDtos.TransferirConversaRequest(destino, "caso da área deles"));

        Assert.Null(conversa.OperadorResponsavelId); // entra na fila de lá, sem responsável
        Assert.Equal(destino, conversa.UnidadeId);
        Assert.Equal(4, conversa.NaoLidas);
        var evento = await UltimoEventoAsync(db, conversa.Id);
        Assert.Equal(TipoEventoConversa.EncaminhadaUnidade, evento.Tipo);
        Assert.Equal(unidadeOrigem, evento.DeUnidadeId);
        Assert.Equal(destino, evento.ParaUnidadeId);
        Assert.Equal(usuario, evento.DeUsuarioId);
    }

    [Fact]
    public async Task Transferir_recusa_unidade_inativa_externa_ou_a_propria()
    {
        await using var db = fixture.CriarDbContext();
        var (usuario, unidade) = await CriarOperadorAsync(db);
        var conversa = await CriarConversaAsync(db, operadorId: usuario, unidadeId: unidade);
        var servico = CriarServico(db, usuario);

        var inativa = await CriarUnidadeAsync(db, ativo: false);
        await Assert.ThrowsAsync<ValidacaoException>(() => servico.TransferirUnidadeAsync(
            conversa.Id, new ConversaDtos.TransferirConversaRequest(inativa, null)));

        var externa = await CriarUnidadeAsync(db, externa: true);
        await Assert.ThrowsAsync<ValidacaoException>(() => servico.TransferirUnidadeAsync(
            conversa.Id, new ConversaDtos.TransferirConversaRequest(externa, null)));

        await Assert.ThrowsAsync<ValidacaoException>(() => servico.TransferirUnidadeAsync(
            conversa.Id, new ConversaDtos.TransferirConversaRequest(unidade, null)));

        Assert.Equal(unidade, conversa.UnidadeId);
    }

    // ===================== CONCORRÊNCIA (token xmin) =====================

    [Fact]
    public async Task Token_xmin_detecta_escrita_concorrente_na_conversa()
    {
        await using var db1 = fixture.CriarDbContext();
        var (_, unidade) = await CriarOperadorAsync(db1);
        var conversa = await CriarConversaAsync(db1, unidadeId: unidade);

        await using var db2 = fixture.CriarDbContext();
        var mesmaLinha = await db2.Conversas.FirstAsync(c => c.Id == conversa.Id);

        conversa.NaoLidas = 1;
        await db1.SaveChangesAsync();

        // db2 carregou ANTES do save do db1: o token envelheceu e o save tem de falhar —
        // é o que fecha a janela da corrida de claim entre dois atendentes.
        mesmaLinha.NaoLidas = 2;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => db2.SaveChangesAsync());
    }

    // ===================== RESUMO (sino/badge) =====================

    [Fact]
    public async Task Resumo_espelha_os_predicados_das_filas()
    {
        await using var db = fixture.CriarDbContext();
        var (usuario, unidade) = await CriarOperadorAsync(db);
        var colega = await CriarUsuarioVinculadoAsync(db, unidade);
        var (outroUsuario, outraUnidade) = await CriarOperadorAsync(db);

        var servico = CriarServico(db, usuario);
        // O banco da bancada é compartilhado (triagem geral inclusive) — o teste mede o DELTA.
        var antes = await servico.ObterResumoAsync();

        await CriarConversaAsync(db, operadorId: usuario, unidadeId: unidade, naoLidas: 2);  // minha
        await CriarConversaAsync(db, operadorId: usuario, unidadeId: unidade, naoLidas: 3);  // minha
        await CriarConversaAsync(db, unidadeId: unidade, naoLidas: 5);                        // fila da minha unidade
        await CriarConversaAsync(db, naoLidas: 7);                                            // fila: triagem geral
        await CriarConversaAsync(db, unidadeId: outraUnidade, naoLidas: 11);                  // fila de OUTRA unidade
        await CriarConversaAsync(db, operadorId: colega, unidadeId: unidade, naoLidas: 13);   // do colega

        var depois = await servico.ObterResumoAsync();

        Assert.Equal(antes.MinhasNaoLidas + 5, depois.MinhasNaoLidas);
        Assert.Equal(antes.FilaNaoLidas + 12, depois.FilaNaoLidas); // 5 (unidade) + 7 (geral)

        // E para o dono da outra unidade: a fila dele ganhou o 11 e a triagem geral (7).
        var doOutro = await CriarServico(db, outroUsuario).ObterResumoAsync();
        Assert.Equal(0, doOutro.MinhasNaoLidas);
    }

    // ===================== HELPERS =====================

    private static ConversaService CriarServico(
        SmsMaisDbContext db, Guid usuarioId, params ModuloPermissao[] modulos)
    {
        if (modulos.Length == 0) modulos = [ModuloPermissao.Conversas];

        var identidade = Substitute.For<IIdentidadeService>();
        // Padrão: TODO usuário tem o módulo Conversas (o caso comum — alvos de encaminhamento
        // inclusive); o usuário do teste recebe os módulos pedidos.
        identidade.ObterPermissoesResolvidasAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Perms(ModuloPermissao.Conversas));
        identidade.ObterPermissoesResolvidasAsync(usuarioId, Arg.Any<CancellationToken>())
            .Returns(Perms(modulos));

        return CriarServico(db, usuarioId, identidade);
    }

    /// <summary>Variante em que o ALVO do encaminhamento não tem o módulo Conversas.</summary>
    private static ConversaService CriarServicoComAlvoSemModulo(
        SmsMaisDbContext db, Guid usuarioId, Guid alvoSemModulo)
    {
        var identidade = Substitute.For<IIdentidadeService>();
        identidade.ObterPermissoesResolvidasAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Perms(ModuloPermissao.Conversas));
        identidade.ObterPermissoesResolvidasAsync(alvoSemModulo, Arg.Any<CancellationToken>())
            .Returns(Perms());

        return CriarServico(db, usuarioId, identidade);
    }

    private static ConversaService CriarServico(
        SmsMaisDbContext db, Guid usuarioId, IIdentidadeService identidade)
    {
        var whats = Substitute.For<IWhatsAppCliente>();
        whats.EnviarTextoAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(new EnvioWhatsAppResultado(true, $"wamid.teste.{Guid.NewGuid():N}", null));

        // O hub FHIR não é o objeto destes testes: nome de paciente é decoração da thread.
        var resolver = Substitute.For<IPacienteResolver>();
        var accessor = new UsuarioAtualAccessorFake(usuarioId);

        return new ConversaService(
            db,
            whats,
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
        var unidade = await CriarUnidadeAsync(db);
        var usuario = await CriarUsuarioVinculadoAsync(db, unidade);
        return (usuario, unidade);
    }

    private static async Task<Guid> CriarUnidadeAsync(
        SmsMaisDbContext db, bool ativo = true, bool externa = false)
    {
        var u = new Unidade
        {
            Id = Guid.NewGuid(),
            Nome = $"UNID {Guid.NewGuid():N}"[..24],
            Ativo = ativo,
            Externa = externa,
            CriadoEm = DateTime.UtcNow,
        };
        db.Unidades.Add(u);
        await db.SaveChangesAsync();
        return u.Id;
    }

    private static async Task<Guid> CriarUsuarioSemVinculoAsync(SmsMaisDbContext db)
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
        await db.SaveChangesAsync();
        return u.Id;
    }

    private static async Task<Guid> CriarUsuarioVinculadoAsync(SmsMaisDbContext db, Guid unidadeId)
    {
        var usuario = await CriarUsuarioSemVinculoAsync(db);
        db.UsuarioUnidades.Add(new UsuarioUnidade
        {
            UsuarioId = usuario,
            UnidadeId = unidadeId,
            CriadoEm = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        return usuario;
    }

    private static async Task<Conversa> CriarConversaAsync(
        SmsMaisDbContext db,
        Guid? operadorId = null,
        Guid? unidadeId = null,
        int naoLidas = 0,
        bool janelaAberta = false)
    {
        var c = new Conversa
        {
            Id = Guid.CreateVersion7(),
            Canal = CanalConversa.WhatsApp,
            TelefoneCanonical = $"5521{Random.Shared.NextInt64(100_000_000, 999_999_999)}",
            Status = StatusConversa.Aberta,
            OperadorResponsavelId = operadorId,
            UnidadeId = unidadeId,
            NaoLidas = naoLidas,
            JanelaExpiraEm = janelaAberta ? DateTime.UtcNow.AddHours(23) : null,
            PrimeiroContatoEm = DateTime.UtcNow,
            CriadoEm = DateTime.UtcNow,
        };
        db.Conversas.Add(c);
        await db.SaveChangesAsync();
        return c;
    }

    private static async Task<ConversaEvento> UltimoEventoAsync(SmsMaisDbContext db, Guid conversaId) =>
        await db.ConversaEventos.AsNoTracking()
            .Where(e => e.ConversaId == conversaId)
            .OrderByDescending(e => e.OcorridoEm)
            .FirstAsync();
}
