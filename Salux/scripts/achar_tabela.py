"""Acha em qual owner está uma tabela específica."""
from __future__ import annotations

import sys

from conexao import executar_select


def main() -> int:
    if len(sys.argv) < 2:
        print("uso: python scripts/achar_tabela.py <TABELA>")
        return 2
    tabela = sys.argv[1].upper()
    linhas, colunas = executar_select(
        "SELECT owner, table_name, num_rows FROM dba_tables WHERE table_name = :t ORDER BY owner",
        {"t": tabela},
    )
    if not linhas:
        # tenta synonyms
        linhas, colunas = executar_select(
            "SELECT owner, synonym_name, table_owner, table_name FROM dba_synonyms WHERE synonym_name = :t",
            {"t": tabela},
        )
        print("=== synonyms ===")
    else:
        print("=== dba_tables ===")
    print(" | ".join(colunas))
    for l in linhas:
        print(" | ".join(l))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
