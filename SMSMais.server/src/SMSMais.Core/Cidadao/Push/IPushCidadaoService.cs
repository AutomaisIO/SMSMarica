using SMSMais.Core.Cidadao.Push.Dtos;

namespace SMSMais.Core.Cidadao.Push;

/// <summary>
/// Notificação (push) no app do cidadão, pelo Firebase Cloud Messaging. O aparelho pertence à
/// SESSÃO (<c>cidadao_sessao</c>), não ao paciente: o envio vai a todas as sessões ativas que têm
/// token, e logout/revogação apagam o token junto. Sem dado de saúde no texto — ele aparece na tela
/// bloqueada e passa pelos servidores do Google.
/// </summary>
public interface IPushCidadaoService
{
    /// <summary>
    /// Grava o token do aparelho na sessão do <paramref name="sessaoId"/> (jti). Se o mesmo token
    /// estiver em outra sessão, sai de lá: o aparelho trocou de login e não pode receber em dobro.
    /// </summary>
    Task RegistrarAparelhoAsync(
        Guid pacienteId, Guid sessaoId, RegistrarDispositivoRequest request, CancellationToken ct = default);

    /// <summary>Aparelhos com notificação ativa + as 20 notificações mais recentes do paciente.</summary>
    Task<AppCidadaoStatusDto> ObterStatusAsync(Guid pacienteId, CancellationToken ct = default);

    /// <summary>
    /// Manda a notificação a cada aparelho do paciente, em sequência, e grava o histórico (mesmo
    /// que nenhum aceite). Sem credencial ativa → <c>ValidacaoException("push.nao_configurado")</c>;
    /// credencial que não decifra neste servidor → <c>ValidacaoException("push.credencial_ilegivel")</c>;
    /// sem aparelho → <c>ConflitoException("push.sem_aparelho")</c>; nesses casos nada é gravado.
    /// </summary>
    Task<EnvioNotificacaoAppDto> EnviarAsync(
        Guid pacienteId, EnviarNotificacaoAppRequest request, CancellationToken ct = default);

    /// <summary>
    /// Confere a credencial do Firebase: pede um access token novo e faz um envio de validação
    /// (<c>validate_only</c>, para um tópico — não chega a ninguém). Nunca lança: o desfecho vem no DTO.
    /// </summary>
    Task<TesteCredencialFcmDto> TestarCredencialAsync(CancellationToken ct = default);
}
