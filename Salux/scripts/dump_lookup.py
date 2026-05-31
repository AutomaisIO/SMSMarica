"""Dump rápido de tabela de lookup (poucas linhas) — só salva como txt."""
from __future__ import annotations

import pathlib
import sys

from conexao import executar_select

_SAIDA = pathlib.Path(__file__).resolve().parent.parent / "capturas"


def main() -> int:
    if len(sys.argv) < 2:
        print("uso: python scripts/dump_lookup.py <tabela> [order_by]")
        return 2
    tabela = sys.argv[1].upper()
    order = sys.argv[2] if len(sys.argv) > 2 else "1"
    sql = f"SELECT * FROM {tabela} ORDER BY {order}"
    linhas, cols = executar_select(sql, modo="supervisor", timeout=120)
    print(" | ".join(cols))
    print("-" * 120)
    for l in linhas:
        print(" | ".join(l))
    print(f"Total: {len(linhas)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
