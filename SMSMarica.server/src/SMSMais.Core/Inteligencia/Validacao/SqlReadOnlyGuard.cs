using System.Text.RegularExpressions;
using SMSMais.Core.Common.Excecoes;

namespace SMSMais.Core.Inteligencia.Validacao;

/// <summary>
/// Guarda read-only do SQL gerado pela IA: garante que é UM ÚNICO SELECT (ou WITH/EXPLAIN)
/// e bloqueia qualquer token de escrita (DML/DDL/DCL/TCL) ou múltiplos statements.
/// Espelha a lógica de <c>Salux/scripts/_guard.py</c> — peca pela segurança (aceita falso
/// positivo se um token bloqueado aparecer dentro de string literal).
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
    /// <see cref="ValidacaoException"/> caso contrário.
    /// </summary>
    public static void GarantirLeitura(string? sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            throw new ValidacaoException("ia.sql", "SQL vazio: a IA não gerou uma consulta.");
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
            throw new ValidacaoException(
                "ia.sql",
                "SQL bloqueado: múltiplos statements não são permitidos (';' interno).");
        }

        if (!PrefixoPermitido().IsMatch(normalizado))
        {
            throw new ValidacaoException(
                "ia.sql",
                "SQL bloqueado: deve começar com SELECT, WITH ou EXPLAIN.");
        }

        if (Bloqueados().IsMatch(semFinal))
        {
            throw new ValidacaoException(
                "ia.sql",
                "SQL contém token de escrita (INSERT/UPDATE/DELETE/MERGE/DDL/DCL/etc). Bloqueado.");
        }
    }
}
