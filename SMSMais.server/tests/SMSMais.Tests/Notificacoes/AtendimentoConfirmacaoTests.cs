using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Notificacoes.Comunicacao;
using SMSMais.Core.Notificacoes.Confirmacoes;
using SMSMais.Core.Notificacoes.Confirmacoes.Dtos;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Core.PendenciasCadastro;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Notificacoes;

/// <summary>
/// Atendimento humano das confirmações (menu Confirmações, 4 abas). Invariantes protegidas:
/// (1) quando uma pessoa entra no circuito, o envio automático que ainda não saiu vira terminal
/// (<see cref="StatusComunicacao.SubstituidaPorAtendente"/>); (2) a posse é trava: atender o que
/// está com outra pessoa é 409, e "Assumir" é explícito; (3) confirmar/cancelar pela mão gravam o
/// canal "atendente" na solicitação; (4) cancelar é LOCAL (fase 1) e devolve a orientação de
/// cancelar no SISREG; (5) as abas são derivadas — pendente e cancelado somem de "Não confirmados".
/// </summary>
[Collection(nameof(PostgresCollection))]
public class AtendimentoConfirmacaoTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Atender_encerra_envio_automatico_pendente_e_marca_como_meu()
    {
        await using var db = fixture.CriarDbContext();
        var atendente = await CriarUsuarioAsync(db);
        var exame = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid(), dataAgendada: DateTime.UtcNow.AddDays(2));
        var comunicacao = await CriarComunicacaoAsync(db, exame.Solicitacao!, StatusComunicacao.Pendente, DateTime.UtcNow);

        var servico = CriarServico(db, atendente);
        var resultado = await servico.AtenderAsync(exame.SolicitacaoId);

        Assert.Equal(nameof(SituacaoAtendimentoConfirmacao.EmAtendimento), resultado.Situacao);

        await using var db2 = fixture.CriarDbContext();
        var c = await db2.ComunicacoesPaciente.SingleAsync(x => x.Id == comunicacao.Id);
        Assert.Equal(StatusComunicacao.SubstituidaPorAtendente, c.Status);
        Assert.Null(c.ProximaTentativaEm);

        var pagina = await CriarServico(db2, atendente).ListarAsync(
            AbaAtendimentoConfirmacao.NaoConfirmados, null, exame.Solicitacao!.UnidadeExecutanteId, null, 1, 200);
        var card = Assert.Single(pagina.Itens, i => i.SolicitacaoId == exame.SolicitacaoId);
        Assert.NotNull(card.Atendimento);
        Assert.True(card.Atendimento!.EhMeu);
        Assert.Equal(nameof(StatusComunicacao.SubstituidaPorAtendente), card.Envio!.Status);

        var evento = await db2.AtendimentoConfirmacaoEventos
            .Where(e => e.Atendimento!.SolicitacaoId == exame.SolicitacaoId)
            .SingleAsync();
        Assert.Equal(TipoEventoAtendimentoConfirmacao.Atendido, evento.Tipo);
        Assert.Equal(atendente, evento.AtorUsuarioId);
    }

    [Fact]
    public async Task Atender_o_que_esta_com_outra_pessoa_da_409_e_Assumir_troca_a_posse()
    {
        await using var db = fixture.CriarDbContext();
        var ana = await CriarUsuarioAsync(db);
        var bia = await CriarUsuarioAsync(db);
        var exame = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid(), dataAgendada: DateTime.UtcNow.AddDays(3));

        await CriarServico(db, ana).AtenderAsync(exame.SolicitacaoId);

        await using var db2 = fixture.CriarDbContext();
        var ex = await Assert.ThrowsAsync<ConflitoException>(() =>
            CriarServico(db2, bia).AtenderAsync(exame.SolicitacaoId));
        Assert.Equal("atendimento.ja_atendido", ex.Codigo);

        await using var db3 = fixture.CriarDbContext();
        var resultado = await CriarServico(db3, bia).AssumirAsync(exame.SolicitacaoId);
        Assert.Equal(nameof(SituacaoAtendimentoConfirmacao.EmAtendimento), resultado.Situacao);

        await using var db4 = fixture.CriarDbContext();
        var ativo = await db4.AtendimentosConfirmacao.SingleAsync(a => a.SolicitacaoId == exame.SolicitacaoId && a.EncerradoEm == null);
        Assert.Equal(bia, ativo.AtendenteUsuarioId);
        var assumido = await db4.AtendimentoConfirmacaoEventos
            .Where(e => e.AtendimentoId == ativo.Id && e.Tipo == TipoEventoAtendimentoConfirmacao.Assumido)
            .SingleAsync();
        Assert.Equal(ana, assumido.DeUsuarioId);
        Assert.Equal(bia, assumido.ParaUsuarioId);
    }

    [Fact]
    public async Task Confirmar_grava_canal_atendente_e_move_para_Confirmados()
    {
        await using var db = fixture.CriarDbContext();
        var atendente = await CriarUsuarioAsync(db);
        var exame = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid(), dataAgendada: DateTime.UtcNow.AddDays(1));

        var resultado = await CriarServico(db, atendente).ConfirmarAsync(
            exame.SolicitacaoId, new ConfirmarAtendimentoRequest("Ligacao", "Confirmou por telefone"));
        Assert.Equal(nameof(SituacaoAtendimentoConfirmacao.Confirmado), resultado.Situacao);

        await using var db2 = fixture.CriarDbContext();
        var s = await db2.Solicitacoes.SingleAsync(x => x.Id == exame.SolicitacaoId);
        Assert.Equal(StatusConfirmacaoAgendamento.Confirmada, s.StatusConfirmacao);
        Assert.Equal(AtendimentoConfirmacaoService.CanalAtendente, s.ConfirmadoCanal);
        Assert.NotNull(s.ConfirmadoEm);

        var contato = await db2.ContatosRegistro.SingleAsync(c => c.SolicitacaoId == s.Id);
        Assert.Equal(MeioContato.Ligacao, contato.Meio);
        Assert.Equal(ResultadoContato.Atendeu, contato.Resultado);

        // Encerrado: nenhuma linha ativa; aparece em Confirmados e não em Não confirmados.
        Assert.False(await db2.AtendimentosConfirmacao.AnyAsync(a => a.SolicitacaoId == s.Id && a.EncerradoEm == null));
        var servico = CriarServico(db2, atendente);
        Assert.Contains((await servico.ListarAsync(AbaAtendimentoConfirmacao.Confirmados, null, exame.Solicitacao!.UnidadeExecutanteId, null, 1, 200)).Itens,
            i => i.SolicitacaoId == s.Id);
        Assert.DoesNotContain((await servico.ListarAsync(AbaAtendimentoConfirmacao.NaoConfirmados, null, exame.Solicitacao!.UnidadeExecutanteId, null, 1, 200)).Itens,
            i => i.SolicitacaoId == s.Id);
    }

    [Fact]
    public async Task Cancelar_e_local_libera_a_vaga_e_orienta_o_SISREG()
    {
        await using var db = fixture.CriarDbContext();
        var atendente = await CriarUsuarioAsync(db);
        var exame = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid(), dataAgendada: DateTime.UtcNow.AddDays(5));
        var comunicacao = await CriarComunicacaoAsync(db, exame.Solicitacao!, StatusComunicacao.Pendente, DateTime.UtcNow.AddMinutes(10));

        var comunicacoes = Substitute.For<IComunicacaoPacienteService>();
        comunicacoes.RevogarAcessosAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<CidadaoLoginLink>());

        var resultado = await CriarServico(db, atendente, comunicacoes).CancelarAsync(
            exame.SolicitacaoId, new CancelarAtendimentoRequest("Paciente mudou de cidade", "WhatsApp"));

        Assert.Equal(nameof(SituacaoAtendimentoConfirmacao.Cancelado), resultado.Situacao);
        Assert.True(resultado.OrientacaoSisreg);
        await comunicacoes.Received(1).RevogarAcessosAsync(exame.SolicitacaoId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>());

        await using var db2 = fixture.CriarDbContext();
        var s = await db2.Solicitacoes.Include(x => x.ExameImagem).SingleAsync(x => x.Id == exame.SolicitacaoId);
        Assert.Equal(StatusSolicitacao.Cancelada, s.Status);
        Assert.NotNull(s.CanceladoEm); // é o CanceladoEm que devolve a vaga (derivação em OfertasSisregService)
        Assert.Equal(atendente, s.CanceladoPorUsuarioId);
        Assert.Equal("Paciente mudou de cidade", s.MotivoCancelamento);
        Assert.Equal(StatusConfirmacaoAgendamento.Cancelada, s.StatusConfirmacao);
        Assert.Equal(AtendimentoConfirmacaoService.CanalAtendente, s.ConfirmadoCanal);
        Assert.Equal(StatusSolicitacaoExame.Cancelada, s.ExameImagem!.Status);

        var c = await db2.ComunicacoesPaciente.SingleAsync(x => x.Id == comunicacao.Id);
        Assert.Equal(StatusComunicacao.SubstituidaPorAtendente, c.Status);

        var servico = CriarServico(db2, atendente);
        foreach (var aba in Enum.GetValues<AbaAtendimentoConfirmacao>())
            Assert.DoesNotContain((await servico.ListarAsync(aba, null, exame.Solicitacao!.UnidadeExecutanteId, null, 1, 200)).Itens, i => i.SolicitacaoId == s.Id);
    }

    [Fact]
    public async Task Cancelar_sem_motivo_e_recusado()
    {
        await using var db = fixture.CriarDbContext();
        var atendente = await CriarUsuarioAsync(db);
        var exame = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid(), dataAgendada: DateTime.UtcNow.AddDays(5));

        var ex = await Assert.ThrowsAsync<ValidacaoException>(() =>
            CriarServico(db, atendente).CancelarAsync(exame.SolicitacaoId, new CancelarAtendimentoRequest("  ", null)));
        Assert.Contains("atendimento.motivo_obrigatorio", ex.Erros.Keys);
    }

    [Fact]
    public async Task Pendente_sai_de_NaoConfirmados_entra_em_Pendentes_e_Atender_retoma()
    {
        await using var db = fixture.CriarDbContext();
        var ana = await CriarUsuarioAsync(db);
        var bia = await CriarUsuarioAsync(db);
        var exame = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid(), dataAgendada: DateTime.UtcNow.AddDays(4));

        await CriarServico(db, ana).EnviarParaPendenteAsync(
            exame.SolicitacaoId, new PendenteAtendimentoRequest("Não atende, tentar à tarde"));

        await using var db2 = fixture.CriarDbContext();
        var servico = CriarServico(db2, ana);
        Assert.DoesNotContain((await servico.ListarAsync(AbaAtendimentoConfirmacao.NaoConfirmados, null, exame.Solicitacao!.UnidadeExecutanteId, null, 1, 200)).Itens,
            i => i.SolicitacaoId == exame.SolicitacaoId);
        var card = Assert.Single((await servico.ListarAsync(AbaAtendimentoConfirmacao.Pendentes, null, exame.Solicitacao!.UnidadeExecutanteId, null, 1, 200)).Itens,
            i => i.SolicitacaoId == exame.SolicitacaoId);
        Assert.Equal(nameof(SituacaoAtendimentoConfirmacao.Pendente), card.Atendimento!.Situacao);
        Assert.Equal("Não atende, tentar à tarde", card.Atendimento.Motivo);

        // Outra atendente retoma pelo Atender (estacionada não é posse travada).
        await using var db3 = fixture.CriarDbContext();
        var resultado = await CriarServico(db3, bia).AtenderAsync(exame.SolicitacaoId);
        Assert.Equal(nameof(SituacaoAtendimentoConfirmacao.EmAtendimento), resultado.Situacao);

        await using var db4 = fixture.CriarDbContext();
        var ativo = await db4.AtendimentosConfirmacao.SingleAsync(a => a.SolicitacaoId == exame.SolicitacaoId && a.EncerradoEm == null);
        Assert.Equal(bia, ativo.AtendenteUsuarioId);
        Assert.True(await db4.AtendimentoConfirmacaoEventos.AnyAsync(e =>
            e.AtendimentoId == ativo.Id && e.Tipo == TipoEventoAtendimentoConfirmacao.Retomado && e.DeUsuarioId == ana && e.ParaUsuarioId == bia));
    }

    [Fact]
    public async Task ContatoErrado_abre_pendencia_de_numero_errado_e_vai_para_a_aba_ContatoErrado()
    {
        await using var db = fixture.CriarDbContext();
        var atendente = await CriarUsuarioAsync(db);
        var pacienteId = Guid.NewGuid();
        var exame = await SeedSolicitacao.CriarAsync(db, pacienteId, dataAgendada: DateTime.UtcNow.AddDays(2));
        await CriarComunicacaoAsync(db, exame.Solicitacao!, StatusComunicacao.Enviada, null, telefone: "5521999990000");

        var pendencias = Substitute.For<IPendenciaCadastroService>();
        await CriarServico(db, atendente, pendencias: pendencias).ContatoErradoAsync(
            exame.SolicitacaoId, new ContatoErradoAtendimentoRequest("Atendeu um senhor que não conhece a paciente"));

        await pendencias.Received(1).RegistrarNumeroErradoAsync(
            null, "5521999990000", pacienteId, VinculoContato.NaoInformado, Arg.Any<string?>(), atendente, Arg.Any<CancellationToken>());

        await using var db2 = fixture.CriarDbContext();
        var servico = CriarServico(db2, atendente);
        Assert.Contains((await servico.ListarAsync(AbaAtendimentoConfirmacao.ContatoErrado, null, exame.Solicitacao!.UnidadeExecutanteId, null, 1, 200)).Itens,
            i => i.SolicitacaoId == exame.SolicitacaoId);
        Assert.DoesNotContain((await servico.ListarAsync(AbaAtendimentoConfirmacao.NaoConfirmados, null, exame.Solicitacao!.UnidadeExecutanteId, null, 1, 200)).Itens,
            i => i.SolicitacaoId == exame.SolicitacaoId);
    }

    // ===================== apoio =====================

    private static AtendimentoConfirmacaoService CriarServico(
        SmsMaisDbContext db, Guid usuarioId,
        IComunicacaoPacienteService? comunicacoes = null,
        IPendenciaCadastroService? pendencias = null)
    {
        var configuracao = Substitute.For<IConfirmacaoConfiguracaoService>();
        configuracao.ObterAsync(Arg.Any<CancellationToken>())
            .Returns(new ConfirmacaoConfiguracaoDto("08:00", "18:00", 100, SomenteSisreg: false, true, null));

        var resolver = Substitute.For<IPacienteResolver>();
        resolver.ResolverManyAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, PacienteResumo>());
        resolver.ResolverAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((PacienteResumo?)null);

        comunicacoes ??= Substitute.For<IComunicacaoPacienteService>();
        comunicacoes.RevogarAcessosAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<CidadaoLoginLink>());

        // Cancelamento no SISREG: dublê que diz "falhou". Nos testes não há SISREG, e o ponto do
        // desenho é justamente este — falhar lá NÃO desfaz o cancelamento local.
        var sisreg = Substitute.For<SMSMais.Core.Integracoes.SisregWeb.Cancelamento.ICancelamentoSisregService>();
        sisreg.CancelarAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new SMSMais.Core.Integracoes.SisregWeb.Cancelamento.CancelamentoSisregDto(
                SMSMais.Core.Integracoes.SisregWeb.Cancelamento.ResultadoCancelamentoSisreg.Falhou,
                null, null, "sem SISREG no teste"));

        return new AtendimentoConfirmacaoService(
            db, new UsuarioAtualAccessorFake(usuarioId), resolver, configuracao, comunicacoes,
            pendencias ?? Substitute.For<IPendenciaCadastroService>(), sisreg,
            NullLogger<AtendimentoConfirmacaoService>.Instance);
    }

    private static async Task<Guid> CriarUsuarioAsync(SmsMaisDbContext db)
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

    private static async Task<ComunicacaoPaciente> CriarComunicacaoAsync(
        SmsMaisDbContext db, Solicitacao s, StatusComunicacao status, DateTime? proximaTentativa, string? telefone = null)
    {
        var c = new ComunicacaoPaciente
        {
            Id = Guid.CreateVersion7(),
            Tipo = TipoAgendamento.Exame,
            Finalidade = FinalidadeComunicacao.ConfirmacaoAgendamento,
            SolicitacaoId = s.Id,
            PacienteId = s.PacienteId,
            Telefone = telefone,
            Status = status,
            ProximaTentativaEm = proximaTentativa,
            EnviadoEm = status == StatusComunicacao.Enviada ? DateTime.UtcNow : null,
            CriadoEm = DateTime.UtcNow,
        };
        db.ComunicacoesPaciente.Add(c);
        await db.SaveChangesAsync();
        return c;
    }
}
