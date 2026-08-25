namespace SMSMais.Core.Sernit;

/// <summary>
/// Como um campo de múltipla escolha do SERNIT (checkbox) cabe num
/// <c>Dictionary&lt;string,string&gt;</c>: o JSF posta o MESMO nome uma vez por opção marcada, mas
/// o rascunho guarda par nome→valor. Convenção: os valores viajam juntos, separados por quebra de
/// linha (segura — as opções vêm do <c>value</c> do SERNIT, uma por linha), e só se desdobram na
/// hora de montar o POST (quando o envio for ligado, quem monta o corpo chama <see cref="Separar"/>).
/// </summary>
public static class SernitValorMultiplo
{
    public const char Separador = '\n';

    public static string Juntar(IEnumerable<string> valores) =>
        string.Join(Separador, valores.Where(v => !string.IsNullOrWhiteSpace(v)));

    public static IReadOnlyList<string> Separar(string? valor) =>
        string.IsNullOrWhiteSpace(valor)
            ? []
            : [.. valor.Split(Separador, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
}
