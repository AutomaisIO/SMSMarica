using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Notificacoes.WhatsApp;
using SMSMais.Core.Ouvidoria;
using SMSMais.Core.Ouvidoria.Dtos;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Ouvidoria;
using SMSMais.Tests.Infraestrutura;
using static SMSMais.Tests.Ouvidoria.OuvidoriaBancada;

namespace SMSMais.Tests.Ouvidoria;

/// <summary>
/// Máquina de estados da manifestação (ADR-0060; plano §2), contra Postgres real. Cada grupo
/// impede uma regressão específica:
/// <list type="bullet">
/// <item><b>Registro</b> — o código de acesso vazando para o banco (só o hash pode ficar) ou
/// uma solicitação anônima entrando (sem contato não há a quem responder).</item>
/// <item><b>Encaminhamento</b> — ponto inativo recebendo manifestação; prazo da área ignorando
/// o prazo do ponto; denúncia chegando à apuração sem o juízo de admissibilidade.</item>
/// <item><b>Complementação e prorrogação</b> — o relógio do cidadão continuando a correr
/// enquanto ele é quem deve; segunda complementação ou segunda prorrogação (a lei permite uma);
/// prorrogação depois de mandar para outro órgão.</item>
/// <item><b>Resposta, recurso, arquivamento</b> — conclusiva sem resolutividade (indicador do
/// OuvidorSUS fica furado); segundo recurso; duplicidade sem dizer qual é a original.</item>
/// <item><b>Sigilo e escopo</b> — a identidade saindo para quem não pode ver (ponto de resposta,
/// técnico sem sigilo) ou saindo sem deixar rastro; ponto vendo manifestação de outro ponto.</item>
/// <item><b>Rotina</b> — arquivamento por falta de complementação pegando manifestação ainda no
/// prazo, ou deixando passar a vencida.</item>
/// </list>
/// </summary>
[Collection(nameof(PostgresCollection))]
public partial class OuvidoriaManifestacaoServiceTests(PostgresFixture fixture)
{
    [GeneratedRegex(@"^\d{4}-\d{6}$")]
    private static partial Regex FormatoProtocolo();

    [GeneratedRegex("^[A-HJ-NP-Z2-9]{8}$")]
    private static partial Regex FormatoCodigo();

    // ===================== Registro =====================

    [Fact]
    public async Task Registrar_gera_protocolo_e_codigo_com_hash_salvo_e_codigo_nao_salvo()
    {
        await using var db = fixture.CriarDbContext();
        var tecnico = await CriarUsuarioAsync(db);
        var amb = Como(db, tecnico, PermOuvidoria());
        var config = await ConfigAsync(db);

        var criada = await RegistrarAsync(amb, OuvidoriaTipo.Reclamacao);

        Assert.Matches(FormatoProtocolo(), criada.Protocolo);
        Assert.StartsWith($"{OuvidoriaPrazos.Hoje().Year}-", criada.Protocolo);
        Assert.NotNull(criada.CodigoAcesso);
        Assert.Matches(FormatoCodigo(), criada.CodigoAcesso);
        Assert.Equal(OuvidoriaPrazos.Hoje().AddDays(config.PrazoCidadaoDias), criada.PrazoRespostaEm);

        var m = await LerAsync(db, criada.Id);
        Assert.Equal(OuvidoriaStatus.Registrada, m.Status);
        Assert.Equal(OuvidoriaPrioridade.Normal, m.Prioridade);
        Assert.Equal(OuvidoriaProtocolo.Hash(criada.CodigoAcesso), m.CodigoAcessoHash);
        Assert.NotEqual(criada.CodigoAcesso, m.CodigoAcessoHash);
        Assert.Equal(64, m.CodigoAcessoHash!.Length);
        Assert.Equal(tecnico, m.CriadoPor);

        var registro = Assert.Single(m.Eventos);
        Assert.Equal(OuvidoriaTipoEvento.Registro, registro.Tipo);
        Assert.True(registro.VisivelAoCidadao);
        Assert.Equal(OuvidoriaStatus.Registrada, registro.StatusNovo);
    }

    /// <summary>O código só existe na resposta do registro: nenhuma coluna da linha pode contê-lo.</summary>
    [Fact]
    public async Task Registrar_nao_deixa_o_codigo_em_texto_claro_em_nenhuma_coluna()
    {
        await using var db = fixture.CriarDbContext();
        var amb = Sistema(db);

        var criada = await RegistrarAsync(amb, OuvidoriaTipo.Sugestao);

        var linhas = await db.Database
            .SqlQueryRaw<int>(
                "SELECT count(*)::int AS \"Value\" FROM smsmarica.ouvidoria_manifestacao m WHERE m.id = {0} AND m::text ILIKE {1}",
                criada.Id, $"%{criada.CodigoAcesso}%")
            .ToListAsync();
        Assert.Equal(0, linhas[0]);
    }

    [Fact]
    public async Task Solicitacao_anonima_lanca_ValidacaoException()
    {
        await using var db = fixture.CriarDbContext();
        var amb = Sistema(db);

        var ex = await Assert.ThrowsAsync<ValidacaoException>(() =>
            RegistrarAsync(amb, OuvidoriaTipo.Solicitacao, OuvidoriaIdentificacao.Anonima));

        Assert.Contains("identificacao", ex.Erros.Keys);
    }

