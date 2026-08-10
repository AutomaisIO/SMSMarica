namespace SMSMarica.Core.Ser;

/// <summary>
/// Como um campo de <b>múltipla escolha</b> do SER (checkbox) cabe num
/// <c>Dictionary&lt;string, string&gt;</c>.
///
/// <para><b>O problema:</b> o JSF posta um <c>selectManyCheckbox</c> repetindo o MESMO nome uma
/// vez por opção marcada — <c>form0:dinamico_id_594=Diabetes&amp;form0:dinamico_id_594=Depressão</c>.
/// O rascunho guarda os campos como par nome→valor, que só comporta um valor.</para>
///
/// <para><b>A convenção:</b> os valores viajam juntos, separados por quebra de linha, e só se
/// desdobram na hora de montar o POST. Quebra de linha é segura porque as opções não são texto
/// livre: vêm do atributo <c>value</c> do próprio SER, uma linha cada ("Diabetes",
/// "Doenças articulares", "Tipo A"). Nenhuma pode conter <c>\n</c>.</para>
///
/// <para>Esta classe existe para essa regra morar em UM lugar — o dia em que o envio ao SER for
/// ligado, quem montar o corpo do POST chama <see cref="Separar"/> e emite um par por item, em
/// vez de mandar tudo grudado num valor só e ver o pedido ser recusado sem explicação.</para>
/// </summary>
public static class SerValorMultiplo
{
    public const char Separador = '\n';

    /// <summary>Junta as opções marcadas num único valor guardável no rascunho.</summary>
    public static string Juntar(IEnumerable<string> valores) =>
        string.Join(Separador, valores.Where(v => !string.IsNullOrWhiteSpace(v)));

    /// <summary>
    /// Desdobra o valor guardado nas opções que o SER espera receber repetidas.
    /// Campo de valor único devolve um item só — o chamador não precisa saber o tipo.
    /// </summary>
    public static IReadOnlyList<string> Separar(string? valor) =>
        string.IsNullOrWhiteSpace(valor)
            ? []
            : [.. valor.Split(Separador, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
}
