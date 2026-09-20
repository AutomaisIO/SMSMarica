using Microsoft.Extensions.Logging;
using SMSMais.Core.Institucional;
using SMSMais.Core.Notificacoes.WhatsApp;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Ouvidoria;

namespace SMSMais.Core.Ouvidoria;

/// <summary>
/// Avisos ao manifestante por WhatsApp, <b>best-effort</b> (§2.8 do plano). Condições: a instância
/// notifica (<c>NotificarPorWhatsApp</c>), a manifestação não é anônima e há telefone. Em
/// <c>Sigilosa</c> o texto nunca traz teor nem assunto — só protocolo e etapa. Falha só loga
/// <c>Warning</c> (não é falha de plataforma) e nunca lança: a transição já foi gravada.
/// ADR-0057 (destinatário correto) é aplicado dentro do <see cref="IWhatsAppCliente"/>.
/// </summary>
public sealed class OuvidoriaNotificador(
    IWhatsAppCliente whatsApp,
    IInstituicaoService instituicao,
    ILogger<OuvidoriaNotificador> logger)
{
    /// <summary>Recibo do registro: protocolo, código de acesso (única vez) e prazo.</summary>
    public async Task RegistroAsync(OuvidoriaManifestacao m, string? codigoAcesso, OuvidoriaConfiguracao config, CancellationToken ct = default)
    {
        if (!PodeNotificar(m, config)) return;

        var prazo = m.PrazoRespostaEm.ToString("dd/MM/yyyy");
        string texto;
        if (!string.IsNullOrWhiteSpace(config.TextoRecibo))
        {
            texto = config.TextoRecibo
                .Replace("{protocolo}", m.Protocolo, StringComparison.OrdinalIgnoreCase)
                .Replace("{codigo}", codigoAcesso ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("{prazo}", prazo, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            var nome = await NomeCurtoAsync(ct);
            texto = $"Ouvidoria {nome}: sua manifestação foi registrada com o protocolo *{m.Protocolo}*."
                + (codigoAcesso is null ? string.Empty : $"\nCódigo de acesso: *{codigoAcesso}* (guarde — ele não será enviado de novo).")
                + $"\nPrazo de resposta: {prazo}.";
        }

        await EnviarAsync(m, texto, ct);
    }

    /// <summary>Encaminhamento à área responsável — sem nomear pessoas nem unidade.</summary>
    public async Task EncaminhamentoAsync(OuvidoriaManifestacao m, OuvidoriaConfiguracao config, CancellationToken ct = default)
    {
        if (!PodeNotificar(m, config)) return;
        var nome = await NomeCurtoAsync(ct);
        await EnviarAsync(m,
            $"Ouvidoria {nome}: a manifestação *{m.Protocolo}* foi encaminhada à área responsável em {Hoje()}. Prazo de resposta: {m.PrazoRespostaEm:dd/MM/yyyy}.",
            ct);
    }

    /// <summary>Pedido de complementação: o relógio fica suspenso até o cidadão responder.</summary>
    public async Task PedidoComplementacaoAsync(OuvidoriaManifestacao m, OuvidoriaConfiguracao config, string? pedido, CancellationToken ct = default)
    {
        if (!PodeNotificar(m, config)) return;
        var nome = await NomeCurtoAsync(ct);
        // Em sigilosa o pedido pode citar o teor: vai só a etapa.
        var detalhe = m.Identificacao == OuvidoriaIdentificacao.Sigilosa || string.IsNullOrWhiteSpace(pedido)
            ? string.Empty
            : $"\n{pedido.Trim()}";
        await EnviarAsync(m,
            $"Ouvidoria {nome}: a manifestação *{m.Protocolo}* precisa de informações complementares.{detalhe}\nResponda em até {config.ComplementacaoDias} dias pelo acompanhamento com seu protocolo e código de acesso; sem resposta ela será arquivada.",
            ct);
    }

    public async Task ProrrogacaoAsync(OuvidoriaManifestacao m, OuvidoriaConfiguracao config, CancellationToken ct = default)
    {
        if (!PodeNotificar(m, config)) return;
        var nome = await NomeCurtoAsync(ct);
        await EnviarAsync(m,
            $"Ouvidoria {nome}: o prazo de resposta da manifestação *{m.Protocolo}* foi prorrogado por {config.ProrrogacaoDias} dias, conforme a Lei 13.460/2017. Novo prazo: {m.PrazoRespostaEm:dd/MM/yyyy}.",
            ct);
    }

    /// <summary>Resposta conclusiva: o texto da resposta vai junto, exceto em sigilosa (só a etapa).</summary>
    public async Task RespostaConclusivaAsync(OuvidoriaManifestacao m, OuvidoriaConfiguracao config, CancellationToken ct = default)
    {
        if (!PodeNotificar(m, config)) return;
        var nome = await NomeCurtoAsync(ct);
        var corpo = m.Identificacao == OuvidoriaIdentificacao.Sigilosa || string.IsNullOrWhiteSpace(m.RespostaConclusiva)
            ? "Consulte a resposta no acompanhamento, com seu protocolo e código de acesso."
            : m.RespostaConclusiva.Trim();
        await EnviarAsync(m,
            $"Ouvidoria {nome}: a manifestação *{m.Protocolo}* foi respondida.\n{corpo}\nSe discordar, você pode recorrer em até {config.ArquivamentoAutomaticoDias} dias pelo acompanhamento.",
            ct);
    }

    public async Task ArquivamentoAsync(OuvidoriaManifestacao m, OuvidoriaConfiguracao config, CancellationToken ct = default)
    {
        if (!PodeNotificar(m, config)) return;
        var nome = await NomeCurtoAsync(ct);
        var motivo = m.MotivoArquivamento switch
        {
            OuvidoriaMotivoArquivamento.SemComplementacao => " por falta das informações complementares solicitadas",
            OuvidoriaMotivoArquivamento.Duplicidade => " por já existir manifestação sobre o mesmo fato",
            _ => string.Empty,
        };
        await EnviarAsync(m,
            $"Ouvidoria {nome}: a manifestação *{m.Protocolo}* foi arquivada{motivo}. Você pode registrar uma nova manifestação a qualquer momento.",
            ct);
    }

    // ---- internos ----

    private static bool PodeNotificar(OuvidoriaManifestacao m, OuvidoriaConfiguracao config)
        => config.NotificarPorWhatsApp
            && m.Identificacao != OuvidoriaIdentificacao.Anonima
            && !string.IsNullOrWhiteSpace(m.ManifestanteTelefone);

    private async Task<string> NomeCurtoAsync(CancellationToken ct)
    {
        var inst = await instituicao.ObterAsync(ct);
        return string.IsNullOrWhiteSpace(inst.NomeCurto) ? inst.NomeSecretaria : inst.NomeCurto;
    }

    private static string Hoje() => OuvidoriaPrazos.Hoje().ToString("dd/MM/yyyy");

    private async Task EnviarAsync(OuvidoriaManifestacao m, string texto, CancellationToken ct)
    {
        try
        {
            var resultado = await whatsApp.EnviarTextoAsync(m.ManifestanteTelefone!, texto, m.ManifestantePatientId, ct, OrigemEnvioWhatsApp.Automatico);
            if (!resultado.Ok)
            {
                logger.LogWarning("Ouvidoria: aviso da manifestação {Protocolo} não enviado: {Erro}.", m.Protocolo, resultado.Erro);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Ouvidoria: falha ao avisar o manifestante da manifestação {Protocolo}.", m.Protocolo);
        }
    }
}
