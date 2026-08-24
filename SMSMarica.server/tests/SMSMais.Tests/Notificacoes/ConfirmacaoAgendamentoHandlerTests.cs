using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SMSMais.Core.Notificacoes.WhatsApp;
using SMSMais.Core.Notificacoes.WhatsApp.Manipuladores;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Conversas;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Notificacoes;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Notificacoes;

/// <summary>
/// Máquina de estados do cancelamento pelo WhatsApp (quick reply → confirmar → motivo),
/// incluindo o "não-sequestro": texto livre sem estado ativo passa reto pelo handler.
/// O handler não chama SaveChanges (contrato do webhook) — os testes commitam após TratarAsync.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class ConfirmacaoAgendamentoHandlerTests(PostgresFixture fixture)
{
    private static IWhatsAppCliente CriarWhatsAppMock()
    {
        var cliente = Substitute.For<IWhatsAppCliente>();
        cliente.EnviarTextoAsync(null!, null!)
            .ReturnsForAnyArgs(new EnvioWhatsAppResultado(true, $"wamid.OUT.{Guid.NewGuid():N}", null));
        cliente.EnviarInterativoBotoesAsync(null!, null!, null!)
            .ReturnsForAnyArgs(new EnvioWhatsAppResultado(true, $"wamid.OUT.{Guid.NewGuid():N}", null));
        return cliente;
    }

    private static ManipuladorContexto Contexto(
        string telefone, Guid? pacienteId, string? texto = null,
        string? botaoPayload = null, string? interativoReplyId = null)
    {
        var conversa = new Conversa
        {
            Id = Guid.NewGuid(),
            Canal = CanalConversa.WhatsApp,
            TelefoneCanonical = telefone,
            PacienteId = pacienteId,
            Status = StatusConversa.Aberta,
            PrimeiroContatoEm = DateTime.UtcNow,
            CriadoEm = DateTime.UtcNow,
        };
        var msg = new MensagemWhatsApp
        {
            Id = Guid.NewGuid(),
            Telefone = telefone,
            Direcao = DirecaoMensagem.Entrada,
            Conteudo = texto,
            Status = StatusMensagemWhatsApp.Recebida,
            OcorridoEm = DateTime.UtcNow,
            CriadoEm = DateTime.UtcNow,
        };
        return new ManipuladorContexto(conversa, msg, texto, pacienteId, botaoPayload, interativoReplyId);
    }

    private async Task<(ExameImagem Solic, ComunicacaoPaciente Notif)> SeedAsync(SmsMaisDbContext db)
    {
        var solic = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid(), dataAgendada: DateTime.UtcNow.AddDays(3));
        var notif = new ComunicacaoPaciente
        {
            Id = Guid.NewGuid(),
            Tipo = TipoAgendamento.Exame,
            SolicitacaoId = solic.SolicitacaoId,
            PacienteId = solic.Solicitacao!.PacienteId,
            Status = StatusComunicacao.Enviada,
            CriadoEm = DateTime.UtcNow,
        };
        db.ComunicacoesPaciente.Add(notif);
        await db.SaveChangesAsync();
        return (solic, notif);
    }

    [Fact]
    public async Task Fluxo_completo_quick_reply_ate_o_motivo_cancela_com_motivo()
    {
        await using var db = fixture.CriarDbContext();
        var (solic, _) = await SeedAsync(db);
        var telefone = SeedSolicitacao.TelefoneAleatorio();
        var whatsApp = CriarWhatsAppMock();
        var handler = new ConfirmacaoAgendamentoWhatsAppHandler(db, whatsApp, NullLogger<ConfirmacaoAgendamentoWhatsAppHandler>.Instance);

        // 1. "Não poderei comparecer" (quick reply do template) → pergunta interativa + estado.
        await handler.TratarAsync(Contexto(telefone, solic.Solicitacao!.PacienteId, botaoPayload: $"confirma:{solic.SolicitacaoId}"), default);
        await db.SaveChangesAsync();
        var estado = await db.AgendamentoConfirmacaoEstados.SingleAsync(e => e.TelefoneCanonical == telefone);
        Assert.Equal(EtapaConfirmacaoAgendamento.AguardandoConfirmacaoCancelamento, estado.Etapa);
        await whatsApp.ReceivedWithAnyArgs(1).EnviarInterativoBotoesAsync(null!, null!, null!);

        // 2. "Sim, cancelar" → aguarda o motivo.
        await handler.TratarAsync(Contexto(telefone, solic.Solicitacao!.PacienteId, interativoReplyId: $"cancela_sim:{solic.SolicitacaoId}"), default);
        await db.SaveChangesAsync();
        estado = await db.AgendamentoConfirmacaoEstados.SingleAsync(e => e.TelefoneCanonical == telefone);
        Assert.Equal(EtapaConfirmacaoAgendamento.AguardandoMotivo, estado.Etapa);

        // 3. Texto livre com estado ativo = motivo → cancela e limpa o estado.
        await handler.TratarAsync(Contexto(telefone, solic.Solicitacao!.PacienteId, texto: "vou estar viajando"), default);
        await db.SaveChangesAsync();
        var s = await db.Solicitacoes.AsNoTracking().SingleAsync(x => x.Id == solic.SolicitacaoId);
        Assert.Equal(StatusConfirmacaoAgendamento.Cancelada, s.StatusConfirmacao);
        Assert.Equal("vou estar viajando", s.MotivoCancelamentoPaciente);
        Assert.False(await db.AgendamentoConfirmacaoEstados.AnyAsync(e => e.TelefoneCanonical == telefone));
    }

    [Fact]
    public async Task Cancela_nao_confirma_presenca_e_limpa_estado()
    {
        await using var db = fixture.CriarDbContext();
        var (solic, notif) = await SeedAsync(db);
        var telefone = SeedSolicitacao.TelefoneAleatorio();
        db.AgendamentoConfirmacaoEstados.Add(new AgendamentoConfirmacaoEstado
        {
            Id = Guid.NewGuid(),
            TelefoneCanonical = telefone,
            ComunicacaoPacienteId = notif.Id,
            Etapa = EtapaConfirmacaoAgendamento.AguardandoConfirmacaoCancelamento,
            ExpiraEm = DateTime.UtcNow.AddHours(48),
            CriadoEm = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        var handler = new ConfirmacaoAgendamentoWhatsAppHandler(db, CriarWhatsAppMock(), NullLogger<ConfirmacaoAgendamentoWhatsAppHandler>.Instance);

        await handler.TratarAsync(Contexto(telefone, solic.Solicitacao!.PacienteId, interativoReplyId: $"cancela_nao:{solic.SolicitacaoId}"), default);
        await db.SaveChangesAsync();

        var s = await db.Solicitacoes.AsNoTracking().SingleAsync(x => x.Id == solic.SolicitacaoId);
        Assert.Equal(StatusConfirmacaoAgendamento.Confirmada, s.StatusConfirmacao);
        Assert.Equal("whatsapp-quickreply", s.ConfirmadoCanal);
        Assert.False(await db.AgendamentoConfirmacaoEstados.AnyAsync(e => e.TelefoneCanonical == telefone));
    }

    [Fact]
    public async Task Texto_livre_sem_estado_ativo_passa_reto()
    {
        await using var db = fixture.CriarDbContext();
        var (solic, _) = await SeedAsync(db);
        var telefone = SeedSolicitacao.TelefoneAleatorio();
        var whatsApp = CriarWhatsAppMock();
        var handler = new ConfirmacaoAgendamentoWhatsAppHandler(db, whatsApp, NullLogger<ConfirmacaoAgendamentoWhatsAppHandler>.Instance);

        await handler.TratarAsync(Contexto(telefone, solic.Solicitacao!.PacienteId, texto: "bom dia, uma dúvida"), default);
        await db.SaveChangesAsync();

        var s = await db.Solicitacoes.AsNoTracking().SingleAsync(x => x.Id == solic.SolicitacaoId);
        Assert.Equal(StatusConfirmacaoAgendamento.Pendente, s.StatusConfirmacao); // nada mudou
        await whatsApp.DidNotReceiveWithAnyArgs().EnviarTextoAsync(null!, null!); // não sequestrou o chat
    }

    [Fact]
    public async Task Estado_expirado_e_ignorado_e_removido()
    {
        await using var db = fixture.CriarDbContext();
        var (solic, notif) = await SeedAsync(db);
        var telefone = SeedSolicitacao.TelefoneAleatorio();
        db.AgendamentoConfirmacaoEstados.Add(new AgendamentoConfirmacaoEstado
        {
            Id = Guid.NewGuid(),
            TelefoneCanonical = telefone,
            ComunicacaoPacienteId = notif.Id,
            Etapa = EtapaConfirmacaoAgendamento.AguardandoMotivo,
            ExpiraEm = DateTime.UtcNow.AddHours(-1), // vencido
            CriadoEm = DateTime.UtcNow.AddDays(-3),
        });
        await db.SaveChangesAsync();
        var handler = new ConfirmacaoAgendamentoWhatsAppHandler(db, CriarWhatsAppMock(), NullLogger<ConfirmacaoAgendamentoWhatsAppHandler>.Instance);

        await handler.TratarAsync(Contexto(telefone, solic.Solicitacao!.PacienteId, texto: "isso não é um motivo"), default);
        await db.SaveChangesAsync();

        var s = await db.Solicitacoes.AsNoTracking().SingleAsync(x => x.Id == solic.SolicitacaoId);
        Assert.Equal(StatusConfirmacaoAgendamento.Pendente, s.StatusConfirmacao); // expirado não cancela
        Assert.False(await db.AgendamentoConfirmacaoEstados.AnyAsync(e => e.TelefoneCanonical == telefone));
    }
}
