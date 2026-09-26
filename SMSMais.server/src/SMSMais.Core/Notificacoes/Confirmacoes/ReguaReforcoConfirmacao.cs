using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Conversas;
using SMSMais.Core.Notificacoes.Comunicacao;
using SMSMais.Core.Notificacoes.WhatsApp;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Notificacoes;

namespace SMSMais.Core.Notificacoes.Confirmacoes;

/// <summary>
/// Régua de reforço da confirmação (24/09/2026) — as regras que o ENFILEIRADOR
/// (<see cref="ReforcoConfirmacaoService"/>) e o ENVIO (<see cref="ComunicacaoPacienteService"/>)
/// têm de aplicar do mesmo jeito. Ficam num lugar só porque a régua é conferida duas vezes: na
/// entrada da fila e de novo na hora de sair — entre uma e outra a pessoa pode ter respondido,
/// uma atendente pode ter assumido, o cadastro pode ter mudado.
///
/// <para>O público: quem recebeu a PRIMEIRA mensagem (<c>confirmacao_exame</c>/
/// <c>confirmacao_consulta</c>, a "principal", retida em
/// <see cref="StatusComunicacao.AguardandoVerificacaoCadastral"/>) e não se identificou.</para>
///
/// <para>Toque 2 — reforço (<see cref="FinalidadeComunicacao.ReforcoConfirmacao"/>): três dias
/// depois, para quem não mandou NADA desde a principal. Toque 3 — orientação ao posto
/// (<see cref="FinalidadeComunicacao.OrientacaoPosto"/>): terminal, "a guia está no posto".</para>
///
/// <para><b>Nada do agendamento vai na mensagem.</b> {{1}} é o tratamento ("Sr. João") e {{2}} é
/// uma CONSTANTE escolhida pelo tipo (exame × consulta) — nunca o procedimento, a especialidade, a
/// data, a hora ou a unidade. O número ainda não foi provado (ADR-0057).</para>
/// </summary>
public static class ReguaReforcoConfirmacao
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>
    /// Carimbo na <c>motivo_falha</c> da principal quando a pessoa toca em <b>Vou ao posto</b>.
    /// Principal com este carimbo não recebe mais nada automático da régua (nem o lembrete): a
    /// resposta que ela recebeu promete "não vamos mais insistir por aqui". É PREFIXO — o carimbo
    /// completo leva a data ("Confirmou que vai ao posto (24/09)").
    /// </summary>
    public const string CarimboVaiAoPosto = "Confirmou que vai ao posto";

    /// <summary>Prefixo do motivo de uma linha da régua que deixou de fazer sentido antes de sair.</summary>
    public const string PrefixoSuperadoReforco = "Superado antes do reforço";

    /// <summary>O mesmo, para a orientação ao posto.</summary>
    public const string PrefixoSuperadoOrientacao = "Superado antes da orientação ao posto";

    /// <summary>
    /// Diálogo de identificação PARADO há tanto tempo (a pessoa começou — mandou CPF, parou no
    /// nascimento — e sumiu) entra no público da orientação ao posto. Três dias, a mesma espera do
    /// reforço: menos que isso é gente que ainda vai voltar.
    /// </summary>
    public const int HorasDialogoParado = 72;

    /// <summary>Validade do estado de verificação renovada a cada toque da régua — a mesma da
    /// primeira mensagem: a pessoa pode responder dias depois.</summary>
    public static readonly TimeSpan ValidadeEstado = TimeSpan.FromDays(7);

    /// <summary>Linha da régua (reforço ou orientação)?</summary>
    public static bool EhDaRegua(FinalidadeComunicacao f) =>
        f is FinalidadeComunicacao.ReforcoConfirmacao or FinalidadeComunicacao.OrientacaoPosto;

    // ===================== MODELO E TEXTO =====================

    /// <summary>
    /// {{2}} de cada modelo — CONSTANTE pelo tipo, nunca dado do agendamento. O B diz "as
    /// informações <i>sobre seu exame</i>"; o A e o C dizem "a confirmação / as informações
    /// <i>do seu exame</i>".
    /// </summary>
    internal static string ComplementoDoModelo(string modelo, TipoAgendamento tipo, ComunicacaoPacienteOptions opts)
    {
        var consulta = tipo == TipoAgendamento.Consulta;
        return string.Equals(modelo, opts.TemplateReforcoLidaSemResposta, StringComparison.OrdinalIgnoreCase)
            ? (consulta ? "sobre sua consulta" : "sobre seu exame")
            : (consulta ? "da sua consulta" : "do seu exame");
    }

    /// <summary>
    /// Modelo do reforço, escolhido NO ENVIO: quem não leu a principal recebe o A ("a confirmação
    /// continua pendente") — mas só se ele estiver aprovado; senão, e para quem leu, o B ("nossa
    /// mensagem chegou…"), cujo texto é verdadeiro nos dois casos.
    ///
    /// <para>"Aprovado" = PRESENTE no catálogo. O catálogo vem do relay
    /// (<c>GET /v1/templates</c> do Automais.Zap), que só lista os modelos com status
    /// <c>APPROVED</c> na Meta e não devolve o status — então estar na lista é estar aprovado.
    /// Catálogo indisponível (relay fora, modo simulado) chega vazio, e vazio é B: na dúvida, o
    /// modelo que sabidamente existe.</para>
    /// </summary>
    internal static string ModeloDoReforco(
        bool principalLida, IReadOnlyList<TemplateWhatsApp> catalogo, ComunicacaoPacienteOptions opts)
        => !principalLida
           && catalogo.Any(t => string.Equals(t.Nome, opts.TemplateReforcoNaoLida, StringComparison.OrdinalIgnoreCase))
            ? opts.TemplateReforcoNaoLida
            : opts.TemplateReforcoLidaSemResposta;

    /// <summary>O que sai: modelo, parâmetros e o texto humano gravado na thread (a atendente
    /// precisa ver o que o cidadão recebeu, não um marcador técnico).</summary>
    internal sealed record EnvioRegua(string Modelo, string[] Parametros, string ConteudoLegivel);

    internal static EnvioRegua MontarEnvio(
        FinalidadeComunicacao finalidade, TipoAgendamento tipo, string tratamento, bool principalLida,
        IReadOnlyList<TemplateWhatsApp> catalogo, ComunicacaoPacienteOptions opts)
    {
        var modelo = finalidade == FinalidadeComunicacao.OrientacaoPosto
            ? opts.TemplateOrientacaoPosto
            : ModeloDoReforco(principalLida, catalogo, opts);
        var complemento = ComplementoDoModelo(modelo, tipo, opts);
        return new EnvioRegua(modelo, [tratamento, complemento], TextoDoModelo(modelo, tratamento, complemento, opts));
    }

    /// <summary>
    /// Corpo APROVADO de cada modelo com {{1}}/{{2}} preenchidos. Copiado do que foi submetido à
    /// Meta — se o modelo mudar lá, muda aqui junto (senão a thread mostra uma coisa e o cidadão
    /// recebeu outra).
    /// </summary>
    internal static string TextoDoModelo(string modelo, string p1, string p2, ComunicacaoPacienteOptions opts)
    {
        const string abertura = "esse é o canal oficial do *Alô Maricá* da Secretaria Municipal de Saúde.";

        if (string.Equals(modelo, opts.TemplateOrientacaoPosto, StringComparison.OrdinalIgnoreCase))
            return $"Olá *{p1}*, {abertura}\n\n"
                + "Tentamos falar com você sobre o seu agendamento, mas não conseguimos confirmar os dados "
                + "por aqui. Tudo bem: não vamos mais insistir por mensagem.\n\n"
                + "O seu agendamento continua valendo. Para saber o dia, a hora e o local e retirar a guia, "
                + "procure o *posto de saúde onde o paciente tem cadastro*, com um documento com foto.\n\n"
                + $"Se preferir ver as informações {p2} por aqui, ainda dá: toque em *Quero mais informações*. "
                + "Se vai ao posto, toque em *Vou ao posto*. Se esta mensagem não é para você, toque em "
                + "*Não sou essa pessoa*";

        if (string.Equals(modelo, opts.TemplateReforcoNaoLida, StringComparison.OrdinalIgnoreCase))
            return $"Olá *{p1}*, {abertura}\n\n"
                + $"A confirmação *{p2}* continua pendente: enviamos uma mensagem sobre o seu agendamento e "
                + "ainda não tivemos resposta.\n\n"
                + "Se esta mensagem não é para você, toque em *Não sou essa pessoa*"
                + "\n\nNunca pedimos CPF completo, senha ou pagamento.";

        return $"Olá *{p1}*, {abertura}\n\n"
            + "Nossa mensagem sobre o seu agendamento chegou, mas ainda não recebemos sua resposta. Sem ela, "
            + $"não podemos enviar as informações *{p2}* por aqui: é uma regra de segurança para proteger os "
            + "dados do paciente.\n\n"
            + "Toque em *Quero mais informações*.\n\n"
            + "Se preferir não responder, tudo bem: o agendamento continua valendo, e a guia com dia, hora e "
            + "local pode ser retirada no posto de saúde onde o paciente tem cadastro.\n\n"
            + "Se esta mensagem não é para você, toque em *Não sou essa pessoa*.";
    }

    /// <summary>O carimbo que a principal ganha quando a linha da régua sai.</summary>
    internal static string CarimboNaPrincipal(FinalidadeComunicacao finalidade, DateTime enviadoEmUtc)
    {
        var dia = FusoBrasilia.ParaExibicao(enviadoEmUtc).ToString("dd/MM", PtBr);
        return finalidade == FinalidadeComunicacao.OrientacaoPosto
            ? $"Orientado a procurar o posto em {dia} — sem novos automáticos"
            : $"Reforço enviado em {dia} — aguardando o paciente se identificar";
    }

    // ===================== QUEM RECEBE =====================

    /// <summary>
    /// Reforço: só para quem não mandou NADA desde a principal, e cujo número não tem diálogo de
    /// identificação andando (sem estado, ou parado em "Quero mais informações", que é onde a
    /// primeira mensagem o deixa). Quem já escreveu está sendo atendido — pela máquina, pelo robô
    /// ou por uma pessoa —, e insistir por cima é atropelar a conversa.
    /// </summary>
    internal static bool ReforcoAlcanca(bool houveEntradaDesdeAPrincipal, VerificacaoCadastralEstado? estado)
        => !houveEntradaDesdeAPrincipal
           && (estado is null || estado.Etapa == EtapaVerificacaoCadastral.AguardandoInteresse);

    /// <summary>
    /// Orientação ao posto: quem não mandou nada desde a principal, OU quem começou a se
    /// identificar e parou há dias (o estado deste número aponta para este paciente e não anda há
    /// <see cref="HorasDialogoParado"/> horas). Nunca para <see cref="EtapaVerificacaoCadastral.Esgotado"/>:
    /// esses já ouviram "procure o posto" da própria máquina de verificação.
    /// </summary>
    internal static bool OrientacaoAlcanca(
        bool houveEntradaDesdeAPrincipal, VerificacaoCadastralEstado? estado,
        Guid principalId, Guid pacienteId, DateTime agoraUtc)
    {
        if (estado?.Etapa == EtapaVerificacaoCadastral.Esgotado) return false;
        if (!houveEntradaDesdeAPrincipal) return true;
        if (estado is null) return false;

        var apontaParaEste = estado.ComunicacaoPacienteId == principalId || estado.PacienteId == pacienteId;
        var etapaDoMeio = estado.Etapa is EtapaVerificacaoCadastral.AguardandoCpf
            or EtapaVerificacaoCadastral.AguardandoNascimento
            or EtapaVerificacaoCadastral.AguardandoNome
            or EtapaVerificacaoCadastral.AguardandoVinculo
            or EtapaVerificacaoCadastral.AguardandoNovaTentativa;
        var parado = (estado.AtualizadoEm ?? estado.CriadoEm) <= agoraUtc.AddHours(-HorasDialogoParado);
        return apontaParaEste && etapaDoMeio && parado;
    }

    /// <summary>Domingo em Brasília: a régua não sai. É insistência, e insistência no domingo
    /// irrita mais do que ajuda.</summary>
    internal static bool EhDomingo(DateTime agoraUtc) =>
        FusoBrasilia.ParaExibicao(agoraUtc).DayOfWeek == DayOfWeek.Sunday;

    /// <summary>A abertura da janela no primeiro dia depois de hoje que não é domingo (UTC).</summary>
    internal static DateTime ProximaAberturaForaDoDomingo(DateTime agoraUtc, TimeOnly inicio)
    {
        var dia = FusoBrasilia.ParaExibicao(agoraUtc).Date.AddDays(1);
        if (dia.DayOfWeek == DayOfWeek.Sunday) dia = dia.AddDays(1);
        return FusoBrasilia.DeBrasiliaParaUtc(dia.Add(inicio.ToTimeSpan()));
    }

    // ===================== O NÚMERO =====================

    /// <summary>
    /// As formas com que o MESMO celular aparece gravado. A comunicação guarda o formato atual de
    /// 13 dígitos (<see cref="TelefoneWhatsApp.NormalizarNonoDigito"/>); a mensagem que CHEGA
    /// guarda o <c>from</c> da Meta canonizado (<see cref="TelefoneWhatsApp.Canonizar"/>) — e o
    /// WhatsApp ainda identifica muitos celulares brasileiros antigos SEM o nono dígito (12
    /// dígitos). Comparar só a forma de 13 faria a resposta dessa gente passar despercebida, e o
    /// reforço sairia para quem já respondeu.
    /// </summary>
    internal static string[] FormasDoNumero(string? telefone)
    {
        if (string.IsNullOrWhiteSpace(telefone)) return [];
        var atual = TelefoneWhatsApp.NormalizarNonoDigito(telefone);
        var formas = new List<string> { atual, TelefoneWhatsApp.Canonizar(telefone) };
        if (atual.Length == 13 && atual.StartsWith("55", StringComparison.Ordinal) && atual[4] == '9')
            formas.Add(atual[..4] + atual[5..]);
        return [.. formas.Distinct(StringComparer.Ordinal)];
    }

    /// <summary>Chave única do número para agrupar (formato atual, com o nono dígito).</summary>
    internal static string ChaveDoNumero(string telefone) => TelefoneWhatsApp.NormalizarNonoDigito(telefone);

    /// <summary>Chegou alguma mensagem deste número depois do instante dado?</summary>
    internal static Task<bool> HouveEntradaDesdeAsync(
        SmsMaisDbContext db, string telefone, DateTime desdeUtc, CancellationToken ct)
    {
        var formas = FormasDoNumero(telefone);
        return db.MensagensWhatsApp.AsNoTracking().AnyAsync(m =>
            m.Direcao == DirecaoMensagem.Entrada && m.OcorridoEm > desdeUtc && formas.Contains(m.Telefone), ct);
    }

    /// <summary>O estado da verificação cadastral deste número (rastreado — o envio o renova).</summary>
    internal static Task<VerificacaoCadastralEstado?> EstadoDoNumeroAsync(
        SmsMaisDbContext db, string telefone, CancellationToken ct)
    {
        var formas = FormasDoNumero(telefone);
        return db.VerificacoesCadastraisEstado.FirstOrDefaultAsync(e => formas.Contains(e.TelefoneCanonical), ct);
    }

    /// <summary>Finalidades que contam para o silêncio mínimo por número: tudo o que INSISTE sobre
    /// agendamento. O aviso de cancelamento fica de fora — é notícia, e atrasá-lo seria pior.</summary>
    internal static readonly FinalidadeComunicacao[] FinalidadesQueContamNoSilencio =
    [
        FinalidadeComunicacao.ConfirmacaoAgendamento,
        FinalidadeComunicacao.LembreteAgendamento,
        FinalidadeComunicacao.ReforcoConfirmacao,
        FinalidadeComunicacao.OrientacaoPosto,
    ];

    /// <summary>Quando saiu o último automático de agendamento para este número (fora a própria linha).</summary>
    internal static Task<DateTime?> UltimoAutomaticoDoNumeroAsync(
        SmsMaisDbContext db, string telefone, Guid excetoId, CancellationToken ct)
    {
        var formas = FormasDoNumero(telefone);
        return db.ComunicacoesPaciente.AsNoTracking()
            .Where(c => c.Id != excetoId
                && FinalidadesQueContamNoSilencio.Contains(c.Finalidade)
                && c.EnviadoEm != null
                && c.Telefone != null && formas.Contains(c.Telefone))
            .MaxAsync(c => c.EnviadoEm, ct);
    }
}
