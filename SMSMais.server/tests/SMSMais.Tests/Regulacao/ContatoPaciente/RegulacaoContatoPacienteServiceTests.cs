using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using NSubstitute;

using SMSMais.Core.Identidade;
using SMSMais.Core.GuiasAgendamento;
using SMSMais.Core.Notificacoes.Confirmacoes;
using SMSMais.Core.Notificacoes.Confirmacoes.Dtos;
using SMSMais.Core.Pacientes;
using SMSMais.Core.Regulacao.Comum;
using SMSMais.Core.Regulacao.ContatoPaciente;
using SMSMais.Core.Solicitacoes;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Notificacoes;
using SMSMais.Data.Entities.Regulacao;
using SMSMais.Data.Entities.Ser;
using SMSMais.Tests.Infraestrutura;
using SMSMais.Tests.Regulacao.EnvioSer;

namespace SMSMais.Tests.Regulacao.ContatoPaciente;

/// <summary>
/// O quadro "Contato com o paciente" da solicitação da regulação (10/10/2026): a unidade e o
/// regulador vendo se o paciente foi avisado pelo WhatsApp, se leu e o que respondeu.
///
/// <para>O que estes testes guardam: do SER não sai aviso automático (o quadro diz isso, em vez de
/// fingir um "aguardando"); as mensagens da conversa entram desde a abertura do pedido, sem nota
/// interna; e o aviso do SISREG chega com recibos e a resposta do paciente.</para>
/// </summary>
[Collection(nameof(PostgresCollection))]
public class RegulacaoContatoPacienteServiceTests(PostgresFixture fixture)
{
    private static RegulacaoContatoPacienteService Servico(
        SmsMaisDbContext db, string? telefoneConfirmado = null, bool avisoExterno = true)
    {
        // Sem usuário no contexto o escopo é "tudo" (como num job): o alcance tem testes próprios.
        var acessor = new UsuarioAtualAccessorFake();
        var escopo = new RegulacaoEscopo(db, acessor, Substitute.For<IIdentidadeService>());
        var pacientes = Substitute.For<IPacientesService>();
        pacientes.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(PacienteNaTelaTests.Paciente() with { TelefoneVerificado = telefoneConfirmado });
        var regras = Substitute.For<IConfirmacaoConfiguracaoService>();
        regras.ObterAsync(Arg.Any<CancellationToken>()).Returns(new ConfirmacaoConfiguracaoDto(
            "08:00", "18:00", 100, SomenteSisreg: true, true, null, AvisoAgendamentoExternoHabilitado: avisoExterno));
        return new RegulacaoContatoPacienteService(
            db, escopo, new SolicitacaoHistoricoService(db, acessor), pacientes, regras,
            Substitute.For<IFilaGuiasService>());
    }

    private static async Task<RegulacaoSolicitacao> SolicitacaoAsync(
        SmsMaisDbContext db, Guid pacienteId, SistemaRegulacao destino, DateTime criadaEm,
        Guid? espinhaId = null, Guid? serId = null)
    {
        var unidade = new Unidade
        {
            Id = Guid.NewGuid(),
            Nome = $"UNID CONTATO {Guid.NewGuid():N}"[..40],
            CriadoEm = DateTime.UtcNow,
        };
        db.Unidades.Add(unidade);

        var procedimento = new RegulacaoProcedimento
        {
            Id = Guid.CreateVersion7(),
            NomeCanonico = $"PROC CONTATO {Guid.NewGuid():N}"[..40],
            NomeNormalizado = "PROC CONTATO",
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
            PacienteId = pacienteId,
            PacienteNome = "PACIENTE TESTE",
            ProcedimentoId = procedimento.Id,
            SistemaDestino = destino,
            Status = StatusRegulacao.Agendada,
            NumeroExterno = Random.Shared.Next(1_000_000, 9_999_999).ToString(),
            SolicitacaoId = espinhaId,
            SerSolicitacaoId = serId,
            CriadoEm = criadaEm,
        };
        db.RegulacaoSolicitacoes.Add(s);
        await db.SaveChangesAsync();
        return s;
    }