    [Fact]
    public async Task Informacao_sigilosa_lanca_ValidacaoException()
    {
        await using var db = fixture.CriarDbContext();
        var amb = Sistema(db);

        await Assert.ThrowsAsync<ValidacaoException>(() =>
            RegistrarAsync(amb, OuvidoriaTipo.Informacao, OuvidoriaIdentificacao.Sigilosa));
    }

    /// <summary>Solicitação identificada precisa de CPF ou de nome + telefone; só nome não basta.</summary>
    [Fact]
    public async Task Solicitacao_identificada_sem_cpf_nem_telefone_lanca_ValidacaoException()
    {
        await using var db = fixture.CriarDbContext();
        var amb = Sistema(db);
        var soNome = new ManifestanteDto("FULANO", null, null, null, null);

        await Assert.ThrowsAsync<ValidacaoException>(() =>
            RegistrarAsync(amb, OuvidoriaTipo.Solicitacao, manifestante: soNome));

        var soCpf = new ManifestanteDto(null, CpfUnico(), null, null, null);
        var ok = await RegistrarAsync(amb, OuvidoriaTipo.Solicitacao, manifestante: soCpf);
        Assert.NotNull(ok.CodigoAcesso);
    }

    [Fact]
    public async Task Denuncia_anonima_nao_gera_codigo_de_acesso()
    {
        await using var db = fixture.CriarDbContext();
        var amb = Sistema(db);

        var criada = await RegistrarAsync(amb, OuvidoriaTipo.Denuncia, OuvidoriaIdentificacao.Anonima);

        Assert.Null(criada.CodigoAcesso);
        Assert.Matches(FormatoProtocolo(), criada.Protocolo);

        var m = await LerAsync(db, criada.Id);
        Assert.Null(m.CodigoAcessoHash);
        Assert.Null(m.ManifestanteNome);
        Assert.Null(m.ManifestanteCpf);
        Assert.Null(m.ManifestanteTelefone);
    }

    // ===================== Encaminhamento =====================

    [Fact]
    public async Task Encaminhar_exige_ponto_ativo_e_calcula_PrazoAreaEm()
    {
        await using var db = fixture.CriarDbContext();
        var tecnico = await CriarUsuarioAsync(db);
        var amb = Como(db, tecnico, PermOuvidoria());
        var config = await ConfigAsync(db);
        var inativo = await CriarPontoAsync(db, ativo: false);
        var ativo = await CriarPontoAsync(db, prazoDias: 7);
        var criada = await RegistrarAsync(amb, OuvidoriaTipo.Reclamacao);

        var ex = await Assert.ThrowsAsync<ConflitoException>(() =>
            amb.Manifestacoes.EncaminharAsync(criada.Id, new EncaminharRequest(inativo.Id, null, null, null)));
        Assert.Equal("ouvidoria.ponto_inativo", ex.Codigo);

        await Assert.ThrowsAsync<NaoEncontradoException>(() =>
            amb.Manifestacoes.EncaminharAsync(criada.Id, new EncaminharRequest(Guid.NewGuid(), null, null, null)));

        await amb.Manifestacoes.EncaminharAsync(criada.Id, new EncaminharRequest(ativo.Id, null, "Favor apurar.", null));

        var m = await LerAsync(db, criada.Id);
        Assert.Equal(OuvidoriaStatus.Encaminhada, m.Status);
        Assert.Equal(ativo.Id, m.PontoRespostaId);
        Assert.NotNull(m.EncaminhadaEm);
        // Normal + prazo próprio do ponto: 7 dias, não os 20 da configuração.
        Assert.Equal(OuvidoriaPrazos.PrazoArea(OuvidoriaPrazos.Hoje(), OuvidoriaPrioridade.Normal, config, 7), m.PrazoAreaEm);
        Assert.Equal(OuvidoriaPrazos.Hoje().AddDays(7), m.PrazoAreaEm);

        var encaminhamento = Assert.Single(m.Eventos, e => e.Tipo == OuvidoriaTipoEvento.Encaminhamento);
        Assert.True(encaminhamento.VisivelAoCidadao);
        Assert.Equal(ativo.Id, encaminhamento.PontoRespostaId);
        Assert.DoesNotContain(ativo.Nome, encaminhamento.Texto); // não nomeia a área ao cidadão
        var orientacao = Assert.Single(m.Eventos, e => e.Tipo == OuvidoriaTipoEvento.Anotacao);
        Assert.False(orientacao.VisivelAoCidadao);
        Assert.Equal("Favor apurar.", orientacao.Texto);
    }

    /// <summary>Prazo explícito no pedido sobrepõe o calculado.</summary>
    [Fact]
    public async Task Encaminhar_com_PrazoDias_explicito_usa_esse_prazo()
    {
        await using var db = fixture.CriarDbContext();
        var amb = Como(db, await CriarUsuarioAsync(db), PermOuvidoria());
        var ponto = await CriarPontoAsync(db);
        var criada = await RegistrarAsync(amb, OuvidoriaTipo.Reclamacao);

        await amb.Manifestacoes.EncaminharAsync(criada.Id, new EncaminharRequest(ponto.Id, 3, null, null));

        var m = await LerAsync(db, criada.Id);
        Assert.Equal(OuvidoriaPrazos.Hoje().AddDays(3), m.PrazoAreaEm);
    }

