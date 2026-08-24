using System.Text.RegularExpressions;

namespace SMSMarica.Secretario.Api.Oracle;

/// <summary>
/// Guarda read-only do SQL: garante que é UM ÚNICO SELECT (ou WITH/EXPLAIN)
/// e bloqueia qualquer token de escrita (DML/DDL/DCL/TCL) ou múltiplos statements.
/// Espelha o <c>SqlReadOnlyGuard</c> do SMSMais.server (que por sua vez espelha
/// <c>Salux/scripts/_guard.py</c>) — peca pela segurança (aceita falso positivo se um
/// token bloqueado aparecer dentro de string literal). Aqui as consultas são constantes
/// do código, mas o guard roda mesmo assim: Oracle é produção viva de hospital.
/// </summary>
public static partial class SqlReadOnlyGuard
{
    // Início (ignorando comentários de linha/bloco) deve ser SELECT, WITH ou EXPLAIN.
    [GeneratedRegex(
        @"^\s*(?:--[^\n]*\n\s*|/\*[\s\S]*?\*/\s*)*(SELECT|WITH|EXPLAIN)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PrefixoPermitido();

    // Qualquer token de escrita/transação/DDL/DCL/procedimento.
    [GeneratedRegex(
        @"\b(INSERT|UPDATE|DELETE|MERGE|TRUNCATE|DROP|ALTER|CREATE|GRANT|REVOKE|" +
        @"COMMIT|ROLLBACK|SAVEPOINT|LOCK|CALL|EXECUTE|EXEC|BEGIN|DECLARE)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Bloqueados();

    /// <summary>
    /// Valida que <paramref name="sql"/> é uma consulta read-only única. Lança
    /// <see cref="InvalidOperationException"/> caso contrário.
    /// </summary>
    public static void GarantirLeitura(string? sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            throw new InvalidOperationException("SQL vazio.");
        }

        var normalizado = sql.Trim();

        // Múltiplos statements: ';' só é tolerado como terminador único no fim.
        var semFinal = normalizado.TrimEnd();
        if (semFinal.EndsWith(';'))
        {
            semFinal = semFinal[..^1].TrimEnd();
        }

        if (semFinal.Contains(';'))
        {
            throw new InvalidOperationException(
                "SQL bloqueado: múltiplos statements não são permitidos (';' interno).");
        }

        if (!PrefixoPermitido().IsMatch(normalizado))
        {
            throw new InvalidOperationException(
                "SQL bloqueado: deve começar com SELECT, WITH ou EXPLAIN.");
        }

        if (Bloqueados().IsMatch(semFinal))
        {
            throw new InvalidOperationException(
                "SQL contém token de escrita (INSERT/UPDATE/DELETE/MERGE/DDL/DCL/etc). Bloqueado.");
        }
    }
}
