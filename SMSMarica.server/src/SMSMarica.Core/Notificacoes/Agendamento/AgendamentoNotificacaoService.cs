using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMSMarica.Core.Cidadao;
using SMSMarica.Core.Common.Tempo;
using SMSMarica.Core.Conversas;
using SMSMarica.Core.Notificacoes.WhatsApp;
using SMSMarica.Core.Pacientes;
using SMSMarica.Data;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Notificacoes.Agendamento;

/// <summary>
/// Fila de notificação WhatsApp de agendamentos. O import (SISREG hoje; API depois) só
/// ENFILEIRA (<see cref="EnfileirarParaExameAsync"/>); o worker
/// (<see cref="NotificadorAgendamentoService"/>) processa com ritmo e retentativas
/// (<see cref="ProcessarTentativaEnvioAsync"/>): valida celular BR, gera magic link e envia
/// o template com botão de confirmação (URL dinâmica) + quick reply de cancelamento.
/// </summary>
public interface IAgendamentoNotificacaoService
{
    /// <summary>Enfileira a notificação do exame se a DataAgendada for futura (senão não faz nada).
    /// NÃO salva — participa do SaveChanges do chamador (import).</summary>
    void EnfileirarParaExame(SolicitacaoExame solicitacao);

    /// <summary>Processa UMA tentativa de envio. Nunca lança — falha vira backoff/estado terminal.</summary>
    Task ProcessarTentativaEnvioAsync(Guid notificacaoId, CancellationToken ct = default);
}

