using Microsoft.Extensions.Logging;
using SMSMais.Core.Cidadao.Push;
using SMSMais.Core.Cidadao.Push.Dtos;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Translado;

/// <summary>
/// Os avisos do transporte pelo push do app do cidadão (ADR-0070). O toque abre a tela do Transporte,
/// onde estão os botões ("Sim, já estou disponível" / "Não, ainda não terminei" e as estrelas); o texto
/// só chama a pessoa para lá — nada de unidade, procedimento ou horário, porque aparece na tela
/// bloqueada. Paciente sem o app instalado, ou push desligado em Integrações, não é erro: o aviso
/// continua esperando no app e no PWA.
/// </summary>
public sealed class AvisoAoPacientePorPush(IPushCidadaoService push, ILogger<AvisoAoPacientePorPush> logger)
    : IAvisoAoPacienteTransporte
{
    internal const string RotaTransporte = "/transporte";

    public Task<bool> PerguntarSeDisponivelAsync(
        Guid pacienteId, string primeiroNome, Guid sessaoId, CancellationToken ct = default) =>
        EnviarAsync(pacienteId, sessaoId,
            "Você já está disponível para retorno?",
            $"{Saudacao(primeiroNome)} Toque para responder se já terminou ou se ainda não.", ct);

    public Task<bool> PedirAvaliacaoAsync(
        Guid pacienteId, string primeiroNome, Guid sessaoId, TrechoViagem trecho, string? motorista,
        CancellationToken ct = default) =>
        EnviarAsync(pacienteId, sessaoId,
            "Como foi o motorista?",
            trecho == TrechoViagem.Resgate
                ? $"{Saudacao(primeiroNome)} Você chegou em casa. Toque para avaliar a viagem com até 5 estrelas."
                : $"{Saudacao(primeiroNome)} Você chegou ao seu destino. Toque para avaliar a viagem com até 5 estrelas.",
            ct);

    internal static string Saudacao(string? primeiroNome) =>
        string.IsNullOrWhiteSpace(primeiroNome) ? "Olá!" : $"Olá, {primeiroNome.Trim()}!";

    private async Task<bool> EnviarAsync(
        Guid pacienteId, Guid sessaoId, string titulo, string mensagem, CancellationToken ct)
    {
        try
        {
            var envio = await push.EnviarDoSistemaAsync(
                pacienteId, new EnviarNotificacaoAppRequest(titulo, mensagem, RotaTransporte),
                PushCidadaoService.OrigemTransporte, ct);
            return envio.Entregues > 0;
        }
        catch (Exception ex) when (ex is ConflitoException or ValidacaoException)
        {
            // Sem aparelho com notificação ativa, ou push não configurado: o aviso fica só no app/PWA.
            logger.LogDebug("Transporte: aviso da sessão {Sessao} sem push ({Motivo}).", sessaoId, ex.Message);
            return false;
        }
    }
}
