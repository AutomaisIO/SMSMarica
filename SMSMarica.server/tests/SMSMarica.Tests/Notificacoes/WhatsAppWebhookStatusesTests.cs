using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SMSMarica.Core.Conversas;
using SMSMarica.Core.Notificacoes.Comunicacao;
using SMSMarica.Core.Notificacoes.WhatsApp;
using SMSMarica.Core.Notificacoes.WhatsApp.Manipuladores;
using SMSMarica.Core.Pacientes;
using SMSMarica.Data;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Enums;
using SMSMarica.Data.Entities.Notificacoes;
using SMSMarica.Tests.Infraestrutura;

namespace SMSMarica.Tests.Notificacoes;

/// <summary>
/// Processamento de <c>value.statuses</c> do webhook da Meta (recibos de entrega):
/// promoção monotônica Enviada→Entregue→Lida, falha com ErroMeta, espelho na
/// ComunicacaoPaciente (re-enfileira retentável, terminal em erro permanente) e idempotência.
/// Payloads no formato real da Cloud API.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class WhatsAppWebhookStatusesTests(PostgresFixture fixture)
{
    private static WhatsAppWebhookService CriarService(SmsMaricaDbContext db) => new(
        db,
        Substitute.For<IPacientesService>(),
        Substitute.For<IConversaNotificador>(),
        [],
        Options.Create(new ComunicacaoPacienteOptions()),
        NullLogger<WhatsAppWebhookService>.Instance);

    private static string PayloadStatus(string wamid, string status, string? erroJson = null)
    {
        var erros = erroJson is null ? string.Empty : $",\"errors\":[{erroJson}]";
        return "{\"entry\":[{\"changes\":[{\"value\":{\"messaging_product\":\"whatsapp\",\"statuses\":[" +
               $"{{\"id\":\"{wamid}\",\"status\":\"{status}\",\"timestamp\":\"1720000000\",\"recipient_id\":\"5521999990000\"{erros}}}" +
               "]}}]}]}";
    }

    private async Task<(MensagemWhatsApp Msg, ComunicacaoPaciente Notif)> SeedEnvioAsync(
        SmsMaricaDbContext db, int tentativas = 1)
    {
        var wamid = $"wamid.TEST.{Guid.NewGuid():N}";
        var msg = new MensagemWhatsApp
        {
            Id = Guid.NewGuid(),
            Telefone = SeedSolicitacao.TelefoneAleatorio(),
            Template = "confirma_exame",
            Direcao = DirecaoMensagem.Saida,
            Status = StatusMensagemWhatsApp.Enviada,
            WaMessageId = wamid,
            OcorridoEm = DateTime.UtcNow,
            CriadoEm = DateTime.UtcNow,
        };
        var notif = new ComunicacaoPaciente
        {
            Id = Guid.NewGuid(),
            Tipo = TipoAgendamento.Exame,
            PacienteId = Guid.NewGuid(),
            Status = StatusComunicacao.Enviada,
            MensagemWhatsAppId = msg.Id,
            Tentativas = tentativas,
            EnviadoEm = DateTime.UtcNow,
            CriadoEm = DateTime.UtcNow,
        };
        db.MensagensWhatsApp.Add(msg);
        db.ComunicacoesPaciente.Add(notif);
        await db.SaveChangesAsync();
        return (msg, notif);
    }

    [Fact]
    public async Task Delivered_promove_para_entregue_e_espelha_na_notificacao()
    {
        await using var db = fixture.CriarDbContext();
        var (msg, notif) = await SeedEnvioAsync(db);

        await CriarService(db).ProcessarAsync(PayloadStatus(msg.WaMessageId!, "delivered"));

        await using var leitura = fixture.CriarDbContext();
        var m = await leitura.MensagensWhatsApp.SingleAsync(x => x.Id == msg.Id);
        var n = await leitura.ComunicacoesPaciente.SingleAsync(x => x.Id == notif.Id);
        Assert.Equal(StatusMensagemWhatsApp.Entregue, m.Status);
        Assert.Equal(StatusComunicacao.Entregue, n.Status);
        Assert.NotNull(n.EntregueEm);
    }

    [Fact]
    public async Task Read_depois_delivered_nao_regride()
    {
        await using var db = fixture.CriarDbContext();
        var (msg, notif) = await SeedEnvioAsync(db);
        var service = CriarService(db);

        await service.ProcessarAsync(PayloadStatus(msg.WaMessageId!, "read"));
        await service.ProcessarAsync(PayloadStatus(msg.WaMessageId!, "delivered")); // recibo atrasado

        await using var leitura = fixture.CriarDbContext();
        var m = await leitura.MensagensWhatsApp.SingleAsync(x => x.Id == msg.Id);
        var n = await leitura.ComunicacoesPaciente.SingleAsync(x => x.Id == notif.Id);
        Assert.Equal(StatusMensagemWhatsApp.Lida, m.Status); // monotônico — Lida não volta a Entregue
        Assert.Equal(StatusComunicacao.Lida, n.Status);
        Assert.NotNull(n.LidoEm);
    }

    [Fact]
    public async Task Failed_retentavel_reenfileira_com_backoff()
    {
        await using var db = fixture.CriarDbContext();
        var (msg, notif) = await SeedEnvioAsync(db, tentativas: 1);
        var erro = """{"code":131047,"title":"Re-engagement message","error_data":{"details":"janela expirada"}}""";

        await CriarService(db).ProcessarAsync(PayloadStatus(msg.WaMessageId!, "failed", erro));

        await using var leitura = fixture.CriarDbContext();
        var m = await leitura.MensagensWhatsApp.SingleAsync(x => x.Id == msg.Id);
        var n = await leitura.ComunicacoesPaciente.SingleAsync(x => x.Id == notif.Id);
        Assert.Equal(StatusMensagemWhatsApp.Falha, m.Status);
        Assert.Contains("131047", m.ErroMeta);
        Assert.Equal(StatusComunicacao.Pendente, n.Status); // volta pra fila
        Assert.NotNull(n.ProximaTentativaEm);                          // worker reenvia com link novo
    }

    [Fact]
    public async Task Failed_permanente_131026_marca_falha_terminal()
    {
        await using var db = fixture.CriarDbContext();
        var (msg, notif) = await SeedEnvioAsync(db);
        var erro = """{"code":131026,"title":"Message undeliverable"}""";

        await CriarService(db).ProcessarAsync(PayloadStatus(msg.WaMessageId!, "failed", erro));

        await using var leitura = fixture.CriarDbContext();
        var n = await leitura.ComunicacoesPaciente.SingleAsync(x => x.Id == notif.Id);
        Assert.Equal(StatusComunicacao.Falha, n.Status);
        Assert.Null(n.ProximaTentativaEm); // terminal — número não é WhatsApp
        Assert.Contains("131026", n.MotivoFalha);
    }

    [Fact]
    public async Task Status_de_wamid_desconhecido_e_ignorado()
    {
        await using var db = fixture.CriarDbContext();
        // Não deve lançar nem criar nada.
        await CriarService(db).ProcessarAsync(PayloadStatus($"wamid.INEXISTENTE.{Guid.NewGuid():N}", "delivered"));
    }
}
