using Microsoft.Extensions.Logging;
using SMSMais.Core.Alertas;
using SMSMais.Core.Notificacoes.WhatsApp;

namespace SMSMais.Core.Notificacoes.Sincronismo;

/// <summary>
/// Avisa por WhatsApp quem opera as integrações quando um sincronismo falha ou precisa de gente —
/// CAPTCHA, credencial derrubada, unidade com erro, rodada interrompida.
///
/// <para><b>Por que existe:</b> os motores do SISREG, SER e SERNIT rodam de madrugada e sozinhos.
/// Quando o SISREG pede CAPTCHA, o motor para e <b>ninguém fica sabendo</b> até alguém abrir a tela
/// e reparar que a importação do dia não aconteceu — o que na prática significa descobrir depois
/// que o paciente já perdeu a consulta.</para>
///
/// <para><b>Falha vai pelo aviso da plataforma</b> (<see cref="IAlertaPlataforma"/>, template
/// <c>erro_plataforma</c>). Antes ia por texto livre com "reabertura" pelo template de verificação
/// cadastral — e fora da janela de 24h a Meta aceita o texto e só descarta depois, então o aviso
/// "saía" e não chegava.</para>
///
/// <para><b>Quem recebe:</b> os telefones de Sistema → Avisos no celular, e SÓ eles
/// (<see cref="IAlertaDestinatarios"/>). Até 24/09/2026 cada integração tinha também a sua lista,
/// na tela dela — o operador cadastrou o celular na do SISREG e não recebeu nada quando o robô
/// parou. Uma configuração só para erro e falha da plataforma.</para>
/// </summary>
public interface INotificadorSincronismo
{
    /// <summary>
    /// Manda o aviso. Nunca lança: falhar em avisar não pode derrubar o motor que estava tentando
    /// trabalhar — o erro real já está no log.
    /// </summary>
    /// <param name="provedor">Chave da integração: <c>sisreg</c>, <c>ser</c>, <c>sernit</c>.</param>
    /// <param name="chaveRepeticao">
    /// Tipo do aviso. Sem <c>:</c> (ex.: <c>captcha</c>) vira fonte própria na tela, com freio
    /// próprio — um CAPTCHA não pode ficar preso atrás do freio de uma unidade que falhou dez
    /// minutos antes. Com <c>:</c> (ex.: <c>unidade:{id}</c>) entra na fonte do provedor.
    /// </param>
    /// <param name="informativo">
    /// Progresso, não falha ("Iniciando X", "OK X", rodada limpa): só texto, para os mesmos
    /// destinatários. Não gasta template — se a janela de 24h estiver fechada, não chega, e tudo bem.
    /// </param>
    Task NotificarAsync(
        string provedor, string titulo, string detalhe,
        string? chaveRepeticao = null, CancellationToken ct = default, bool informativo = false);
}

public sealed class NotificadorSincronismo(
    IAlertaDestinatarios destinatarios,
    IWhatsAppCliente whatsApp,
    IAlertaPlataforma alerta,
    ILogger<NotificadorSincronismo> logger) : INotificadorSincronismo
{
    public async Task NotificarAsync(
        string provedor, string titulo, string detalhe,
        string? chaveRepeticao = null, CancellationToken ct = default, bool informativo = false)
    {
        try
        {
            var sistema = provedor.ToUpperInvariant();

            if (informativo)
            {
                var texto = $"*{sistema} — {titulo}*\n\n{detalhe}";
                foreach (var telefone in await destinatarios.ListarAtivosAsync(ct))
                {
                    var envio = await whatsApp.EnviarTextoAsync(telefone, texto, ct: ct);
                    if (!envio.Ok)
                        logger.LogInformation(
                            "NOTIFICADOR_SINCRONISMO: informativo não entregue a {Telefone}: {Erro}", telefone, envio.Erro);
                }
                return;
            }

            var chave = AlertaCatalogo.Sincronismo(provedor);
            var rotulo = $"Sincronismo {sistema}";
            if (chaveRepeticao is { Length: > 0 } sub && !sub.Contains(':'))
            {
                chave = $"{chave}.{sub.ToLowerInvariant()}";
                rotulo = $"{rotulo} — {sub}";
            }

            alerta.Reportar(new EventoAlerta(chave, titulo, detalhe)
            {
                Rotulo = rotulo,
                Grupo = "Sincronismo",
            });
        }
        catch (Exception ex)
        {
            // Avisar é secundário: se falhar, o motor segue e o erro original continua no log.
            logger.LogWarning(ex, "NOTIFICADOR_SINCRONISMO: falha ao avisar sobre {Provedor}.", provedor);
        }
    }
}
