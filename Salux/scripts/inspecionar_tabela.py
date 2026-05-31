"""Lista colunas de uma tabela do schema SYBSA via DBA_TAB_COLUMNS.

Uso:
    python scripts/inspecionar_tabela.py PACIENTE
    python scripts/inspecionar_tabela.py FIA
"""
from __future__ import annotations

import sys

from conexao import executar_select


def main() -> int:
    if len(sys.argv) < 2:
        print("uso: python scripts/inspecionar_tabela.py <TABELA>")
        return 2
    tabela = sys.argv[1].upper()
    linhas, colunas = executar_select(
        """
        SELECT column_id, column_name, data_type, data_length, nullable
        FROM dba_tab_columns
        WHERE owner = 'INFOSAUDE' AND table_name = :t
        ORDER BY column_id
        """,
        {"t": tabela},
    )
    if not linhas:
        print(f"Tabela SYBSA.{tabela} não encontrada (ou sem permissão).")
        return 1
    print(f"=== SYBSA.{tabela} — {len(linhas)} colunas ===")
    print(" | ".join(colunas))
    print("-" * 80)
    for l in linhas:
        print(" | ".join(l))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
