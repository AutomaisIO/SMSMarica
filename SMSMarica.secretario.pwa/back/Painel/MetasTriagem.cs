namespace SMSMarica.Secretario.Api.Painel;

/// <summary>
/// A régua de tempo do painel: metas do protocolo de Manchester, em minutos, iguais para
/// as três unidades. Decisão do usuário em 25/07/2026.
///
/// <para>
/// <b>Por que constante no código e não o cadastro das bases.</b> As três bases guardam
/// meta, e as três discordam — inclusive uma consigo mesma:
/// </para>
///
/// <list type="bullet">
///   <item><b>Salux/Conde</b> (<c>INFOSAUDE.CLASSIFICACAO_RISCO.QT_TEMPO</c>): a tabela
///   tem DUAS famílias de linhas. As de nome clínico (cd 1–5: EMERGÊNCIA 0, MUITO URGENTE
///   10, URGENTE 60, POUCO URGENTE 120, NÃO URGENTE 240) são exatamente esta régua — e
///   estão MORTAS: zero boletins em 30 dias. As vivas são as de nome de cor (cd 7–10:
///   Vermelho 15, Amarelo 30, Verde 60, Azul 1440), com valores que não são o protocolo.</item>
///
///   <item><b>UPAs</b> (<c>risco_acolhimento.risaco_Tempo_Espera</c>): a meta é por
///   protocolo. O 0006 bate com esta régua; o 0002 é todo vazio; e o 0005 da Santa Rita
///   tem Verde 60 / Amarelo 30 — metade. Pior: a Santa Rita usa os DOIS ao mesmo tempo
///   (30 dias: 4.858 classificações pelo 0005 contra 1.006 pelo 0006), então lá o mesmo
///   verde era medido contra metas diferentes conforme a tela que a enfermagem abriu.</item>
/// </list>
///
/// <para>
/// Ler meta de cadastro assim não é "respeitar a unidade", é herdar inconsistência. Como
/// o Oracle do Salux é read-only absoluto e o HIS das UPAs é de terceiro, corrigir na
/// origem não está na nossa mão — a régua vive aqui.
/// </para>
/// </summary>
public static class MetasTriagem
{
    /// <summary>
    /// Minutos-alvo entre a classificação e o atendimento. <b>Zero no vermelho</b> quer
    /// dizer imediato: não é meta que se cumpra "em X min", e por isso o vermelho não
    /// reporta percentual — ver <see cref="TemPercentual"/>.
    /// </summary>
    private static readonly Dictionary<string, int> PorCor = new()
    {
        [Unidades.Vermelho] = 0,
        [Unidades.Laranja] = 10,
        [Unidades.Amarelo] = 60,
        [Unidades.Verde] = 120,
        [Unidades.Azul] = 240,
    };

    /// <summary>Meta da cor, ou nulo para SEM_CLASSIFICACAO (sem cor ⇒ sem alvo).</summary>
    public static int? De(string cor) => PorCor.TryGetValue(cor, out var meta) ? meta : null;

    /// <summary>
    /// Só faz sentido calcular "% dentro da meta" quando a meta é um prazo maior que zero.
    /// No vermelho o alvo é imediato: qualquer tempo medido ficaria fora, e a linha viraria
    /// 0% fixo na tela — ruído, não informação.
    /// </summary>
    public static bool TemPercentual(string cor) => De(cor) is > 0;

    /// <summary>
    /// A meta na forma de <c>CASE</c> SQL sobre o código de classificação do Salux, para
    /// entrar na consulta de período. Mapeia os códigos VIVOS (7–10) — os de nome clínico
    /// (1–5) não são usados por boletim nenhum.
    /// </summary>
    public static string CaseSalux(string coluna) => $"""
        CASE {coluna}
                        WHEN 7 THEN {PorCor[Unidades.Vermelho]}
                        WHEN 8 THEN {PorCor[Unidades.Amarelo]}
                        WHEN 9 THEN {PorCor[Unidades.Verde]}
                        WHEN 10 THEN {PorCor[Unidades.Azul]}
                      END
        """;

    /// <summary>
    /// A meta na forma de <c>CASE</c> SQL sobre a descrição da cor do HIS das UPAs, que
    /// vem capitalizada ('Vermelho', 'Amarelo'…) e é a ÚNICA fonte confiável de cor lá
    /// (<c>risaco_gravidade</c> é relativa ao protocolo — ver ConsultasUpa).
    /// </summary>
    public static string CaseUpa(string coluna) => $"""
        CASE UPPER({coluna})
                        WHEN 'VERMELHO' THEN {PorCor[Unidades.Vermelho]}
                        WHEN 'LARANJA' THEN {PorCor[Unidades.Laranja]}
                        WHEN 'AMARELO' THEN {PorCor[Unidades.Amarelo]}
                        WHEN 'VERDE' THEN {PorCor[Unidades.Verde]}
                        WHEN 'AZUL' THEN {PorCor[Unidades.Azul]}
                      END
        """;
}
