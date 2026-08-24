namespace SMSMais.Core.PainelInicio;

/// <summary>
/// As janelas (em dias) das raias do painel de início.
///
/// Vivem aqui, e não como número solto em cada consulta, porque o painel e o "ver todos" da
/// listagem de solicitações precisam usar EXATAMENTE o mesmo recorte: se divergirem, a contagem
/// da raia deixa de bater com a lista que ela abre — o jeito mais rápido de o operador parar de
/// confiar no painel.
///
/// São constantes de código, não configuração. Viram configuração quando alguém pedir — não antes
/// (ADR-0033).
/// </summary>
public static class JanelasPainel
{
    /// <summary>"Aguardando resposta": exame dentro de N dias e paciente ainda sem responder.</summary>
    public const int AguardandoDias = 3;

    /// <summary>"Confirmados": contagem tranquila dos próximos N dias.</summary>
    public const int ConfirmadosDias = 7;
}
