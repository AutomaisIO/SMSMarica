"""Busca queries no snapshot.jsonl por substring ou tabela."""
from __future__ import annotations

import json
import pathlib
import re
import sys

_RAIZ = pathlib.Path(__file__).resolve().parent.parent
_HIST = _RAIZ / "capturas"


def main() -> int:
    if len(sys.argv) < 2:
        print("uso: python scripts/buscar_query.py <termo|tab:NOME>")
        return 2
    termo = " ".join(sys.argv[1:])
    arquivo = _HIST / "snapshot_sql_1h.jsonl"
    if not arquivo.exists():
        print("snapshot inexistente"); return 1

    eh_tab = termo.upper().startswith("TAB:")
    if eh_tab:
        nome = termo[4:].upper()
        pattern = re.compile(rf"\b(from|join|into|update)\s+{re.escape(nome)}\b", re.IGNORECASE)

    achei = 0
    for linha in arquivo.read_text(encoding="utf-8").splitlines():
        try:
            o = json.loads(linha)
        except Exception:
            continue
        sql = o.get("sql_text", "")
        match = pattern.search(sql) if eh_tab else (termo.lower() in sql.lower())
        if match:
            achei += 1
            print(f"\n--- {o['sql_id']} (execs={o.get('executions')}) ---")
            print(sql)
    print(f"\n{achei} matches")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
