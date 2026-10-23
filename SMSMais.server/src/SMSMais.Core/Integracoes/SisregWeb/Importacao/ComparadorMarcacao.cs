using SMSMais.Core.Common.Tempo;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Integracoes.SisregWeb.Importacao;

/// <summary>Os campos que o SISREG manda e que podem mudar entre duas leituras da mesma solicitação.</summary>
/// <param name="DataAgendadaUtc">Já convertida para UTC — a comparação não pode depender de fuso.</param>
public sealed record FotoMarcacao(
    DateTime? DataAgendadaUtc,
    string? ExecutanteCpf,
    string? ProcedimentoCodigo,
    string? ProcedimentoNome);

/// <param name="Antes">Vazio quando o campo não existia (dado que só agora o SISREG passou a mandar).</param>
public sealed record AlteracaoDetectada(TipoAlteracaoAgenda Tipo, string? Antes, string? Depois);

/// <summary>
/// Compara a solicitação que temos com a linha que o SISREG acabou de mandar.
///
/// <para><b>Por que isto existe:</b> até agora, encontrar um número de solicitação já importado era
/// motivo para pular a linha. Barato, mas cego — se o SISREG remarcasse a consulta, trocasse o
/// profissional ou o procedimento, nosso banco ficava com o dado velho <b>para sempre</b>, e nada
/// no sistema denunciava. Uma agenda montada sobre isso mostraria o paciente no dia errado com
/// cara de certeza.</para>
///
/// <para><b>Só compara o que é do SISREG.</b> Paciente resolvido, exame já na worklist, laudo e a
/// confirmação que o paciente deu por WhatsApp são <b>nossos</b> — não entram aqui, e sobrescrevê-los
/// a partir do arquivo apagaria trabalho feito deste lado.</para>
///
/// <para><b>Compara campo a campo, não a linha crua.</b> Um <c>diff</c> do RAW inteiro acusaria
/// mudança a cada variação de formatação ou de campo volátil, e a lista de alterações viraria ruído
/// que ninguém lê. O que interessa é o punhado de campos que muda a vida de alguém.</para>
///
/// <para><b>O que este comparador NÃO pega: cancelamento.</b> Agendamento cancelado no SISREG
/// simplesmente para de vir no arquivo — não é campo alterado, é linha ausente. Detectar isso exige
/// comparar o conjunto de códigos da janela, com o cuidado de nunca concluir "cancelado" a partir de
/// uma exportação truncada ou parcial. Fica para tratamento próprio.</para>
///
/// <para><b>Procedimento diferente com o mesmo nº não é troca (ticket #149).</b> O SISREG manda
/// cada procedimento do pedido numa linha própria, com o mesmo código de solicitação. Quem chama
/// decide primeiro se a linha é do mesmo item (<see cref="MesmoProcedimento"/>) — e só então
/// compara; procedimento que ainda não estava no pedido vira item novo, não alteração.</para>
/// </summary>
public static class ComparadorMarcacao
{
    public static IReadOnlyList<AlteracaoDetectada> Comparar(FotoMarcacao antes, FotoMarcacao depois)
    {
        var alteracoes = new List<AlteracaoDetectada>();

        // Data ausente no arquivo novo nunca apaga a que temos, e ganhar a hora que faltava não é
        // remarcação (ver `SoGanhouHora`) — a solicitação sem horário fica em meia-noite, e o
        // preenchimento dela mudava o instante sem mudar o compromisso de ninguém.
        if (depois.DataAgendadaUtc is { } nova && antes.DataAgendadaUtc != nova
            && !SoGanhouHora(antes.DataAgendadaUtc, nova))
        {
            alteracoes.Add(new(
                TipoAlteracaoAgenda.DataHora,
                Data(antes.DataAgendadaUtc),
                Data(nova)));
        }

        // Nos campos de texto, vazio de qualquer um dos dois lados nunca vira alteração (ver
        // `Mudou`): o SISREG deixa campo em branco com frequência — o código do procedimento vem
        // vazio em ~1/3 das linhas — e tanto "sumiu" quanto "apareceu" são notícia sobre o ARQUIVO,
        // não sobre a agenda.
        if (Mudou(antes.ExecutanteCpf, depois.ExecutanteCpf))
        {
            alteracoes.Add(new(
                TipoAlteracaoAgenda.Executante, antes.ExecutanteCpf, depois.ExecutanteCpf));
        }

        if (Mudou(antes.ProcedimentoCodigo, depois.ProcedimentoCodigo))
        {
            alteracoes.Add(new(
                TipoAlteracaoAgenda.Procedimento,
                antes.ProcedimentoCodigo, depois.ProcedimentoCodigo));
        }

        // O nome só vira alteração quando o código NÃO mudou: quando os dois mudam é o mesmo fato,
        // e registrar duas linhas para um fato só faria a contagem de alterações mentir.
        else if (Mudou(antes.ProcedimentoNome, depois.ProcedimentoNome))
        {
            alteracoes.Add(new(
                TipoAlteracaoAgenda.Procedimento, antes.ProcedimentoNome, depois.ProcedimentoNome));
        }

        return alteracoes;
    }

