using SMSMais.Core.Regulacao.Regras;

namespace SMSMais.Core.Regulacao.Catalogo;

/// <summary>
/// A identidade de um recurso nos espelhos de catálogo dos sistemas de regulação (SER, SERNIT,
/// ESUS SG) é o <b>NOME</b>, nunca o número do combo.
///
/// <para><b>Por que (07/10/2026):</b> o <c>value</c> do combo de recursos é posição. A SES
/// renumera o combo inteiro quando acrescenta um recurso — medido em 22/09, 30/09 e 07/10/2026; no
/// último, o 1130 do SER era "Cirurgia Buco-Maxilo Facial" no nosso espelho e "Odontopediatria" no
/// SER, e as 422 origens do SER apontavam para números que já eram de outro recurso. Casando pelo
/// número, a linha do espelho era reaproveitada para o recurso que passou a ocupar aquela posição,
/// e quem estava ligado a ela trocava de recurso calado.</para>
///
/// <para>O nome do recurso não se repete dentro de um combo (medido em 07/10/2026: 148, 65, 124 e
/// 85 recursos nos quatro combos do SER, nenhum nome repetido). Se a SES <b>renomear</b> um
/// recurso, ele vira outro recurso para nós — a ligação antiga perde o par e alguém refaz o
/// pareamento. Falha visível, em vez de mandar o paciente para a especialidade errada.</para>
/// </summary>
public static class IdentidadePorNome
{
    /// <summary>A chave de identidade de um rótulo. Vazio = rótulo sem letra nem dígito.</summary>
    public static string Chave(string? rotulo) => ChaveRotulo.Normalizar(rotulo);

    /// <summary>
    /// As opções de um combo AO VIVO que têm o nome procurado. Quem chama decide o que fazer com
    /// zero (o sistema não oferece mais) ou com mais de uma (não escolher no chute).
    /// </summary>
    public static List<T> Achar<T>(IEnumerable<T> opcoes, Func<T, string> rotulo, string procurado)
    {
        var nome = Chave(procurado);
        return nome.Length == 0 ? [] : [.. opcoes.Where(o => Chave(rotulo(o)) == nome)];
    }

    /// <summary>
    /// Agrupa as linhas existentes de um combo pelo nome. Cada nome fica com UMA linha — a da
    /// listagem mais recente, que é a que tem o número de hoje e a que as origens seguem —, e as
    /// demais voltam como <c>Fundidas</c> para quem chama religar o que apontava para elas e
    /// apagá-las.
    ///
    /// <para>As fundidas existem por causa do regime antigo: cada renumeração deixava para trás
    /// uma linha com o nome no número velho (07/10/2026: 622 linhas para 422 recursos no SER).</para>
    /// </summary>
    public static (Dictionary<string, T> PorChave, List<(T Fundida, T Dona)> Fundidas) Consolidar<T>(
        IEnumerable<T> linhas, Func<T, string> rotulo, Func<T, DateTime> carimbo)
        where T : class
    {
        var porChave = new Dictionary<string, T>(StringComparer.Ordinal);
        var fundidas = new List<(T, T)>();

        foreach (var grupo in linhas.GroupBy(l => Chave(rotulo(l)), StringComparer.Ordinal))
        {
            if (grupo.Key.Length == 0) continue;

            var ordenadas = grupo.OrderByDescending(carimbo).ToList();
            var dona = ordenadas[0];
            porChave[grupo.Key] = dona;
            fundidas.AddRange(ordenadas.Skip(1).Select(f => (f, dona)));
        }

        return (porChave, fundidas);
    }
}
