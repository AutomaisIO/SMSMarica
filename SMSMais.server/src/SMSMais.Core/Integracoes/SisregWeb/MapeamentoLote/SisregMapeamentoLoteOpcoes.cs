namespace SMSMais.Core.Integracoes.SisregWeb.MapeamentoLote;

/// <summary>
/// Parâmetros do motor que sincroniza o mapeamento (profissionais + procedimentos) de <b>todas</b>
/// as unidades de uma vez (seção <c>Sisreg:MapeamentoLote</c> do appsettings).
///
/// <para><b>O ritmo não é estético.</b> O mapeamento e a varredura de agenda dividem o mesmo
/// orçamento anti-robô do SISREG (mesmo IP de saída, ver ADR-0040 e <c>docs/sisreg-egress.md</c>):
/// o CAPTCHA aparece por volta de 700 requisições por operador. Por isso o lote roda as unidades
/// <b>em sequência</b> (nunca em paralelo) e o teto de volume é do
/// <see cref="SisregOrcamentoRequisicoes"/>, compartilhado com os demais motores.</para>
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

    /// <summary>
    /// Pausa entre requisições. <b>350ms, o mesmo da varredura</b> — e o mesmo do script Python que
    /// rodou sem bloquear.
    ///
    /// <para><b>Por que não é mais lento:</b> este motor espaçava as requisições em 7,2s para
    /// "caber em 500/h", e o resultado foi uma rodada de 391 requisições levando 47 minutos
    /// (medido em 30/08/2026). Não comprava nada: o SISREG bloqueia por <b>volume acumulado</b>,
    /// não por ritmo — o laboratório fez 352 requisições em 47 <i>segundos</i> no CDT sem CAPTCHA
    /// (<c>Automais.SISREG/docs/APRENDIZADOS.md</c>), e a varredura da agenda sempre usou 350ms.
    /// Quem protege o orçamento é o <see cref="SisregOrcamentoRequisicoes"/>, que conta a janela
    /// rolante de 60 min; a pausa aqui só evita rajada.</para>
    /// </summary>
    public int PausaMs { get; set; } = 350;

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

    /// <summary>
    /// Orçamento mínimo para o lote valer a pena. Abaixo disto ele nem começa.
    ///
    /// <para>Nasceu de uma rodada real: em 30/08/2026, cinco minutos depois de um lote que gastou
    /// 391 requisições, um segundo disparo passou pelo "resta mais que zero" (sobravam 9), gastou
    /// 1 requisição na descoberta e pulou as 53 unidades por falta de orçamento — deixando no
    /// histórico uma linha "Parcial" com zero feito. Não é errado, mas é ruído: o operador abre o
    /// detalhe esperando encontrar trabalho e encontra uma lista de desculpas.</para>
    /// </summary>
    public int OrcamentoMinimoParaIniciar { get; set; } = 25;

    public TimeSpan IntervaloMinimoRequisicao => TimeSpan.FromMilliseconds(Math.Max(0, PausaMs));

    public TimeSpan TtlProcedimentos => TimeSpan.FromDays(Math.Max(1, TtlDiasProcedimentos));
}