    private static MensagemWhatsApp Mensagem(
        Guid pacienteId, DateTime em, DirecaoMensagem direcao, string? conteudo,
        TipoMensagem tipo = TipoMensagem.Texto, string? autor = null) => new()
    {
        Id = Guid.CreateVersion7(),
        PacienteId = pacienteId,
        Telefone = "5521999990000",
        Direcao = direcao,
        Conteudo = conteudo,
        TipoMensagem = tipo,
        Status = direcao == DirecaoMensagem.Entrada ? StatusMensagemWhatsApp.Recebida : StatusMensagemWhatsApp.Lida,
        AutorNomeExibicao = autor,
        OcorridoEm = em,
        CriadoEm = em,
    };

    [Fact]
    public async Task Ser_sem_aviso_automatico_mostra_o_agendamento_e_a_conversa_desde_a_abertura()
    {
        await using var db = fixture.CriarDbContext();
        var paciente = Guid.NewGuid();
        var abertura = DateTime.UtcNow.AddDays(-8);

        var ser = new SerSolicitacao
        {
            Id = Guid.NewGuid(), IdSer = Random.Shared.Next(1_000_000, 9_999_999).ToString(), Tipo = TipoRecursoSer.Consulta,
            Recurso = "CONSULTA EM ODONTOLOGIA", PacienteNome = "PACIENTE TESTE", PacienteId = paciente,
            Situacao = SituacaoSer.Agendada, AgendadoParaTexto = "19/10/2026",
            UnidadeExecutora = "HOSPITAL DE EXEMPLO", SincronizadoEm = DateTime.UtcNow, CriadoEm = DateTime.UtcNow,
        };
        db.SerSolicitacoes.Add(ser);
        await db.SaveChangesAsync();
        var s = await SolicitacaoAsync(db, paciente, SistemaRegulacao.Ser, abertura, serId: ser.Id);

        // O aviso automático de OUTRO pedido do mesmo paciente (um SISREG), que caiu na mesma conversa.
        var outroPedido = await SeedSolicitacao.CriarAsync(db, paciente, dataAgendada: DateTime.UtcNow.AddDays(30));
        var avisoDoOutro = Mensagem(paciente, abertura.AddDays(4), DirecaoMensagem.Saida, "Sua consulta foi agendada!",
            TipoMensagem.Template);
        db.MensagensWhatsApp.Add(avisoDoOutro);
        db.ComunicacoesPaciente.Add(new ComunicacaoPaciente
        {
            Id = Guid.CreateVersion7(),
            Tipo = TipoAgendamento.Consulta,
            Finalidade = FinalidadeComunicacao.ConfirmacaoAgendamento,
            SolicitacaoId = outroPedido.SolicitacaoId,
            PacienteId = paciente,
            Status = StatusComunicacao.Entregue,
            MensagemWhatsAppId = avisoDoOutro.Id,
            CriadoEm = abertura.AddDays(4),
        });

        db.MensagensWhatsApp.AddRange(
            Mensagem(paciente, abertura.AddDays(-30), DirecaoMensagem.Saida, "de antes do pedido"),
            Mensagem(paciente, abertura.AddDays(5), DirecaoMensagem.Saida, "Sua consulta foi agendada",
                TipoMensagem.Template, autor: "ATENDENTE TESTE"),
            Mensagem(paciente, abertura.AddDays(5).AddMinutes(30), DirecaoMensagem.Entrada, "Falar com um atendente"),
            Mensagem(paciente, abertura.AddDays(5).AddMinutes(40), DirecaoMensagem.Saida, "anotação da equipe",
                TipoMensagem.NotaInterna, autor: "ATENDENTE TESTE"),
            Mensagem(Guid.NewGuid(), abertura.AddDays(5), DirecaoMensagem.Saida, "de outro paciente"));
        await db.SaveChangesAsync();

        // Com a chave do aviso externo DESLIGADA o SER volta a não avisar sozinho — e o quadro diz isso.
        var dto = await Servico(db, telefoneConfirmado: "21999990000", avisoExterno: false)
            .ObterAsync(s.Id, CancellationToken.None);

        dto.DestinoComAvisoAutomatico.Should().BeFalse("com a chave desligada o SER não avisa sozinho");
        dto.Avisos.Should().BeEmpty();
        dto.Confirmacao.Should().BeNull();
        dto.Agendamento.Should().NotBeNull();
        dto.Agendamento!.DataTexto.Should().Be("19/10/2026");
        dto.Agendamento.Local.Should().Be("HOSPITAL DE EXEMPLO");
        dto.TelefoneConfirmado.Should().Be("21999990000");

        dto.Mensagens.Select(m => m.Texto).Should().Equal(
            ["Falar com um atendente", "Sua consulta foi agendada", "Sua consulta foi agendada!"],
            "mais recente primeiro; sem o que veio antes do pedido, sem nota interna e sem outro paciente");
        dto.Mensagens[0].DoPaciente.Should().BeTrue();
        dto.Mensagens[0].Autor.Should().BeNull();
        dto.Mensagens[1].Autor.Should().Be("ATENDENTE TESTE");
        dto.Mensagens[1].Aviso.Should().BeNull("mensagem escrita por alguém da equipe não é aviso automático");
        dto.Mensagens[2].Aviso.Should().NotBeNull();
        dto.Mensagens[2].Aviso!.DestePedido.Should().BeFalse("o aviso é do outro pedido, não deste agendamento do SER");
        dto.Mensagens[2].Aviso!.Finalidade.Should().Be("ConfirmacaoAgendamento");
        dto.Mensagens[2].Aviso!.DataAgendada.Should().NotBeNull();
    }

