using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SMSMais.Core.Notificacoes.WhatsApp.Conta;

/// <summary>De onde veio a notícia sobre a conta.</summary>
public static class OrigemSituacaoConta
{
    /// <summary>Resposta do envio (o Zap devolveu o erro da Meta na hora).</summary>
    public const string Envio = "envio";
    /// <summary>Recibo do webhook (a Meta aceitou e recusou depois).</summary>
    public const string Webhook = "webhook";
    /// <summary>Consulta periódica do <c>health_status</c> do número na Meta.</summary>
    public const string SaudeMeta = "saude-meta";
}

/// <summary>Retrato da conta parada, para a faixa do painel.</summary>
public sealed record ContaWhatsAppParada(
    DateTime Desde,
    string Codigo,
    string Motivo,
    string Origem,
    string? ErroOriginal,
    DateTime UltimaConfirmacaoEm,
    DateTime ProximaSondaEm,
    int EnviosSegurados);

/// <summary>
/// Estado da conta do WhatsApp Business nesta instância: funcionando ou PARADA (pagamento,
/// bloqueio, token…). Singleton em memória — a verdade é da Meta: depois de um restart a primeira
/// recusa (ou a consulta de saúde) refaz o estado em segundos.
///
/// <para><b>O freio:</b> com a conta parada, nenhum envio vai à Meta, a não ser UMA sonda a cada
/// <see cref="ContaWhatsAppOptions.IntervaloSondaMinutos"/>: sem isso, cada mensagem da fila
/// gastaria uma tentativa contra uma porta fechada e viraria falha definitiva (foi o que aconteceu
/// em 19/10/2026). A sonda que passa libera tudo; a consulta de saúde também.</para>
///
/// <para>Parada vinda do RECIBO só é liberada por recibo de entrega/leitura ou pela consulta de
/// saúde — não por um envio aceito: quando a Meta recusa no recibo, o envio sempre é "aceito" antes,
/// e liberar ali faria a fila inteira sair e voltar recusada a cada sonda.</para>
/// </summary>
public interface ISituacaoContaWhatsApp
{
    /// <summary>Null = conta funcionando.</summary>
    ContaWhatsAppParada? Parada { get; }

    /// <summary>Registra a recusa de conta. Devolve true quando a conta ACABOU de parar.</summary>
    bool Parar(ErroDeContaWhatsApp erro, string origem, string? erroOriginal);

    /// <summary>A conta voltou a funcionar. Devolve true quando ela ACABOU de voltar.</summary>
    bool Liberar(string origem);

    /// <summary>
    /// Pode ir à Meta agora? Com a conta funcionando, sempre. Parada, só a sonda — e quem a recebe
    /// a consome (a próxima só depois do intervalo).
    /// </summary>
    bool PodeEnviar();

    /// <summary>Há sonda disponível (sem consumir) — para o worker da fila decidir se busca 1 item.</summary>
    bool SondaDisponivel { get; }

    /// <summary>A consulta de saúde respondeu (qualquer resultado) — vai para a faixa.</summary>
    void RegistrarConsultaSaude();

    /// <summary>Última consulta de saúde que respondeu (null = nunca, ou o relay não sabe responder).</summary>
    DateTime? UltimaConsultaSaudeEm { get; }
}

public sealed class SituacaoContaWhatsApp(
    IOptions<ContaWhatsAppOptions> options,
    ILogger<SituacaoContaWhatsApp> logger,
    TimeProvider? relogio = null) : ISituacaoContaWhatsApp
{
    private readonly object _trava = new();
    private readonly TimeProvider _relogio = relogio ?? TimeProvider.System;
    private ContaWhatsAppParada? _parada;
    private DateTime? _ultimaConsultaSaude;

    private DateTime Agora => _relogio.GetUtcNow().UtcDateTime;
    private TimeSpan IntervaloSonda => TimeSpan.FromMinutes(Math.Max(1, options.Value.IntervaloSondaMinutos));

    public ContaWhatsAppParada? Parada
    {
        get { lock (_trava) return _parada; }
    }

    public DateTime? UltimaConsultaSaudeEm
    {
        get { lock (_trava) return _ultimaConsultaSaude; }
    }

    public bool Parar(ErroDeContaWhatsApp erro, string origem, string? erroOriginal)
    {
        ContaWhatsAppParada? nova;
        lock (_trava)
        {
            var agora = Agora;
            if (_parada is { } atual)
            {
                // Já parada: só se renova o retrato (o motivo mais recente vale mais) e empurra a sonda.
                _parada = atual with
                {
                    Codigo = erro.Codigo,
                    Motivo = erro.Explicacao,
                    // Recibo manda mais que envio na hora de liberar (ver a interface).
                    Origem = atual.Origem == OrigemSituacaoConta.Webhook ? atual.Origem : origem,
                    ErroOriginal = erroOriginal ?? atual.ErroOriginal,
                    UltimaConfirmacaoEm = agora,
                    ProximaSondaEm = agora + IntervaloSonda,
                };
                return false;
            }
            nova = _parada = new ContaWhatsAppParada(
                agora, erro.Codigo, erro.Explicacao, origem, erroOriginal, agora, agora + IntervaloSonda, 0);
        }

        // Error: entra no log de erros da plataforma. O aviso no celular sai pelo próprio WhatsApp e
        // por isso NÃO chega agora — quem avisa é a faixa vermelha do painel.
        logger.LogError(
            "WhatsApp PARADO ({Codigo}, via {Origem}): {Motivo} Envios segurados até a conta voltar. Erro da Meta: {Erro}",
            nova.Codigo, nova.Origem, nova.Motivo, erroOriginal);
        return true;
    }

    public bool Liberar(string origem)
    {
        ContaWhatsAppParada? era;
        lock (_trava)
        {
            if (_parada is null) return false;
            if (_parada.Origem == OrigemSituacaoConta.Webhook && origem == OrigemSituacaoConta.Envio) return false;
            era = _parada;
            _parada = null;
        }

        logger.LogWarning(
            "WhatsApp VOLTOU (via {Origem}) depois de {Minutos:F0} min parado por {Codigo}; {Segurados} envios tinham sido segurados.",
            origem, (Agora - era.Desde).TotalMinutes, era.Codigo, era.EnviosSegurados);
        return true;
    }

    public bool PodeEnviar()
    {
        lock (_trava)
        {
            if (_parada is null) return true;
            var agora = Agora;
            if (agora >= _parada.ProximaSondaEm)
            {
                _parada = _parada with { ProximaSondaEm = agora + IntervaloSonda };
                return true;
            }
            _parada = _parada with { EnviosSegurados = _parada.EnviosSegurados + 1 };
            return false;
        }
    }

    public bool SondaDisponivel
    {
        get { lock (_trava) return _parada is null || Agora >= _parada.ProximaSondaEm; }
    }

    public void RegistrarConsultaSaude()
    {
        lock (_trava) _ultimaConsultaSaude = Agora;
    }
}
