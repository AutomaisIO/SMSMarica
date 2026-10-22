namespace SMSMais.Data.Entities;

/// <summary>
/// Histórico de cada notificação (push) mandada ao app do cidadão: quem mandou, quando, o texto e
/// o desfecho. É a trilha de auditoria (LGPD) e o que responde ao "não chegou" — sem ela, depois
/// do envio não sobra nada do lado de cá (o Firebase não guarda histórico consultável).
/// </summary>
public class CidadaoNotificacao
{
    public Guid Id { get; set; }

    /// <summary>Aponta para fhir.patient (hub FHIR) — sem FK/navegação local.</summary>
    public Guid PacienteId { get; set; }

    public string Titulo { get; set; } = string.Empty;
    public string Mensagem { get; set; } = string.Empty;

    /// <summary>Tela do app aberta no toque (lista fixa). Null = Início.</summary>
    public string? Rota { get; set; }

    /// <summary>De onde saiu o envio: <c>painel</c> (equipe, pela ficha do paciente).</summary>
    public string Origem { get; set; } = string.Empty;

    /// <summary>Usuário da equipe que mandou. Null quando não houver pessoa por trás.</summary>
    public Guid? EnviadoPor { get; set; }

    public DateTime CriadoEm { get; set; }

    /// <summary>Quantos aparelhos (sessões ativas com token) estavam na mira.</summary>
    public int Aparelhos { get; set; }

    /// <summary>Quantos o Firebase aceitou. Aceitar não prova que o cidadão viu.</summary>
    public int Entregues { get; set; }

    /// <summary>Resumo do que deu errado quando <see cref="Entregues"/> &lt; <see cref="Aparelhos"/>.</summary>
    public string? Falha { get; set; }
}