    [Fact]
    public async Task Denuncia_nao_habilitada_nao_encaminha()
    {
        await using var db = fixture.CriarDbContext();
        var tecnico = await CriarUsuarioAsync(db);
        var amb = Como(db, tecnico, PermOuvidoria(), PermSigilo());
        var apuracao = await CriarPontoAsync(db, OuvidoriaTipoPontoResposta.Apuracao);
        var central = await CriarPontoAsync(db, OuvidoriaTipoPontoResposta.AreaCentral);
        var criada = await RegistrarAsync(amb, OuvidoriaTipo.Denuncia);

        // Sem teor pseudonimizado em todas as tentativas inválidas: o service guarda o teor na
        // entidade antes das checagens, e um teor passado aqui contaminaria o passo seguinte.
        var ex = await Assert.ThrowsAsync<ConflitoException>(() =>
            amb.Manifestacoes.EncaminharAsync(criada.Id, new EncaminharRequest(apuracao.Id, null, null, null)));
        Assert.Equal("ouvidoria.denuncia_nao_habilitada", ex.Codigo);

        await amb.Manifestacoes.HabilitarDenunciaAsync(criada.Id, new TextoRequest("Autoria, materialidade e competência presentes."));

        // Habilitada, mas para ponto que não é de apuração: também não vai.
        var ex2 = await Assert.ThrowsAsync<ConflitoException>(() =>
            amb.Manifestacoes.EncaminharAsync(criada.Id, new EncaminharRequest(central.Id, null, null, null)));
        Assert.Equal("ouvidoria.denuncia_ponto_invalido", ex2.Codigo);

        // Ponto certo, mas sem teor pseudonimizado: a apuração não pode ver o original.
        await Assert.ThrowsAsync<ValidacaoException>(() =>
            amb.Manifestacoes.EncaminharAsync(criada.Id, new EncaminharRequest(apuracao.Id, null, null, null)));
        Assert.Equal(OuvidoriaStatus.Registrada, (await LerAsync(db, criada.Id)).Status);

        await amb.Manifestacoes.EncaminharAsync(criada.Id, new EncaminharRequest(apuracao.Id, null, null, "Teor sem nomes."));

        var m = await LerAsync(db, criada.Id);
        Assert.Equal(OuvidoriaStatus.Encaminhada, m.Status);
        Assert.NotNull(m.HabilitadaEm);
        Assert.Equal(tecnico, m.HabilitadaPor);
        Assert.Equal("Teor sem nomes.", m.TeorPseudonimizado);
        var habilitacao = Assert.Single(m.Eventos, e => e.Tipo == OuvidoriaTipoEvento.Habilitacao);
        Assert.False(habilitacao.VisivelAoCidadao);
    }

    // ===================== Complementação =====================

    [Fact]
    public async Task Complementacao_suspende_e_retoma_o_prazo_e_a_segunda_vez_conflita()
    {
        await using var db = fixture.CriarDbContext();
        var amb = Como(db, await CriarUsuarioAsync(db), PermOuvidoria());
        var criada = await RegistrarAsync(amb, OuvidoriaTipo.Reclamacao);
        var prazoOriginal = criada.PrazoRespostaEm;

        await amb.Manifestacoes.PedirComplementacaoAsync(criada.Id, new TextoRequest("Informe a data e o nome do setor."));

        var suspensa = await LerAsync(db, criada.Id);
        Assert.Equal(OuvidoriaStatus.AguardandoComplementacao, suspensa.Status);
        Assert.True(suspensa.ComplementacaoUsada);
        Assert.NotNull(suspensa.SuspensaEm);
        Assert.NotNull(suspensa.ComplementacaoSolicitadaEm);
        Assert.Equal(prazoOriginal, suspensa.PrazoRespostaEm); // ainda não moveu: move quando retoma

        // O cidadão demorou 5 dias: força a suspensão no passado direto no banco.
        var haCincoDias = DateTime.UtcNow.AddDays(-5);
        await db.OuvidoriaManifestacoes.Where(m => m.Id == criada.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.SuspensaEm, haCincoDias));
        db.ChangeTracker.Clear(); // ExecuteUpdate não atualiza o tracker: o service leria a cópia velha

        await amb.Manifestacoes.ComplementarAsync(criada.Id, new TextoComAnexosRequest("Foi dia 10, no setor de marcação.", []));

        var retomada = await LerAsync(db, criada.Id);
        Assert.Equal(OuvidoriaStatus.EmTriagem, retomada.Status);
        Assert.Null(retomada.SuspensaEm);
        Assert.Equal(5, retomada.DiasSuspensos);
        Assert.Equal(prazoOriginal.AddDays(5), retomada.PrazoRespostaEm);
        var complementacao = Assert.Single(retomada.Eventos, e => e.Tipo == OuvidoriaTipoEvento.Complementacao);
        Assert.True(complementacao.VisivelAoCidadao);

