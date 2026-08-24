using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SMSMarica.Core.Identidade;
using SMSMarica.Core.Identidade.Dtos;
using SMSMarica.Core.PainelInicio;
using SMSMarica.Core.PainelInicio.Dtos;
using SMSMarica.Core.Pacientes.Fhir;
using SMSMarica.Core.SolicitacoesExame.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Conversas;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Sisreg;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.PainelInicio;

/// <summary>
/// O painel da tela de início (ADR-0033/0034/0035). O teste mais importante daqui é
/// <see cref="Quando_a_equipe_cancela_a_solicitacao_a_raia_se_esvazia"/>: é a invariante que
/// substitui a tabela de "marcar como visto" que deliberadamente não existe.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class PainelInicioTests(PostgresFixture fixture)
{
    // ===================== RAIA: CANCELADOS =====================

    [Fact]
    public async Task Cancelado_pelo_paciente_entra_na_raia_com_o_motivo_literal()
    {
        await using var db = fixture.CriarDbContext();
        var (usuario, unidade) = await CriarOperadorAsync(db);
        await CriarSolicitacaoAsync(db, unidade,
            confirmacao: StatusConfirmacaoAgendamento.Cancelada,
            motivoPaciente: "estou viajando");

        var painel = await CriarServico(db, usuario, unidade).ObterAsync(
            LenteEscopoPainel.Unidade, DirecaoPainel.Tudo);

        Assert.NotNull(painel.Cancelados);
        Assert.Equal(1, painel.Cancelados!.Total);
        Assert.Equal("estou viajando", painel.Cancelados.Itens[0].MotivoCancelamento);
    }

    /// <summary>
    /// A mecânica de auto-limpeza: não há botão de "visto" — o que tira a linha da raia é a EQUIPE
    /// agir. Se este teste quebrar, o painel virou uma lista que só cresce.
    /// </summary>
    [Fact]
    public async Task Quando_a_equipe_cancela_a_solicitacao_a_raia_se_esvazia()
    {
        await using var db = fixture.CriarDbContext();
        var (usuario, unidade) = await CriarOperadorAsync(db);
        var solicitacao = await CriarSolicitacaoAsync(db, unidade,
            confirmacao: StatusConfirmacaoAgendamento.Cancelada);

        var servico = CriarServico(db, usuario, unidade);
        Assert.Equal(1, (await servico.ObterAsync(LenteEscopoPainel.Unidade, DirecaoPainel.Tudo)).Cancelados!.Total);

        // A equipe age: cancela de verdade.
        solicitacao.Status = StatusSolicitacao.Cancelada;
        await db.SaveChangesAsync();

        Assert.Equal(0, (await servico.ObterAsync(LenteEscopoPainel.Unidade, DirecaoPainel.Tudo)).Cancelados!.Total);
    }

    /// <summary>
    /// Cancelado com data no passado CONTINUA na raia. Filtrar por futuro esconderia exatamente o
    /// caso esquecido — a vaga que morreu sem ninguém fechar o registro (ADR-0034).
    /// </summary>
    [Fact]
    public async Task Cancelado_com_data_no_passado_continua_na_raia()
    {
        await using var db = fixture.CriarDbContext();
        var (usuario, unidade) = await CriarOperadorAsync(db);
        await CriarSolicitacaoAsync(db, unidade,
            confirmacao: StatusConfirmacaoAgendamento.Cancelada,
            dataAgendada: DateTime.UtcNow.AddDays(-30));

        var painel = await CriarServico(db, usuario, unidade).ObterAsync(
            LenteEscopoPainel.Unidade, DirecaoPainel.Tudo);

        Assert.Equal(1, painel.Cancelados!.Total);
    }

    // ===================== DIREÇÃO =====================

    [Fact]
    public async Task Executante_e_solicitante_veem_a_mesma_linha_com_direcao_invertida()
    {
        await using var db = fixture.CriarDbContext();
        var (usuarioExec, executante) = await CriarOperadorAsync(db);
        var (usuarioSolic, solicitante) = await CriarOperadorAsync(db);
        await CriarSolicitacaoAsync(db, executante,
            unidadeSolicitanteId: solicitante,
            confirmacao: StatusConfirmacaoAgendamento.Cancelada);

        var visaoExec = await CriarServico(db, usuarioExec, executante)
            .ObterAsync(LenteEscopoPainel.Unidade, DirecaoPainel.Tudo);
        var visaoSolic = await CriarServico(db, usuarioSolic, solicitante)
            .ObterAsync(LenteEscopoPainel.Unidade, DirecaoPainel.Tudo);

        Assert.Equal(DirecaoSolicitacao.Recebida, visaoExec.Cancelados!.Itens[0].Direcao);
        Assert.Equal(DirecaoSolicitacao.Enviada, visaoSolic.Cancelados!.Itens[0].Direcao);
    }

    [Fact]
    public async Task Filtro_como_solicitante_nao_traz_o_que_a_unidade_executa()
    {
        await using var db = fixture.CriarDbContext();
        var (usuario, unidade) = await CriarOperadorAsync(db);
        await CriarSolicitacaoAsync(db, unidade, confirmacao: StatusConfirmacaoAgendamento.Cancelada);

        var servico = CriarServico(db, usuario, unidade);

        Assert.Equal(1, (await servico.ObterAsync(LenteEscopoPainel.Unidade, DirecaoPainel.Executante)).Cancelados!.Total);
        Assert.Equal(0, (await servico.ObterAsync(LenteEscopoPainel.Unidade, DirecaoPainel.Solicitante)).Cancelados!.Total);
    }

    // ===================== PERMISSÃO E LENTE =====================

    /// <summary>Raia sem permissão volta NULL, não vazia: "não tenho acesso" e "não tem nada" não
    /// podem virar o mesmo pixel (ADR-0033 §6).</summary>
    [Fact]
    public async Task Sem_permissao_de_sisreg_a_raia_de_pendencias_volta_null()
    {
        await using var db = fixture.CriarDbContext();
        var (usuario, unidade) = await CriarOperadorAsync(db);
        await CriarPendenciaAsync(db, unidade);

        var painel = await CriarServico(db, usuario, unidade, ModuloPermissao.SolicitacoesExame)
            .ObterAsync(LenteEscopoPainel.Unidade, DirecaoPainel.Tudo);

        Assert.Null(painel.PendenciasImportacao);
        Assert.NotNull(painel.Cancelados);
    }

    [Fact]
    public async Task Sem_permissao_de_solicitacoes_as_raias_de_solicitacao_voltam_null()
    {
        await using var db = fixture.CriarDbContext();
        var (usuario, unidade) = await CriarOperadorAsync(db);

        var painel = await CriarServico(db, usuario, unidade, ModuloPermissao.Sisreg)
            .ObterAsync(LenteEscopoPainel.Unidade, DirecaoPainel.Tudo);

        Assert.Null(painel.Cancelados);
        Assert.Null(painel.Aguardando);
        Assert.Null(painel.Confirmados);
        Assert.NotNull(painel.PendenciasImportacao);
    }

    /// <summary>403 e não degradação silenciosa: degradar em silêncio faria o operador achar que o
    /// município está vazio, em vez de descobrir que não tem o direito.</summary>
    [Fact]
    public async Task Lente_municipio_sem_visao_global_e_negada()
    {
        await using var db = fixture.CriarDbContext();
        var (usuario, unidade) = await CriarOperadorAsync(db);
        var servico = CriarServico(db, usuario, unidade);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => servico.ObterAsync(LenteEscopoPainel.Municipio, DirecaoPainel.Tudo));

        var painel = await servico.ObterAsync(LenteEscopoPainel.Unidade, DirecaoPainel.Tudo);
        Assert.False(painel.PodeAlternarLente);
    }

    /// <summary>É assim que a REGULAÇÃO enxerga: sem escopo de unidade, por permissão — sem unidade
    /// fantasma, sem coluna nova (ADR-0033 §5).</summary>
    [Fact]
    public async Task Regulacao_com_visao_global_ve_o_municipio_inteiro()
    {
        await using var db = fixture.CriarDbContext();
        var (_, unidadeA) = await CriarOperadorAsync(db);
        var (_, unidadeB) = await CriarOperadorAsync(db);
        await CriarSolicitacaoAsync(db, unidadeA, confirmacao: StatusConfirmacaoAgendamento.Cancelada);
        await CriarSolicitacaoAsync(db, unidadeB, confirmacao: StatusConfirmacaoAgendamento.Cancelada);

        var regulador = await CriarUsuarioAsync(db);
        var servico = CriarServico(db, regulador, unidadeAtiva: null,
            ModuloPermissao.SolicitacoesExame, ModuloPermissao.Sisreg, ModuloPermissao.RegulacaoTriagem);

        var painel = await servico.ObterAsync(LenteEscopoPainel.Municipio, DirecaoPainel.Tudo);

        Assert.True(painel.PodeAlternarLente);
        Assert.True(painel.Cancelados!.Total >= 2);
        // Sem unidade de referência não há seta — no lugar dela, o nome da unidade.
        Assert.All(painel.Cancelados.Itens, i => Assert.Null(i.Direcao));
        Assert.All(painel.Cancelados.Itens, i => Assert.NotNull(i.UnidadeNome));
    }

    // ===================== AGUARDANDO / CONFIRMADOS =====================

    [Fact]
    public async Task Aguardando_traz_so_o_que_esta_dentro_da_janela()
    {
        await using var db = fixture.CriarDbContext();
        var (usuario, unidade) = await CriarOperadorAsync(db);
        await CriarSolicitacaoAsync(db, unidade,
            confirmacao: StatusConfirmacaoAgendamento.Pendente, dataAgendada: DateTime.UtcNow.AddDays(2));
        await CriarSolicitacaoAsync(db, unidade,
            confirmacao: StatusConfirmacaoAgendamento.Pendente, dataAgendada: DateTime.UtcNow.AddDays(10));

        var painel = await CriarServico(db, usuario, unidade).ObterAsync(
            LenteEscopoPainel.Unidade, DirecaoPainel.Tudo);

        Assert.Equal(1, painel.Aguardando!.Total);
    }

    [Fact]
    public async Task Confirmados_separa_recebidos_de_enviados()
    {
        await using var db = fixture.CriarDbContext();
        var (usuario, minha) = await CriarOperadorAsync(db);
        var outra = await CriarUnidadeAsync(db);

        // Recebida: eu executo.
        await CriarSolicitacaoAsync(db, minha, unidadeSolicitanteId: outra,
            confirmacao: StatusConfirmacaoAgendamento.Confirmada, dataAgendada: DateTime.UtcNow.AddDays(1));
        // Enviada: eu pedi, outra executa.
        await CriarSolicitacaoAsync(db, outra, unidadeSolicitanteId: minha,
            confirmacao: StatusConfirmacaoAgendamento.Confirmada, dataAgendada: DateTime.UtcNow.AddDays(1));

        var painel = await CriarServico(db, usuario, minha).ObterAsync(
            LenteEscopoPainel.Unidade, DirecaoPainel.Tudo);

        Assert.Equal(1, painel.Confirmados!.Recebidos);
        Assert.Equal(1, painel.Confirmados.Enviados);
    }

    // ===================== PENDÊNCIAS =====================

    [Fact]
    public async Task Pendencia_por_falta_de_cpf_e_marcada_como_acionavel()
    {
        await using var db = fixture.CriarDbContext();
        var (usuario, unidade) = await CriarOperadorAsync(db);
        await CriarPendenciaAsync(db, unidade, CausaFalhaImportacao.CpfNaoResolvido);
        await CriarPendenciaAsync(db, unidade, CausaFalhaImportacao.CadsusIndisponivel);

        var painel = await CriarServico(db, usuario, unidade).ObterAsync(
            LenteEscopoPainel.Unidade, DirecaoPainel.Tudo);

        Assert.Equal(2, painel.PendenciasImportacao!.Total);
        Assert.Single(painel.PendenciasImportacao.Itens, i => i.PodeInformarCpf);
    }

    [Fact]
    public async Task Pendencia_resolvida_sai_da_raia()
    {
        await using var db = fixture.CriarDbContext();
        var (usuario, unidade) = await CriarOperadorAsync(db);
        var falha = await CriarPendenciaAsync(db, unidade, CausaFalhaImportacao.CpfNaoResolvido);

        var servico = CriarServico(db, usuario, unidade);
        Assert.Equal(1, (await servico.ObterAsync(LenteEscopoPainel.Unidade, DirecaoPainel.Tudo)).PendenciasImportacao!.Total);

        falha.ResolvidoEm = DateTime.UtcNow;
        await db.SaveChangesAsync();

        Assert.Equal(0, (await servico.ObterAsync(LenteEscopoPainel.Unidade, DirecaoPainel.Tudo)).PendenciasImportacao!.Total);
    }

    /// <summary>O número do badge do menu — sem ele o painel é invisível para quem tem menu
    /// favorito, que é redirecionado e nunca vê a home (ADR-0033 §8).</summary>
    [Fact]
    public async Task Total_critico_soma_cancelados_e_pendencias()
    {
        await using var db = fixture.CriarDbContext();
        var (usuario, unidade) = await CriarOperadorAsync(db);
        await CriarSolicitacaoAsync(db, unidade, confirmacao: StatusConfirmacaoAgendamento.Cancelada);
        await CriarPendenciaAsync(db, unidade);
        // Aguardando NÃO é crítico: é palpite, não fato consumado.
        await CriarSolicitacaoAsync(db, unidade,
            confirmacao: StatusConfirmacaoAgendamento.Pendente, dataAgendada: DateTime.UtcNow.AddDays(1));

        var painel = await CriarServico(db, usuario, unidade).ObterAsync(
            LenteEscopoPainel.Unidade, DirecaoPainel.Tudo);

        Assert.Equal(2, painel.TotalCritico);
    }

    // ===================== apoio =====================

    private static PainelInicioService CriarServico(
        SmsMaisDbContext db, Guid usuarioId, Guid? unidadeAtiva, params ModuloPermissao[] modulos)
    {
        if (modulos.Length == 0) modulos = [ModuloPermissao.SolicitacoesExame, ModuloPermissao.Sisreg];

        var identidade = Substitute.For<IIdentidadeService>();
        var resolvidas = modulos.Select(m => new PermissaoModuloDto(m, AcoesPermissao.Todas)).ToList();
        identidade.ObterPermissoesResolvidasAsync(usuarioId, Arg.Any<CancellationToken>())
            .Returns(new PermissoesResolvidasDto(resolvidas, [], resolvidas));

        // O hub FHIR não é o objeto destes testes: o nome do paciente é decoração da linha.
        var resolver = Substitute.For<IPacienteResolver>();
        resolver.ResolverManyAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, PacienteResumo>());

        return new PainelInicioService(
            db,
            new UsuarioAtualAccessorFake(usuarioId, unidadeAtiva),
            identidade,
            resolver,
            NullLogger<PainelInicioService>.Instance);
    }

    private static async Task<(Guid Usuario, Guid Unidade)> CriarOperadorAsync(SmsMaisDbContext db)
    {
        var unidade = await CriarUnidadeAsync(db);
        var usuario = await CriarUsuarioAsync(db);
        db.UsuarioUnidades.Add(new UsuarioUnidade
        {
            UsuarioId = usuario,
            UnidadeId = unidade,
            CriadoEm = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        return (usuario, unidade);
    }

    private static async Task<Guid> CriarUnidadeAsync(SmsMaisDbContext db)
    {
        var u = new Unidade
        {
            Id = Guid.NewGuid(),
            Nome = $"UNID {Guid.NewGuid():N}"[..24],
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
        };
        db.Unidades.Add(u);
        await db.SaveChangesAsync();
        return u.Id;
    }

    private static async Task<Guid> CriarUsuarioAsync(SmsMaisDbContext db)
    {
        var u = new Usuario
        {
            Id = Guid.NewGuid(),
            NomeCompleto = "OPERADOR TESTE",
            Email = $"{Guid.NewGuid():N}@teste.local",
            SenhaHash = "x",
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
        };
        db.Usuarios.Add(u);
        await db.SaveChangesAsync();
        return u.Id;
    }

    private static async Task<Solicitacao> CriarSolicitacaoAsync(
        SmsMaisDbContext db,
        Guid unidadeExecutanteId,
        Guid? unidadeSolicitanteId = null,
        StatusConfirmacaoAgendamento confirmacao = StatusConfirmacaoAgendamento.Pendente,
        DateTime? dataAgendada = null,
        string? motivoPaciente = null)
    {
        var s = new Solicitacao
        {
            Id = Guid.NewGuid(),
            PacienteId = Guid.NewGuid(),
            Categoria = CategoriaSolicitacao.Imagem,
            ProcedimentoTexto = "MAMOGRAFIA BILATERAL",
            UnidadeExecutanteId = unidadeExecutanteId,
            UnidadeSolicitanteId = unidadeSolicitanteId,
            SolicitanteNome = "DR TESTE",
            Status = StatusSolicitacao.Solicitada,
            StatusConfirmacao = confirmacao,
            Prioridade = PrioridadeSolicitacao.Eletiva,
            DataAgendada = dataAgendada ?? DateTime.UtcNow.AddDays(1),
            ConfirmacaoCanceladaEm = confirmacao == StatusConfirmacaoAgendamento.Cancelada ? DateTime.UtcNow : null,
            MotivoCancelamentoPaciente = motivoPaciente,
            ConfirmadoCanal = confirmacao == StatusConfirmacaoAgendamento.Pendente ? null : "whatsapp-quickreply",
            CriadoEm = DateTime.UtcNow,
        };
        db.Solicitacoes.Add(s);
        await db.SaveChangesAsync();
        return s;
    }

    private static async Task<SisregImportacaoFalha> CriarPendenciaAsync(
        SmsMaisDbContext db, Guid unidadeId, CausaFalhaImportacao causa = CausaFalhaImportacao.CpfNaoResolvido)
    {
        var f = new SisregImportacaoFalha
        {
            Id = Guid.NewGuid(),
            CodigoSolicitacao = Random.Shared.NextInt64(100_000, 999_999).ToString(),
            HashLinha = Guid.NewGuid().ToString("N"),
            LinhaRaw = "linha crua de teste",
            Origem = OrigemFalhaImportacao.Execucao,
            Causa = causa,
            Motivo = "motivo de teste",
            NomePaciente = "MARIA DA SILVA",
            ProcedimentoTexto = "ULTRASSOM ABDOME",
            UnidadeExecutanteId = unidadeId,
            CriadoEm = DateTime.UtcNow,
            AtualizadoEm = DateTime.UtcNow,
        };
        db.SisregImportacaoFalhas.Add(f);
        await db.SaveChangesAsync();
        return f;
    }
}