public sealed class AgendamentoNotificacaoService(
    SmsMaricaDbContext db,
    IPacientesService pacientes,
    ICidadaoLoginLinkService loginLinks,
    IWhatsAppCliente whatsApp,
    IOptions<NotificadorAgendamentoOptions> options,
    ILogger<AgendamentoNotificacaoService> logger) : IAgendamentoNotificacaoService
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Códigos de erro da Meta que não adianta retentar (número inexistente/não autorizado).</summary>
    private static readonly string[] ErrosMetaPermanentes = ["131026", "131030"];

    public void EnfileirarParaExame(SolicitacaoExame solicitacao)
    {
        if (solicitacao.DataAgendada is not { } da || da <= DateTime.UtcNow) return;

        db.AgendamentoNotificacoes.Add(new AgendamentoNotificacao
        {
            Id = Guid.CreateVersion7(),
            Tipo = TipoAgendamento.Exame,
            SolicitacaoExameId = solicitacao.Id,
            PacienteId = solicitacao.PacienteId,
            Status = StatusNotificacaoAgendamento.Pendente,
            ProximaTentativaEm = DateTime.UtcNow,
            CriadoEm = DateTime.UtcNow,
        });
    }

    public async Task ProcessarTentativaEnvioAsync(Guid notificacaoId, CancellationToken ct = default)
    {
        var n = await db.AgendamentoNotificacoes
            .Include(x => x.SolicitacaoExame!).ThenInclude(s => s.TipoExame)
            .Include(x => x.SolicitacaoExame!).ThenInclude(s => s.Unidade)
            .FirstOrDefaultAsync(x => x.Id == notificacaoId, ct);
        if (n is null || n.Status != StatusNotificacaoAgendamento.Pendente || n.ProximaTentativaEm is null)
            return;

        n.Tentativas++;
        n.UltimaTentativaEm = DateTime.UtcNow;
        n.AtualizadoEm = DateTime.UtcNow;

        try
        {
            var s = n.SolicitacaoExame;

            // Terminal: solicitação sumiu/cancelada/excluída, exame já passou ou paciente já respondeu.
            if (s is null || s.ExcluidoEm is not null || s.Status == StatusSolicitacaoExame.Cancelada)
            {
                Terminal(n, StatusNotificacaoAgendamento.Falha, "Solicitação excluída ou cancelada antes do envio.");
            }
            else if (s.DataAgendada is not { } dataAgendada || dataAgendada <= DateTime.UtcNow)
            {
                Terminal(n, StatusNotificacaoAgendamento.Falha, "Exame sem data futura no momento do envio.");
            }
            else if (s.StatusConfirmacao != StatusConfirmacaoAgendamento.Pendente)
            {
                Terminal(n, StatusNotificacaoAgendamento.Falha, "Paciente já respondeu por outro canal.");
            }
            else
            {
                await EnviarAsync(n, s, dataAgendada, ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao notificar agendamento {Id} (tentativa {N}).", n.Id, n.Tentativas);
            ReagendarOuFalhar(n, ex.Message);
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task EnviarAsync(AgendamentoNotificacao n, SolicitacaoExame s, DateTime dataAgendada, CancellationToken ct)
    {
        var paciente = await pacientes.ObterPorIdAsync(n.PacienteId, ct);
        var cpf = SoDigitos(paciente.Cpf);

        // Telefone: contato validado por OTP (âncora por CPF) > celular do cadastro > principal.
        string? telefone = null;
        if (cpf.Length == 11)
            telefone = await db.ContatosValidados.AsNoTracking()
                .Where(c => c.Cpf == cpf).Select(c => c.Numero).FirstOrDefaultAsync(ct);
        telefone ??= new[] { paciente.TelefoneCelular, paciente.TelefonePrincipal }
            .FirstOrDefault(TelefoneWhatsApp.EhCelularBr);

        if (!TelefoneWhatsApp.EhCelularBr(telefone))
        {
            Terminal(n, StatusNotificacaoAgendamento.SemTelefoneValido, "Paciente sem número de celular válido.");
            return;
        }
        n.Telefone = TelefoneWhatsApp.Canonizar(telefone!);

        // Magic link novo a cada tentativa (o anterior simplesmente expira sem uso).
        var link = await loginLinks.GerarParaSolicitacaoAsync(s.Id, "/agendados/exames", ct);
        n.LoginLinkId = link.Token;

        var opts = options.Value;
        var local = FusoBrasilia.ParaExibicao(dataAgendada);
        var parametros = new[]
        {
            PrimeiroNome(paciente.NomeCompleto),
            s.TipoExame?.Nome ?? "exame",
            local.ToString("dd/MM/yyyy", PtBr),
            s.Unidade?.Nome ?? "unidade de saúde",
            local.ToString("HH:mm", PtBr),
        };
        var botoes = new BotaoTemplateWhatsApp[]
        {
            new(TipoBotaoTemplate.Url, link.Token.ToString()),
            new(TipoBotaoTemplate.QuickReply, $"confirma:{s.Id}"),
        };

        var resultado = await whatsApp.EnviarTemplateComBotoesAsync(
            n.Telefone, opts.TemplateExame, opts.Idioma, parametros, botoes, pacienteId: n.PacienteId, ct: ct);

        if (resultado.Ok)
        {
            n.Status = StatusNotificacaoAgendamento.Enviada;
            n.EnviadoEm = DateTime.UtcNow;
            n.MotivoFalha = null;
            n.ProximaTentativaEm = null; // recibos (entrega/leitura/falha) chegam pelo webhook
            if (resultado.WaMessageId is { } wamid)
                n.MensagemWhatsAppId = await db.MensagensWhatsApp.AsNoTracking()
                    .Where(m => m.WaMessageId == wamid).Select(m => (Guid?)m.Id).FirstOrDefaultAsync(ct);
        }
        else if (ErrosMetaPermanentes.Any(c => resultado.Erro?.Contains($"({c})") == true))
        {
            Terminal(n, StatusNotificacaoAgendamento.Falha, resultado.Erro);
        }
        else
        {
            ReagendarOuFalhar(n, resultado.Erro);
        }
    }

    private void ReagendarOuFalhar(AgendamentoNotificacao n, string? erro)
    {
        n.MotivoFalha = Truncar(erro);
        if (n.Tentativas >= Math.Max(1, options.Value.MaxTentativas))
        {
            n.Status = StatusNotificacaoAgendamento.Falha;
            n.ProximaTentativaEm = null;
            return;
        }
        // Backoff exponencial: 5min, 10min, 20min, 40min...
        n.ProximaTentativaEm = DateTime.UtcNow.AddMinutes(5 * Math.Pow(2, n.Tentativas - 1));
    }

    private static void Terminal(AgendamentoNotificacao n, StatusNotificacaoAgendamento status, string? motivo)
    {
        n.Status = status;
        n.MotivoFalha = Truncar(motivo);
        n.ProximaTentativaEm = null;
    }

    private static string PrimeiroNome(string? nome)
    {
        var partes = (nome ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return partes.Length == 0 ? "Paciente" : PtBr.TextInfo.ToTitleCase(partes[0].ToLowerInvariant());
    }

    private static string SoDigitos(string? v) =>
        string.IsNullOrEmpty(v) ? string.Empty : new string([.. v.Where(char.IsDigit)]);

    private static string? Truncar(string? s) => s is null ? null : s.Length <= 1000 ? s : s[..1000];
}
