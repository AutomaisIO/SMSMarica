using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Pacientes;
using SMSMarica.Data;
using SMSMarica.Data.Entities;

namespace SMSMarica.Core.Notificacoes.WhatsApp;

public sealed class WhatsAppNotificador(
    SmsMaricaDbContext db,
    IWhatsAppCliente cliente,
    IPacientesService pacientes,
    ILogger<WhatsAppNotificador> logger) : IWhatsAppNotificador
{
    public async Task PerguntarAcompanhanteAsync(Guid sessaoId, CancellationToken ct = default)
    {
        var (sessao, trat) = await CarregarAsync(sessaoId, ct);
        var (fone, primeiroNome) = await ObterContatoAsync(trat.PacienteId, ct);
        if (fone is null) { logger.LogWarning("Acompanhante: paciente {Id} sem telefone.", trat.PacienteId); return; }

        var texto = $"Olá {primeiroNome}! Sobre o seu transporte de saúde do dia "
            + $"{sessao.DataPrevista:dd/MM}: você vai com acompanhante? "
            + "Responda *1* para SIM ou *2* para NÃO.";
        await cliente.EnviarTextoAsync(fone, texto, trat.PacienteId, ct);
    }

    public async Task AvisarColetaAsync(Guid sessaoId, CancellationToken ct = default)
    {
        var (sessao, trat) = await CarregarAsync(sessaoId, ct);
        var (fone, primeiroNome) = await ObterContatoAsync(trat.PacienteId, ct);
        if (fone is null) { logger.LogWarning("Aviso de coleta: paciente {Id} sem telefone.", trat.PacienteId); return; }

        var hora = sessao.HoraPrevistaBusca?.ToString("HH:mm") ?? "a confirmar";
        var texto = $"Olá {primeiroNome}! Seu transporte de saúde está agendado para "
            + $"{sessao.DataPrevista:dd/MM} às {hora}. Por favor, esteja pronto(a) no horário. 🚐";
        await cliente.EnviarTextoAsync(fone, texto, trat.PacienteId, ct);
    }

    private async Task<(SessaoDeTratamento Sessao, Tratamento Tratamento)> CarregarAsync(Guid sessaoId, CancellationToken ct)
    {
        var sessao = await db.Sessoes.AsNoTracking().Include(s => s.Tratamento)
            .FirstOrDefaultAsync(s => s.Id == sessaoId, ct)
            ?? throw new NaoEncontradoException(nameof(SessaoDeTratamento), sessaoId);
        var trat = sessao.Tratamento
            ?? throw new ConflitoException("sessao.sem_tratamento", "Sessão sem tratamento associado.");
        return (sessao, trat);
    }

    private async Task<(string? Fone, string PrimeiroNome)> ObterContatoAsync(Guid pacienteId, CancellationToken ct)
    {
        try
        {
            var p = await pacientes.ObterPorIdAsync(pacienteId, ct);
            var fone = p.TelefoneCelular ?? p.TelefonePrincipal ?? p.TelefoneResidencial;
            var primeiro = string.IsNullOrWhiteSpace(p.NomeCompleto) ? "" : p.NomeCompleto.Split(' ')[0];
            return (string.IsNullOrWhiteSpace(fone) ? null : fone, primeiro);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Não foi possível obter contato do paciente {Id}.", pacienteId);
            return (null, "");
        }
    }
}
