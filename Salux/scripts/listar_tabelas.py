"""Lista tabelas INFOSAUDE filtrando por padrão de nome.

Uso:
    python scripts/listar_tabelas.py EDOC
    python scripts/listar_tabelas.py DOCUMENTO
    python scripts/listar_tabelas.py FORMULARIO
    python scripts/listar_tabelas.py %                # todas
"""
from __future__ import annotations

import sys

from conexao import executar_select


def main() -> int:
    padrao = sys.argv[1] if len(sys.argv) > 1 else "%"
    if "%" not in padrao:
        padrao = f"%{padrao}%"
    linhas, colunas = executar_select(
        """
        SELECT table_name, num_rows, last_analyzed
        FROM dba_tables
        WHERE owner = 'INFOSAUDE'
          AND table_name LIKE :p
        ORDER BY table_name
        """,
        {"p": padrao.upper()},
    )
    print(f"=== INFOSAUDE LIKE '{padrao.upper()}' — {len(linhas)} tabelas ===")
    print(" | ".join(colunas))
    print("-" * 90)
    for l in linhas:
        print(" | ".join(l))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
