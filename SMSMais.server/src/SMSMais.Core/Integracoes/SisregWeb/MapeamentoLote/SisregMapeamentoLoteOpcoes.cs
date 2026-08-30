namespace SMSMais.Core.Integracoes.SisregWeb.MapeamentoLote;

/// <summary>
/// Parâmetros do motor que sincroniza o mapeamento (profissionais + procedimentos) de <b>todas</b>
/// as unidades de uma vez (seção <c>Sisreg:MapeamentoLote</c> do appsettings).
///
/// <para><b>O ritmo não é estético.</b> O mapeamento e a varredura de agenda dividem o mesmo
/// orçamento anti-robô do SISREG (mesmo IP de saída, ver ADR-0040 e <c>docs/sisreg-egress.md</c>):
/// o CAPTCHA aparece por volta de 700 requisições por operador. Por isso o lote roda as unidades
/// <b>em sequência</b> (nunca em paralelo) e espaça as requisições para caber em
/// <see cref="RequisicoesPorHora"/>.</para>
///
/// <para><b>Por que existe TTL e rodízio.</b> Mapear uma unidade custa 1 requisição pela lista de
/// profissionais + 1 por profissional pelos procedimentos dele. Medido em Maricá: as 6 unidades
/// mapeadas somam 324 profissionais, ou seja 330 requisições — e a rede tem 43 unidades. Refazer
/// todas toda noite seria da ordem de 2.400 requisições, mais de três vezes o teto do CAPTCHA. O
/// lote então trata as unidades por idade de mapeamento (a mais antiga primeiro) e pula as que
/// ainda estão dentro do TTL: a rede inteira se cobre em algumas rodadas, e nenhuma rodada estoura
/// o orçamento.</para>
/// </summary>
public sealed class SisregMapeamentoLoteOpcoes
{
    public const string Secao = "Sisreg:MapeamentoLote";

    /// <summary>Teto de requisições ao SISREG por hora durante o lote. Default 500.</summary>
    public int RequisicoesPorHora { get; set; } = 500;

    /// <summary>Intervalo do tick do scheduler diário, em segundos.</summary>
    public int TickSegundos { get; set; } = 60;

    /// <summary>
    /// Idade máxima do mapeamento de uma unidade <b>cuja varredura depende dele</b> (varredura
    /// ligada e sem o recorte "unidade inteira"): nesse arranjo cada par profissional × procedimento
    /// habilitado é o que a varredura vai buscar, então um mapeamento velho deixa agenda de fora.
    /// </summary>
    public int TtlDiasVarreduraPorCombinacao { get; set; } = 7;

    /// <summary>
    /// Idade máxima do mapeamento nas demais unidades. É bem maior porque a varredura com recorte
    /// "unidade inteira" <b>não usa o mapeamento</b> (traz a agenda toda numa requisição — ver
    /// <c>VarreduraAgendaService</c>): ali o mapeamento só alimenta o Practitioner no hub FHIR, o
    /// aviso por WhatsApp por procedimento e o botão de importação pontual. Nada disso justifica
    /// gastar centenas de requisições por noite.
    /// </summary>
    public int TtlDiasPadrao { get; set; } = 30;

    /// <summary>
    /// Idade máxima dos <b>procedimentos</b> de um profissional já conhecido. É o segundo eixo da
    /// economia: a lista de profissionais da unidade custa 1 requisição e já diz quem é novo, mas
    /// os procedimentos custam 1 requisição por profissional. Com isto, revisitar uma unidade
    /// estável custa ~1 requisição em vez de ~55 — só os profissionais novos (e a fatia vencida)
    /// vão ao SISREG.
    /// </summary>
    public int TtlDiasProcedimentos { get; set; } = 45;

    /// <summary>
    /// Quantos profissionais supor numa unidade que nunca foi mapeada, para decidir se ela cabe no
    /// orçamento restante. Chute deliberadamente alto: subestimar faz o lote entrar numa unidade
    /// que não cabe e parar no meio, jogando fora o que já gastou.
    /// </summary>
    public int EstimativaProfissionaisUnidadeNova { get; set; } = 80;

    /// <summary>Intervalo mínimo entre requisições, derivado de <see cref="RequisicoesPorHora"/>.</summary>
    public TimeSpan IntervaloMinimoRequisicao =>
        TimeSpan.FromSeconds(3600.0 / Math.Max(1, RequisicoesPorHora));

    public TimeSpan TtlProcedimentos => TimeSpan.FromDays(Math.Max(1, TtlDiasProcedimentos));
}