        var ex = await Assert.ThrowsAsync<ConflitoException>(() =>
            amb.Manifestacoes.PedirComplementacaoAsync(criada.Id, new TextoRequest("De novo?")));
        Assert.Equal("ouvidoria.complementacao_unica", ex.Codigo);
    }

    [Fact]
    public async Task Pedir_complementacao_em_anonima_conflita()
    {
        await using var db = fixture.CriarDbContext();
        var amb = Como(db, await CriarUsuarioAsync(db), PermOuvidoria(), PermSigilo());
        var criada = await RegistrarAsync(amb, OuvidoriaTipo.Denuncia, OuvidoriaIdentificacao.Anonima);

        var ex = await Assert.ThrowsAsync<ConflitoException>(() =>
            amb.Manifestacoes.PedirComplementacaoAsync(criada.Id, new TextoRequest("Quem?")));
        Assert.Equal("ouvidoria.anonima_sem_contato", ex.Codigo);
    }

    // ===================== Prorrogação =====================

    [Fact]
    public async Task Prorrogar_so_uma_vez()
    {
        await using var db = fixture.CriarDbContext();
        var amb = Como(db, await CriarUsuarioAsync(db), PermOuvidoria());
        var config = await ConfigAsync(db);
        var criada = await RegistrarAsync(amb, OuvidoriaTipo.Reclamacao);
        var justificativa = "A unidade ainda não devolveu o relatório da apuração interna.";

        await Assert.ThrowsAsync<ValidacaoException>(() =>
            amb.Manifestacoes.ProrrogarAsync(criada.Id, new TextoRequest("curta")));

        await amb.Manifestacoes.ProrrogarAsync(criada.Id, new TextoRequest(justificativa));

        var m = await LerAsync(db, criada.Id);
        Assert.NotNull(m.ProrrogadoEm);
        Assert.Equal(justificativa, m.ProrrogacaoJustificativa);
        Assert.Equal(criada.PrazoRespostaEm.AddDays(config.ProrrogacaoDias), m.PrazoRespostaEm);
        var evento = Assert.Single(m.Eventos, e => e.Tipo == OuvidoriaTipoEvento.Prorrogacao);
        Assert.True(evento.VisivelAoCidadao);
        Assert.Contains(justificativa, evento.Texto); // art. 16 §1º: justificativa expressa ao cidadão

        var ex = await Assert.ThrowsAsync<ConflitoException>(() =>
            amb.Manifestacoes.ProrrogarAsync(criada.Id, new TextoRequest(justificativa)));
        Assert.Equal("ouvidoria.prorrogacao_unica", ex.Codigo);
    }

    [Fact]
    public async Task Prorrogar_proibido_apos_encaminhamento_externo()
    {
        await using var db = fixture.CriarDbContext();
        var amb = Como(db, await CriarUsuarioAsync(db), PermOuvidoria());
        var criada = await RegistrarAsync(amb, OuvidoriaTipo.Reclamacao);

        await amb.Manifestacoes.EncaminharExternoAsync(criada.Id,
            new EncaminharExternoRequest("Ouvidoria Geral do Município", "OGM-123", "Assunto de iluminação pública, fora da saúde."));

        var m = await LerAsync(db, criada.Id);
        Assert.Equal(OuvidoriaStatus.EncaminhadaOutroOrgao, m.Status);
        Assert.Equal("Ouvidoria Geral do Município", m.SistemaExterno);
        Assert.Equal("OGM-123", m.ProtocoloExterno);

        var ex = await Assert.ThrowsAsync<ConflitoException>(() =>
            amb.Manifestacoes.ProrrogarAsync(criada.Id, new TextoRequest("Justificativa longa o bastante para passar.")));
        Assert.Equal("ouvidoria.prorrogacao_vedada_externo", ex.Codigo);
    }

    // ===================== Resposta ao cidadão =====================

    [Fact]
    public async Task Responder_conclusiva_exige_resolutividade_e_situacao_e_grava_DiasAteResposta()
    {
        await using var db = fixture.CriarDbContext();
        var amb = Como(db, await CriarUsuarioAsync(db), PermOuvidoria());
        var criada = await RegistrarAsync(amb, OuvidoriaTipo.Solicitacao);
        await amb.Manifestacoes.TriarAsync(criada.Id, new TriarRequest(null, null, null, null, null, null, null, null, null));

        // Registrada há 10 dias, com 3 dias suspensos: 7 dias até a resposta.
        var haDezDias = DateTime.UtcNow.AddDays(-10);
        await db.OuvidoriaManifestacoes.Where(m => m.Id == criada.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.RegistradaEm, haDezDias)
                .SetProperty(m => m.DiasSuspensos, 3));
        db.ChangeTracker.Clear(); // ExecuteUpdate não atualiza o tracker: o service leria a cópia velha

        const string texto = "Sua consulta foi agendada para o dia 30/09 na policlínica central.";

        var semResolutividade = await Assert.ThrowsAsync<ValidacaoException>(() =>
            amb.Manifestacoes.ResponderCidadaoAsync(criada.Id,
                new ResponderCidadaoRequest(texto, true, null, OuvidoriaSituacaoFinal.Atendida, null)));
        Assert.Contains("resolutividade", semResolutividade.Erros.Keys);

        var semSituacao = await Assert.ThrowsAsync<ValidacaoException>(() =>
            amb.Manifestacoes.ResponderCidadaoAsync(criada.Id,
                new ResponderCidadaoRequest(texto, true, OuvidoriaResolutividade.Resolvida, null, null)));
        Assert.Contains("situacaoFinal", semSituacao.Erros.Keys);

        // Procede é de reclamação/denúncia, não de solicitação.
        await Assert.ThrowsAsync<ValidacaoException>(() =>
            amb.Manifestacoes.ResponderCidadaoAsync(criada.Id,
                new ResponderCidadaoRequest(texto, true, OuvidoriaResolutividade.Resolvida, OuvidoriaSituacaoFinal.Procede, null)));

        // Não atendida sem motivo.
        await Assert.ThrowsAsync<ValidacaoException>(() =>
            amb.Manifestacoes.ResponderCidadaoAsync(criada.Id,
                new ResponderCidadaoRequest(texto, true, OuvidoriaResolutividade.NaoResolvida, OuvidoriaSituacaoFinal.NaoAtendida, null)));

        // Nenhuma das tentativas inválidas pode ter mudado o status.
        Assert.Equal(OuvidoriaStatus.EmTriagem, (await LerAsync(db, criada.Id)).Status);

        await amb.Manifestacoes.ResponderCidadaoAsync(criada.Id,
            new ResponderCidadaoRequest(texto, true, OuvidoriaResolutividade.Resolvida, OuvidoriaSituacaoFinal.Atendida, null));

        var m = await LerAsync(db, criada.Id);
        Assert.Equal(OuvidoriaStatus.Respondida, m.Status);
        Assert.NotNull(m.RespondidaEm);
        Assert.Equal(7, m.DiasAteResposta);
        Assert.Equal(0, m.DiasAtraso);
        Assert.Equal(OuvidoriaResolutividade.Resolvida, m.Resolutividade);
        Assert.Equal(OuvidoriaSituacaoFinal.Atendida, m.SituacaoFinal);
        Assert.Equal(texto, m.RespostaConclusiva);
        var evento = Assert.Single(m.Eventos, e => e.Tipo == OuvidoriaTipoEvento.RespostaConclusiva);
        Assert.True(evento.VisivelAoCidadao);
    }

    /// <summary>Intermediária é só um evento visível: o status fica onde estava.</summary>
    [Fact]
    public async Task Responder_intermediaria_nao_muda_o_status()
    {
        await using var db = fixture.CriarDbContext();
        var amb = Como(db, await CriarUsuarioAsync(db), PermOuvidoria());
        var criada = await RegistrarAsync(amb, OuvidoriaTipo.Reclamacao);
        await amb.Manifestacoes.TriarAsync(criada.Id, new TriarRequest(null, null, null, null, null, null, null, null, null));

        await amb.Manifestacoes.ResponderCidadaoAsync(criada.Id,
            new ResponderCidadaoRequest("Estamos apurando com a unidade.", false, null, null, null));

        var m = await LerAsync(db, criada.Id);
        Assert.Equal(OuvidoriaStatus.EmTriagem, m.Status);
        Assert.Null(m.RespondidaEm);
        Assert.Single(m.Eventos, e => e.Tipo == OuvidoriaTipoEvento.RespostaIntermediaria && e.VisivelAoCidadao);
    }

    /// <summary>Anônima não tem para onde voltar: a resposta conclusiva já conclui.</summary>
    [Fact]
    public async Task Responder_conclusiva_em_anonima_ja_conclui()
    {
        await using var db = fixture.CriarDbContext();
        var amb = Como(db, await CriarUsuarioAsync(db), PermOuvidoria(), PermSigilo());
        var criada = await RegistrarAsync(amb, OuvidoriaTipo.Denuncia, OuvidoriaIdentificacao.Anonima);
        await amb.Manifestacoes.TriarAsync(criada.Id, new TriarRequest(null, null, null, null, null, null, null, null, null));

        await amb.Manifestacoes.ResponderCidadaoAsync(criada.Id,
            new ResponderCidadaoRequest("Apurado; não foram encontrados indícios.", true,
                OuvidoriaResolutividade.Resolvida, OuvidoriaSituacaoFinal.NaoProcede, null));

        var m = await LerAsync(db, criada.Id);
        Assert.Equal(OuvidoriaStatus.Concluida, m.Status);
        Assert.NotNull(m.ConcluidaEm);
        await amb.WhatsApp.DidNotReceive().EnviarTextoAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>(), Arg.Any<OrigemEnvioWhatsApp>());
    }

    // ===================== Recurso =====================

    [Fact]
    public async Task Recurso_so_uma_vez()
    {
        await using var db = fixture.CriarDbContext();
        var amb = Como(db, await CriarUsuarioAsync(db), PermOuvidoria());
        var criada = await RegistrarAsync(amb, OuvidoriaTipo.Reclamacao);
        await ResponderConclusivaAsync(amb, criada.Id, OuvidoriaSituacaoFinal.NaoProcede);

        await amb.Manifestacoes.RegistrarRecursoAsync(criada.Id, new TextoRequest("Discordo: tenho testemunhas."));

        var m = await LerAsync(db, criada.Id);
        Assert.Equal(OuvidoriaStatus.EmRecurso, m.Status);
        Assert.Single(m.Eventos, e => e.Tipo == OuvidoriaTipoEvento.Recurso && e.VisivelAoCidadao);

        // Responde de novo e tenta o segundo recurso: cabe um só.
        await amb.Manifestacoes.ResponderCidadaoAsync(criada.Id,
            new ResponderCidadaoRequest("Reanalisado: mantida a conclusão anterior.", true,
                OuvidoriaResolutividade.NaoResolvida, OuvidoriaSituacaoFinal.NaoProcede, null));
        Assert.Equal(OuvidoriaStatus.Respondida, (await LerAsync(db, criada.Id)).Status);

        var ex = await Assert.ThrowsAsync<ConflitoException>(() =>
            amb.Manifestacoes.RegistrarRecursoAsync(criada.Id, new TextoRequest("De novo.")));
        Assert.Equal("ouvidoria.recurso_unico", ex.Codigo);
    }

    // ===================== Arquivamento =====================

    [Fact]
    public async Task Arquivar_por_duplicidade_exige_texto()
    {
        await using var db = fixture.CriarDbContext();
        var amb = Como(db, await CriarUsuarioAsync(db), PermOuvidoria());
        var criada = await RegistrarAsync(amb, OuvidoriaTipo.Reclamacao);

        var ex = await Assert.ThrowsAsync<ValidacaoException>(() =>
            amb.Manifestacoes.ArquivarAsync(criada.Id, new ArquivarRequest(OuvidoriaMotivoArquivamento.Duplicidade, null)));
        Assert.Contains("texto", ex.Erros.Keys);
        Assert.Equal(OuvidoriaStatus.Registrada, (await LerAsync(db, criada.Id)).Status);

        await amb.Manifestacoes.ArquivarAsync(criada.Id, new ArquivarRequest(OuvidoriaMotivoArquivamento.Duplicidade, "Duplica a 2026-000001."));

        var m = await LerAsync(db, criada.Id);
        Assert.Equal(OuvidoriaStatus.Arquivada, m.Status);
        Assert.Equal(OuvidoriaMotivoArquivamento.Duplicidade, m.MotivoArquivamento);
        var visivel = Assert.Single(m.Eventos, e => e.Tipo == OuvidoriaTipoEvento.Arquivamento);
        Assert.True(visivel.VisivelAoCidadao);
        Assert.DoesNotContain("2026-000001", visivel.Texto); // o protocolo original fica na anotação interna
        var interna = Assert.Single(m.Eventos, e => e.Tipo == OuvidoriaTipoEvento.Anotacao);
        Assert.False(interna.VisivelAoCidadao);
        Assert.Contains("2026-000001", interna.Texto);

        // Final: nada mais transita.
        var ex2 = await Assert.ThrowsAsync<ConflitoException>(() =>
            amb.Manifestacoes.ArquivarAsync(criada.Id, new ArquivarRequest(OuvidoriaMotivoArquivamento.Outro, null)));
        Assert.Equal("ouvidoria.transicao_invalida", ex2.Codigo);
    }

    // ===================== Sigilo =====================

    [Fact]
    public async Task Revelar_identidade_grava_acesso_evento_e_auditoria()
    {
        await using var db = fixture.CriarDbContext();
        var ouvidor = await CriarUsuarioAsync(db, "OUVIDOR");
        var comSigilo = Como(db, ouvidor, PermOuvidoria(), PermSigilo());
        var manifestante = ManifestanteIdentificado("MARIA SIGILOSA");
        var criada = await RegistrarAsync(comSigilo, OuvidoriaTipo.Reclamacao, OuvidoriaIdentificacao.Sigilosa, manifestante);
        const string justificativa = "Necessário contato para esclarecer o local do fato.";

        // Sigilosa vem mascarada até para quem tem sigilo (desvio documentado em PROGRESSO.md).
        var detalhe = await comSigilo.Manifestacoes.ObterAsync(criada.Id);
        Assert.Null(detalhe.Manifestante);
        Assert.Null(detalhe.ManifestanteNome);
        Assert.True(detalhe.IdentidadeRestrita);
        Assert.Contains("revelarIdentidade", detalhe.AcoesPermitidas);

        await Assert.ThrowsAsync<ValidacaoException>(() =>
            comSigilo.Manifestacoes.RevelarIdentidadeAsync(criada.Id, "curta"));

        var revelado = await comSigilo.Manifestacoes.RevelarIdentidadeAsync(criada.Id, justificativa);

        Assert.Equal("MARIA SIGILOSA", revelado.Nome);
        Assert.Equal(manifestante.Cpf, revelado.Cpf);

        var acesso = Assert.Single(await db.OuvidoriaAcessosIdentidade.AsNoTracking()
            .Where(a => a.ManifestacaoId == criada.Id).ToListAsync());
        Assert.Equal(ouvidor, acesso.UsuarioId);
        Assert.Equal(justificativa, acesso.Justificativa);
        Assert.Equal("127.0.0.1", acesso.Ip);

        var m = await LerAsync(db, criada.Id);
        var evento = Assert.Single(m.Eventos, e => e.Tipo == OuvidoriaTipoEvento.AcessoIdentidade);
        Assert.False(evento.VisivelAoCidadao);
        Assert.Equal(ouvidor, evento.AutorId);

        await comSigilo.Auditoria.Received(1).RegistrarAsync(
            "OuvidoriaManifestacao", criada.Id.ToString(), "RevelarIdentidade", null, justificativa, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Revelar_identidade_sem_sigilo_e_negado()
    {
        await using var db = fixture.CriarDbContext();
        var comSigilo = Como(db, await CriarUsuarioAsync(db), PermOuvidoria(), PermSigilo());
        var criada = await RegistrarAsync(comSigilo, OuvidoriaTipo.Reclamacao, OuvidoriaIdentificacao.Sigilosa);

        var semSigilo = Como(db, await CriarUsuarioAsync(db), PermOuvidoria());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            semSigilo.Manifestacoes.RevelarIdentidadeAsync(criada.Id, "Justificativa longa o bastante."));

        Assert.Empty(await db.OuvidoriaAcessosIdentidade.AsNoTracking().Where(a => a.ManifestacaoId == criada.Id).ToListAsync());
    }

    // ===================== Escopo: ponto de resposta =====================

    [Fact]
    public async Task Membro_de_ponto_ve_sem_manifestante_e_nao_ve_manifestacao_de_outro_ponto()
    {
        await using var db = fixture.CriarDbContext();
        var tecnico = await CriarUsuarioAsync(db);
        var ouvidoria = Como(db, tecnico, PermOuvidoria());
        var membro = await CriarUsuarioAsync(db, "MEMBRO PONTO A");
        var pontoA = await CriarPontoAsync(db, membros: membro);
        var pontoB = await CriarPontoAsync(db);

        var minha = await RegistrarAsync(ouvidoria, OuvidoriaTipo.Reclamacao);
        var alheia = await RegistrarAsync(ouvidoria, OuvidoriaTipo.Reclamacao);

        var comoMembro = Como(db, membro, PermPonto());

        // Antes de encaminhar, o ponto não vê nem a "sua".
        await Assert.ThrowsAsync<NaoEncontradoException>(() => comoMembro.Manifestacoes.ObterAsync(minha.Id));

        await ouvidoria.Manifestacoes.EncaminharAsync(minha.Id, new EncaminharRequest(pontoA.Id, null, "Orientação ao ponto A.", null));
        await ouvidoria.Manifestacoes.EncaminharAsync(alheia.Id, new EncaminharRequest(pontoB.Id, null, null, null));

        // Contexto novo: o service cacheia o contexto de acesso por instância.
        comoMembro = Como(db, membro, PermPonto());
        var detalhe = await comoMembro.Manifestacoes.ObterAsync(minha.Id);

        Assert.Null(detalhe.Manifestante);
        Assert.Null(detalhe.ManifestanteNome);
        Assert.True(detalhe.IdentidadeRestrita);
        Assert.Empty(detalhe.PossiveisDuplicatas);
        Assert.Contains("responderArea", detalhe.AcoesPermitidas);
        Assert.DoesNotContain("triar", detalhe.AcoesPermitidas);
        Assert.DoesNotContain("revelarIdentidade", detalhe.AcoesPermitidas);
        // Vê a orientação presa ao seu ponto, e nada da triagem interna da ouvidoria.
        Assert.Contains(detalhe.Eventos, e => e.Tipo == OuvidoriaTipoEvento.Anotacao && e.Texto == "Orientação ao ponto A.");
        Assert.All(detalhe.Eventos, e => Assert.True(e.VisivelAoCidadao || e.PontoRespostaNome == pontoA.Nome));

        await Assert.ThrowsAsync<NaoEncontradoException>(() => comoMembro.Manifestacoes.ObterAsync(alheia.Id));

        var lista = await comoMembro.Manifestacoes.ListarAsync(new ManifestacaoFiltro(null, null, null, null, null, null, null, null, null, null, 1, 200));
        Assert.Contains(lista.Itens, i => i.Id == minha.Id);
        Assert.DoesNotContain(lista.Itens, i => i.Id == alheia.Id);
        Assert.All(lista.Itens, i => Assert.Null(i.ManifestanteNome));

        // O membro responde pelo seu ponto; pelo alheio, 404.
        await comoMembro.Manifestacoes.ResponderAreaAsync(minha.Id, new TextoComAnexosRequest("Conversamos com a equipe da recepção.", []));
        Assert.Equal(OuvidoriaStatus.RespondidaPelaArea, (await LerAsync(db, minha.Id)).Status);
        await Assert.ThrowsAsync<NaoEncontradoException>(() =>
            comoMembro.Manifestacoes.ResponderAreaAsync(alheia.Id, new TextoComAnexosRequest("Não é meu.", [])));
    }

    // ===================== Escopo: denúncia e sigilo =====================

    [Fact]
    public async Task Ouvidoria_sem_sigilo_nao_ve_denuncia_na_lista_e_com_sigilo_ve()
    {
        await using var db = fixture.CriarDbContext();
        var comSigilo = Como(db, await CriarUsuarioAsync(db, "OUVIDOR"), PermOuvidoria(), PermSigilo());
        var semSigilo = Como(db, await CriarUsuarioAsync(db, "TECNICO"), PermOuvidoria());
        var manifestante = ManifestanteIdentificado("JOAO DENUNCIANTE");
        var denuncia = await RegistrarAsync(comSigilo, OuvidoriaTipo.Denuncia, manifestante: manifestante);
        var reclamacao = await RegistrarAsync(comSigilo, OuvidoriaTipo.Reclamacao);

        var filtro = new ManifestacaoFiltro(null, OuvidoriaTipo.Denuncia, null, null, null, null, null, denuncia.Protocolo, null, null, 1, 50);

        var listaSem = await semSigilo.Manifestacoes.ListarAsync(filtro);
        Assert.DoesNotContain(listaSem.Itens, i => i.Id == denuncia.Id);
        await Assert.ThrowsAsync<NaoEncontradoException>(() => semSigilo.Manifestacoes.ObterAsync(denuncia.Id));
        // A reclamação comum ele vê, com manifestante.
        var detalheReclamacao = await semSigilo.Manifestacoes.ObterAsync(reclamacao.Id);
        Assert.NotNull(detalheReclamacao.Manifestante);
        Assert.False(detalheReclamacao.IdentidadeRestrita);

        var listaCom = await comSigilo.Manifestacoes.ListarAsync(filtro);
        var linha = Assert.Single(listaCom.Itens, i => i.Id == denuncia.Id);
        Assert.Equal("JOAO DENUNCIANTE", linha.ManifestanteNome);
        var detalhe = await comSigilo.Manifestacoes.ObterAsync(denuncia.Id);
        Assert.NotNull(detalhe.Manifestante);
        Assert.Equal(manifestante.Cpf, detalhe.Manifestante!.Cpf);
        Assert.False(detalhe.IdentidadeRestrita);
        Assert.Contains("habilitar", detalhe.AcoesPermitidas);
    }

    /// <summary>Busca por nome não pode denunciar a existência de uma sigilosa.</summary>
    [Fact]
    public async Task Busca_por_nome_nao_encontra_sigilosa()
    {
        await using var db = fixture.CriarDbContext();
        var amb = Como(db, await CriarUsuarioAsync(db), PermOuvidoria(), PermSigilo());
        var nome = $"SIGILOSA {Guid.NewGuid():N}"[..30];
        var criada = await RegistrarAsync(amb, OuvidoriaTipo.Reclamacao, OuvidoriaIdentificacao.Sigilosa, ManifestanteIdentificado(nome));

        var porNome = await amb.Manifestacoes.ListarAsync(new ManifestacaoFiltro(null, null, null, null, null, null, null, nome, null, null, 1, 50));
        Assert.DoesNotContain(porNome.Itens, i => i.Id == criada.Id);

        var porProtocolo = await amb.Manifestacoes.ListarAsync(new ManifestacaoFiltro(null, null, null, null, null, null, null, criada.Protocolo, null, null, 1, 50));
        var linha = Assert.Single(porProtocolo.Itens, i => i.Id == criada.Id);
        Assert.Null(linha.ManifestanteNome);
    }

    // ===================== Transições =====================

    [Fact]
    public async Task Transicao_invalida_lanca_ConflitoException_com_codigo()
    {
        await using var db = fixture.CriarDbContext();
        var amb = Como(db, await CriarUsuarioAsync(db), PermOuvidoria());
        var criada = await RegistrarAsync(amb, OuvidoriaTipo.Reclamacao);

        var complementar = await Assert.ThrowsAsync<ConflitoException>(() =>
            amb.Manifestacoes.ComplementarAsync(criada.Id, new TextoComAnexosRequest("Sem pedido.", [])));
        Assert.Equal("ouvidoria.transicao_invalida", complementar.Codigo);

        var recurso = await Assert.ThrowsAsync<ConflitoException>(() =>
            amb.Manifestacoes.RegistrarRecursoAsync(criada.Id, new TextoRequest("Sem resposta.")));
        Assert.Equal("ouvidoria.transicao_invalida", recurso.Codigo);

        var concluir = await Assert.ThrowsAsync<ConflitoException>(() => amb.Manifestacoes.ConcluirAsync(criada.Id));
        Assert.Equal("ouvidoria.transicao_invalida", concluir.Codigo);

        var responderArea = await Assert.ThrowsAsync<ConflitoException>(() =>
            amb.Manifestacoes.ResponderAreaAsync(criada.Id, new TextoComAnexosRequest("Não encaminhada.", [])));
        Assert.Equal("ouvidoria.transicao_invalida", responderArea.Codigo);

        var cobrar = await Assert.ThrowsAsync<ConflitoException>(() => amb.Manifestacoes.CobrarAsync(criada.Id, null));
        Assert.Equal("ouvidoria.transicao_invalida", cobrar.Codigo);

        Assert.Equal(OuvidoriaStatus.Registrada, (await LerAsync(db, criada.Id)).Status);
    }

    // ===================== Rotina =====================

    [Fact]
    public async Task ArquivarSemComplementacao_arquiva_so_a_que_passou_do_prazo()
    {
        await using var db = fixture.CriarDbContext();
        var amb = Como(db, await CriarUsuarioAsync(db), PermOuvidoria());
        var config = await ConfigAsync(db);
        var vencida = await RegistrarAsync(amb, OuvidoriaTipo.Reclamacao);
        var noPrazo = await RegistrarAsync(amb, OuvidoriaTipo.Reclamacao);
        await amb.Manifestacoes.PedirComplementacaoAsync(vencida.Id, new TextoRequest("Qual unidade?"));
        await amb.Manifestacoes.PedirComplementacaoAsync(noPrazo.Id, new TextoRequest("Qual unidade?"));

        var limite = DateTime.UtcNow.AddDays(-(config.ComplementacaoDias + 1));
        await db.OuvidoriaManifestacoes.Where(m => m.Id == vencida.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.ComplementacaoSolicitadaEm, limite)
                .SetProperty(m => m.SuspensaEm, limite));
        db.ChangeTracker.Clear(); // ExecuteUpdate não atualiza o tracker: o service leria a cópia velha

        var rotina = Sistema(db);
        var quantas = await rotina.Manifestacoes.ArquivarSemComplementacaoAsync();

        Assert.True(quantas >= 1, "a rotina devia ter arquivado ao menos a vencida");

        var arquivada = await LerAsync(db, vencida.Id);
        Assert.Equal(OuvidoriaStatus.Arquivada, arquivada.Status);
        Assert.Equal(OuvidoriaMotivoArquivamento.SemComplementacao, arquivada.MotivoArquivamento);
        Assert.Null(arquivada.SuspensaEm);
        var evento = Assert.Single(arquivada.Eventos, e => e.Tipo == OuvidoriaTipoEvento.Arquivamento);
        Assert.True(evento.VisivelAoCidadao);
        Assert.Equal("Sistema", evento.AutorNome);
        Assert.Null(evento.AutorId);

        Assert.Equal(OuvidoriaStatus.AguardandoComplementacao, (await LerAsync(db, noPrazo.Id)).Status);
    }

    // ===================== apoio =====================

    private static async Task ResponderConclusivaAsync(Ambiente amb, Guid id, OuvidoriaSituacaoFinal situacao)
    {
        await amb.Manifestacoes.TriarAsync(id, new TriarRequest(null, null, null, null, null, null, null, null, null));
        await amb.Manifestacoes.ResponderCidadaoAsync(id,
            new ResponderCidadaoRequest("Apurado junto à unidade; resposta conclusiva ao cidadão.", true,
                OuvidoriaResolutividade.Resolvida, situacao, null));
    }
}
