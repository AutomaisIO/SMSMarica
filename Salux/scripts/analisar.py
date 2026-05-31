"""Lê capturas/historico.jsonl e gera docs/queries/<rotulo>.md agrupado por marcas.

Uso:
    python scripts/analisar.py                            # gera "historico-completo.md"
    python scripts/analisar.py prontuario                 # filtra marcas que contém "prontuario" (case-insens) e nomes o md
    python scripts/analisar.py prontuario --rotulo prontuario-paciente
"""
from __future__ import annotations

import argparse
import json
import pathlib
import re
from collections import defaultdict

_RAIZ = pathlib.Path(__file__).resolve().parent.parent
_HIST = _RAIZ / "capturas" / "historico.jsonl"
_DOCS = _RAIZ / "docs" / "queries"


def _carregar() -> list[dict]:
    if not _HIST.exists():
        return []
    out: list[dict] = []
    for linha in _HIST.read_text(encoding="utf-8", errors="replace").splitlines():
        try:
            out.append(json.loads(linha))
        except Exception:
            continue
    return out


def _agrupar(entradas: list[dict]) -> list[dict]:
    """Retorna lista de seções: cada seção tem `marca` e `queries`.

    A primeira seção (antes da 1ª marca) é nomeada "_inicial_".
    """
    secoes: list[dict] = [{"marca": "_inicial_", "ts": None, "queries": []}]
    for e in entradas:
        if e["tipo"] == "marca":
            secoes.append({"marca": e["texto"], "ts": e.get("ts_banco"), "queries": []})
        elif e["tipo"] == "query":
            secoes[-1]["queries"].append(e)
    return secoes


def _tabelas(sql: str) -> list[str]:
    candidatos = re.findall(r"\b(?:from|join|into|update)\s+([A-Za-z_][\w$]*)", sql or "", re.IGNORECASE)
    vistos: list[str] = []
    for t in candidatos:
        tu = t.upper()
        if tu not in vistos and tu not in ("DUAL",):
            vistos.append(tu)
    return vistos


def _escrever_md(rotulo: str, secoes: list[dict]) -> pathlib.Path:
    _DOCS.mkdir(parents=True, exist_ok=True)
    arq = _DOCS / f"{rotulo}.md"
    total = sum(len(s["queries"]) for s in secoes)
    com_marca = [s for s in secoes if s["marca"] != "_inicial_" or s["queries"]]
    with arq.open("w", encoding="utf-8") as f:
        f.write(f"# Queries: {rotulo}\n\n")
        f.write(f"Total: {total} queries em {len(com_marca)} seção(ões)\n\n")
        for s in com_marca:
            f.write(f"## {s['marca']}")
            if s["ts"]:
                f.write(f"  _(ts_banco: {s['ts']})_")
            f.write("\n\n")
            if not s["queries"]:
                f.write("_(sem queries nesta seção)_\n\n")
                continue
            for i, q in enumerate(s["queries"], 1):
                tabs = ", ".join(_tabelas(q.get("sql_text", "")))
                f.write(f"### {i}. SQL_ID `{q['sql_id']}`\n\n")
                f.write(f"- last_active: {q.get('last_active')}\n")
                f.write(f"- executions: {q.get('executions')}\n")
                if tabs:
                    f.write(f"- tabelas: {tabs}\n")
                if q.get("module"):
                    f.write(f"- module: {q.get('module')}\n")
                f.write("\n```sql\n")
                f.write((q.get("sql_text") or "").rstrip())
                f.write("\n```\n\n")
                if q.get("binds"):
                    f.write("**Binds:**\n\n")
                    for b in q["binds"]:
                        f.write(
                            f"- `:{b.get('NAME','?')}` = `{b.get('VALUE_STRING','')}` "
                            f"({b.get('DATATYPE_STRING','?')})\n"
                        )
                    f.write("\n")
    return arq


def main() -> int:
    p = argparse.ArgumentParser()
    p.add_argument("filtro", nargs="?", default=None, help="substring para filtrar marcas")
    p.add_argument("--rotulo", default=None, help="nome do .md de saída")
    args = p.parse_args()

    entradas = _carregar()
    if not entradas:
        print(f"{_HIST} vazio ou inexistente")
        return 1

    secoes = _agrupar(entradas)
    if args.filtro:
        f_lower = args.filtro.lower()
        secoes = [s for s in secoes if f_lower in (s["marca"] or "").lower()]
        if not secoes:
            print(f"nenhuma seção casa com '{args.filtro}'")
            return 1

    rotulo = args.rotulo or (args.filtro or "historico-completo")
    rotulo = re.sub(r"\s+", "-", rotulo.strip().lower())
    arq = _escrever_md(rotulo, secoes)
    print(f"gerado: {arq}")
    print(f"{sum(len(s['queries']) for s in secoes)} queries em {len(secoes)} seções")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
