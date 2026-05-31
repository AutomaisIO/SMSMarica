"""Consolida todos os pbd_*_sql.txt em um catálogo de:
- tabela → módulos que tocam
- função F_* → módulos que chamam
- contagem de SELECT/INSERT/UPDATE/DELETE por módulo
"""
from __future__ import annotations

import pathlib
import re
from collections import Counter, defaultdict

_RAIZ = pathlib.Path(__file__).resolve().parent.parent
_CAPS = _RAIZ / "capturas"
_OUT = _CAPS / "catalogo_pbd.md"

_TABELA_PAT = re.compile(r"\b(?:from|join|into|update)\s+([A-Za-z_][\w$]*)", re.IGNORECASE)
_FUNCAO_PAT = re.compile(r"\b(F_[A-Z][A-Z0-9_]+)\s*\(", re.IGNORECASE)
_SELECT_PAT = re.compile(r"^\s*SELECT\b", re.IGNORECASE | re.MULTILINE)
_INSERT_PAT = re.compile(r"^\s*INSERT\b", re.IGNORECASE | re.MULTILINE)
_UPDATE_PAT = re.compile(r"^\s*UPDATE\b", re.IGNORECASE | re.MULTILINE)
_DELETE_PAT = re.compile(r"^\s*DELETE\b", re.IGNORECASE | re.MULTILINE)


def main() -> int:
    tabela_modulos: dict[str, set[str]] = defaultdict(set)
    funcao_modulos: dict[str, set[str]] = defaultdict(set)
    contagem_por_modulo: dict[str, Counter] = {}

    arquivos = sorted(_CAPS.glob("pbd_*_sql.txt"))
    print(f"processando {len(arquivos)} arquivos...")
    for arq in arquivos:
        nome_mod = arq.name.replace("pbd_", "").replace("_sql.txt", "")
        try:
            texto = arq.read_text(encoding="utf-8", errors="replace")
        except Exception as e:
            print(f"  skip {nome_mod}: {e}"); continue

        tabs = {m.group(1).upper() for m in _TABELA_PAT.finditer(texto)}
        for t in tabs:
            if t in {"DUAL", "SYS"}:
                continue
            tabela_modulos[t].add(nome_mod)

        funcs = {m.group(1).upper() for m in _FUNCAO_PAT.finditer(texto)}
        for f in funcs:
            funcao_modulos[f].add(nome_mod)

        contagem_por_modulo[nome_mod] = Counter({
            "SELECT": len(_SELECT_PAT.findall(texto)),
            "INSERT": len(_INSERT_PAT.findall(texto)),
            "UPDATE": len(_UPDATE_PAT.findall(texto)),
            "DELETE": len(_DELETE_PAT.findall(texto)),
        })
        print(f"  {nome_mod:35s}  tabelas={len(tabs):5d}  funcoes={len(funcs):4d}")

    # Output Markdown
    with _OUT.open("w", encoding="utf-8") as md:
        md.write("# Catálogo SQL extraído dos PBDs\n\n")
        md.write(f"Compilado de {len(arquivos)} arquivos `pbd_*_sql.txt`.\n\n")

        md.write("## Contagem de comandos por módulo\n\n")
        md.write("| Módulo | SELECT | INSERT | UPDATE | DELETE | total |\n")
        md.write("|---|---:|---:|---:|---:|---:|\n")
        for mod, cnt in sorted(contagem_por_modulo.items(), key=lambda x: -sum(x[1].values())):
            total = sum(cnt.values())
            md.write(f"| {mod} | {cnt['SELECT']} | {cnt['INSERT']} | {cnt['UPDATE']} | {cnt['DELETE']} | {total} |\n")

        md.write(f"\n## Tabelas mais referenciadas (top 80 por nº de módulos)\n\n")
        md.write("Tabelas referenciadas por mais módulos são as 'core' do sistema.\n\n")
        md.write("| Tabela | # módulos | Módulos |\n|---|---:|---|\n")
        for t, mods in sorted(tabela_modulos.items(), key=lambda x: (-len(x[1]), x[0]))[:80]:
            md.write(f"| {t} | {len(mods)} | {', '.join(sorted(mods))} |\n")

        md.write(f"\n## Total: {len(tabela_modulos)} tabelas distintas mencionadas no app\n\n")

        md.write(f"\n## Funções F_* / procedures invocadas (top 60)\n\n")
        md.write("| Função | # módulos | Módulos |\n|---|---:|---|\n")
        for f, mods in sorted(funcao_modulos.items(), key=lambda x: (-len(x[1]), x[0]))[:60]:
            md.write(f"| {f} | {len(mods)} | {', '.join(sorted(mods))} |\n")

        md.write(f"\n## Total: {len(funcao_modulos)} funções F_* distintas chamadas\n")

    print(f"\nGravado em {_OUT}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