    /// <summary>
    /// A linha do SISREG fala do MESMO procedimento que este item do pedido? É o que separa
    /// "outro procedimento do mesmo pedido" (vira item novo) de "o mesmo item, relido" (compara data
    /// e executante) — ticket #149.
    ///
    /// <para><b>Código primeiro, nome depois.</b> O <c>pa</c> identifica quando os DOIS lados o
    /// têm; quando falta num dos lados (pedido manual, varredura da agenda), quem decide é o nome. O
    /// SIGTAP NÃO entra: os joelhos direito e esquerdo do caso que abriu o ticket têm o mesmo SIGTAP e
    /// são dois itens.</para>
    ///
    /// <para>Sem identidade nenhuma de um dos lados (nem código nem nome), responde "mesmo": não
    /// dá para afirmar que é outro procedimento, e inventar item seria o erro oposto ao do ticket.</para>
    /// </summary>
    public static bool MesmoProcedimento(string? codigoA, string? nomeA, string? codigoB, string? nomeB)
    {
        var ca = codigoA?.Trim();
        var cb = codigoB?.Trim();
        if (!string.IsNullOrEmpty(ca) && !string.IsNullOrEmpty(cb))
            return string.Equals(ca, cb, StringComparison.Ordinal);

        var na = nomeA?.Trim();
        var nb = nomeB?.Trim();
        if (!string.IsNullOrEmpty(na) && !string.IsNullOrEmpty(nb))
            return string.Equals(na, nb, StringComparison.OrdinalIgnoreCase);

        return true;
    }

    /// <summary>
    /// Mudou de verdade? Ignora diferença só de espaço/caixa, e não confunde com troca nenhuma das
    /// duas pontas vazias:
    ///
    /// <list type="bullet">
    ///   <item><b>Sumiu</b> (<c>valor → vazio</c>): campo que o SISREG deixou de mandar não é
    ///   informação nova sobre a agenda.</item>
    ///   <item><b>Apareceu</b> (<c>vazio → valor</c>): é o campo sendo <b>preenchido</b>, não
    ///   trocado. O <c>pa</c> do procedimento vem em branco em ~1/3 das linhas; quando o SISREG
    ///   passa a mandá-lo, ninguém mudou o procedimento de ninguém — nós é que passamos a saber
    ///   qual era. Ler isso como troca gerou 642 linhas de fila num dia só em 06/09/2026, todas
    ///   <c>vazio → código</c>, todas falsas, afogando as alterações de verdade.</item>
    /// </list>
    /// </summary>
    private static bool Mudou(string? antes, string? depois)
    {
        var d = depois?.Trim();
        if (string.IsNullOrEmpty(d)) return false;

        var a = antes?.Trim();
        if (string.IsNullOrEmpty(a)) return false;

        return !string.Equals(a, d, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A solicitação só <b>ganhou a hora</b> que não tinha? Então não é remarcação.
    ///
    /// <para>Solicitação importada sem horário fica com meia-noite. Quando o SISREG passa a mandar
    /// a hora, o instante muda e a comparação crua acusava "Remarcado" — mesma família de
    /// <c>vazio → valor</c> que já derrubou 644 linhas de procedimento, só que escondida atrás de
    /// um campo de data. As duas alterações que sobraram da limpeza de 07/09/2026 eram exatamente
    /// isto: <c>20/08 00:00 → 20/08 14:00</c> e <c>15/09 00:00 → 15/09 15:30</c>.</para>
    ///
    /// <para><b>A regra é estreita de propósito:</b> exige o <b>mesmo dia</b> e meia-noite do lado
    /// antigo. Mover de 08:00 para 14:00 no mesmo dia continua sendo remarcação — e é remarcação
    /// que o paciente precisa saber.</para>
    /// </summary>
    private static bool SoGanhouHora(DateTime? antesUtc, DateTime depoisUtc)
    {
        if (antesUtc is not { } antes) return false;

        var a = FusoBrasilia.ParaExibicao(antes);
        var d = FusoBrasilia.ParaExibicao(depoisUtc);

        return a.Date == d.Date && a.TimeOfDay == TimeSpan.Zero;
    }

    /// <summary>
    /// Formato legível para a tela — quem lê a alteração precisa entender sem decodificar.
    ///
    /// <para><b>Em Brasília, não em UTC.</b> Formatar o instante cru punha três horas a mais na
    /// cara do operador: a linha dizia "Para 15/09 18:30" num agendamento das 15:30. Errado o
    /// bastante para alguém repassar o horário errado ao paciente — e contra a régua única de fuso
    /// do projeto.</para>
    /// </summary>
    private static string? Data(DateTime? utc) =>
        utc is { } u ? FusoBrasilia.ParaExibicao(u).ToString("dd/MM/yyyy HH:mm") : null;

    /// <summary>O mesmo formato de <c>valor_antes</c>/<c>valor_depois</c> da remarcação — é por ele que
    /// o aviso automático confere se a data nova ainda é a da solicitação.</summary>
    internal static string? FormatarData(DateTime? utc) => Data(utc);
}
