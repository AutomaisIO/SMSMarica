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
/// </summary>
public static class ComparadorMarcacao
{
    public static IReadOnlyList<AlteracaoDetectada> Comparar(FotoMarcacao antes, FotoMarcacao depois)
    {
        var alteracoes = new List<AlteracaoDetectada>();

        // Campo vazio de qualquer um dos dois lados nunca vira alteração (ver `Mudou`): o SISREG
        // deixa campo em branco com frequência — o código do procedimento vem vazio em ~1/3 das
        // linhas — e tanto "sumiu" quanto "apareceu" são notícia sobre o ARQUIVO, não sobre a
        // agenda. Ler qualquer um dos dois como troca enche a tela de alterações fantasmas.
        if (depois.DataAgendadaUtc is { } nova && antes.DataAgendadaUtc != nova)
        {
            alteracoes.Add(new(
                TipoAlteracaoAgenda.DataHora,
                Data(antes.DataAgendadaUtc),
                Data(nova)));
        }

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

    /// <summary>Formato legível para a tela — quem lê a alteração precisa entender sem decodificar.</summary>
    private static string? Data(DateTime? utc) => utc?.ToString("dd/MM/yyyy HH:mm");
}
