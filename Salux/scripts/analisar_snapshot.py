"""Analisa snapshot_sql_<N>h.jsonl: extrai tabelas mais usadas + JOIN patterns."""
from __future__ import annotations

import json
import pathlib
import re
import sys
from collections import Counter, defaultdict

_RAIZ = pathlib.Path(__file__).resolve().parent.parent
_HIST = _RAIZ / "capturas"


def _tabelas(sql: str) -> set[str]:
    # regex pra FROM/JOIN/UPDATE/INTO seguido de nome
    txt = sql.replace("\n", " ")
    matches = re.findall(r"\b(?:from|join|into|update)\s+([A-Za-z_][\w$]*)", txt, re.IGNORECASE)
    return {m.upper() for m in matches if m.upper() not in {"DUAL", "SYS", "TABLE"}}


def main() -> int:
    arquivo = _HIST / (sys.argv[1] if len(sys.argv) > 1 else "snapshot_sql_1h.jsonl")
    if not arquivo.exists():
        print(f"{arquivo} não existe"); return 1

    qtd = 0
    contagem_tab = Counter()
    cooccur: Counter[tuple[str, str]] = Counter()
    por_module = Counter()
    top_execs = []
    for linha in arquivo.read_text(encoding="utf-8").splitlines():
        try:
            o = json.loads(linha)
        except Exception:
            continue
        sql = o.get("sql_text", "")
        tabs = _tabelas(sql)
        for t in tabs:
            contagem_tab[t] += 1
        lst = sorted(tabs)
        for i in range(len(lst)):
            for j in range(i+1, len(lst)):
                cooccur[(lst[i], lst[j])] += 1
        por_module[o.get("module", "?")] += 1
        execs = o.get("executions") or "0"
        try:
            ne = int(execs)
        except Exception:
            ne = 0
        top_execs.append((ne, o.get("sql_id"), sql[:160]))
        qtd += 1

    print(f"=== {qtd} queries SELECT analisadas ===\n")
    print("Top 30 tabelas mais referenciadas em SELECTs ativos:")
    for t, n in contagem_tab.most_common(30):
        print(f"  {n:3d}  {t}")
    print("\nTop 20 co-ocorrências (pares de tabelas em mesmo SELECT — indica JOINs comuns):")
    for (a, b), n in cooccur.most_common(20):
        print(f"  {n:3d}  {a} + {b}")
    print("\nQueries por módulo (atribuído pelo Salux):")
    for m, n in por_module.most_common(20):
        print(f"  {n:3d}  {m or '(vazio)'}")
    print("\nTop 10 queries por #executions (na 1h):")
    top_execs.sort(reverse=True)
    for ne, sid, txt in top_execs[:10]:
        print(f"  {ne:>10}  {sid}  {txt}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