    [Fact]
    public async Task Sisreg_traz_o_aviso_com_recibos_e_a_resposta_do_paciente()
    {
        await using var db = fixture.CriarDbContext();
        var paciente = Guid.NewGuid();
        var exame = await SeedSolicitacao.CriarAsync(db, paciente, dataAgendada: DateTime.UtcNow.AddDays(10),
            confirmacao: StatusConfirmacaoAgendamento.Confirmada);
        var s = await SolicitacaoAsync(db, paciente, SistemaRegulacao.Sisreg, DateTime.UtcNow.AddDays(-3),
            espinhaId: exame.SolicitacaoId);

        db.ComunicacoesPaciente.Add(new ComunicacaoPaciente
        {
            Id = Guid.CreateVersion7(),
            Tipo = TipoAgendamento.Exame,
            Finalidade = FinalidadeComunicacao.ConfirmacaoAgendamento,
            SolicitacaoId = exame.SolicitacaoId,
            PacienteId = paciente,
            Telefone = "5521999990000",
            Status = StatusComunicacao.Lida,
            CriadoEm = DateTime.UtcNow.AddDays(-2),
            EnviadoEm = DateTime.UtcNow.AddDays(-2),
            EntregueEm = DateTime.UtcNow.AddDays(-2),
            LidoEm = DateTime.UtcNow.AddDays(-1),
        });
        await db.SaveChangesAsync();

        var dto = await Servico(db).ObterAsync(s.Id, CancellationToken.None);

        dto.DestinoComAvisoAutomatico.Should().BeTrue();
        dto.Avisos.Should().ContainSingle(a => a.Status == "Lida" && a.LidoEm != null);
        dto.Confirmacao.Should().Be("Confirmada");
        dto.Agendamento.Should().NotBeNull();
        dto.Agendamento!.DataHora.Should().NotBeNull();
        dto.TelefoneConfirmado.Should().BeNull();
    }

    [Fact]
    public async Task Sisreg_sem_aviso_nao_inventa_confirmacao_pendente()
    {
        await using var db = fixture.CriarDbContext();
        var paciente = Guid.NewGuid();
        var exame = await SeedSolicitacao.CriarAsync(db, paciente, dataAgendada: DateTime.UtcNow.AddDays(10));
        var s = await SolicitacaoAsync(db, paciente, SistemaRegulacao.Sisreg, DateTime.UtcNow.AddDays(-3),
            espinhaId: exame.SolicitacaoId);

        var dto = await Servico(db).ObterAsync(s.Id, CancellationToken.None);

        dto.Avisos.Should().BeEmpty();
        dto.Confirmacao.Should().BeNull("Pendente sem aviso é só o valor inicial da coluna");
    }

    [Fact]
    public void Texto_longo_e_cortado() =>
        RegulacaoContatoPacienteService.Cortar(new string('a', 2000))!.Length
            .Should().Be(RegulacaoContatoPacienteService.MaxTexto + 1);
}
