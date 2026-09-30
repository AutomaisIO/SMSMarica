"""Catálogo estático do e-SUS SG: todo endpoint e toda operação GraphQL que o front conhece.

Baixa o `index.html` do front (:8000), acha o bundle `/assets/index-*.js` (~14 MB, um só) e
extrai por regex:

- endpoints do LEGADO PHP (:9001) — `/<modulo>/controller-<x>/<acao>`;
- endpoints do REST novo (:8001) — `/access-control/...`, `/unit-health/...`, etc.;
- operações GraphQL (`query Nome(...)` / `mutation Nome(...)`).

Não faz login nem toca dado — é leitura do JavaScript público. Serve para achar a tela antes
de abrir o navegador: se a ação procurada não está aqui, o front não a chama.

Uso:  python catalogar_endpoints.py                 # grava docs/ENDPOINTS.md
      python catalogar_endpoints.py --filtro fila   # só imprime o que casar
"""

from __future__ import annotations

import argparse
import collections
import datetime as dt
import pathlib
import re
import sys

import httpx

from esus.client import CAP, FRONT, UA

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
RAIZ = pathlib.Path(__file__).resolve().parent


def baixar_bundle() -> tuple[str, str]:
    with httpx.Client(headers={"User-Agent": UA}, timeout=120) as c:
        html = c.get(FRONT + "/").text
        m = re.search(r'src="(/assets/index-[^"]+\.js)"', html)
        if not m:
            raise SystemExit("bundle /assets/index-*.js não encontrado no index.html")
        js = c.get(FRONT + m.group(1)).text
    (CAP / "bundle.js").write_text(js, encoding="utf-8")
    return m.group(1), js


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--filtro", help="regex; só imprime o que casar (não grava o .md)")
    a = ap.parse_args()

    nome, js = baixar_bundle()
    # o front escreve com e sem a barra inicial ("exames2/controller-.../buscar") — normaliza
    legado = sorted(set("/" + p for p in re.findall(
        r'["`]/?([a-z0-9_-]+/controller-[a-z0-9-]+/[a-zA-Z0-9_-]+)', js)))
    rest = sorted(set(
        p for p in re.findall(r'["`](/(?:access-control|unit-health|client|integration|person|patient|'
                              r'employee|sector|appointment|exam|hospitalization|report|billing|tfd|'
                              r'emergency|pharmacy|transport|notification)[a-zA-Z0-9/_-]*)', js)
        if "controller-" not in p))
    ops = collections.defaultdict(set)
    for tipo, op in re.findall(r"\b(query|mutation)\s+([A-Za-z_][A-Za-z0-9_]*)\s*[({]", js):
        ops[tipo].add(op)

    if a.filtro:
        rx = re.compile(a.filtro, re.I)
        for rot, itens in (("legado", legado), ("rest", rest),
                           ("query", sorted(ops["query"])), ("mutation", sorted(ops["mutation"]))):
            for i in itens:
                if rx.search(i):
                    print(f"{rot:9s} {i}")
        return 0

    por_mod = collections.defaultdict(list)
    for p in legado:
        por_mod[p.split("/")[1]].append(p)

    md = [
        "# e-SUS SG — catálogo de endpoints (gerado)",
        "",
        f"> Gerado por `catalogar_endpoints.py` em {dt.date.today():%d/%m/%Y} a partir de `{nome}`.",
        "> **Não editar à mão** — rode o script de novo. É leitura estática do JavaScript público do",
        "> front: lista o que o front *sabe chamar*, não o que o usuário tem permissão de usar.",
        "> Escrita (ações `salvar`/`excluir`/`agendar`..., `mutation`) aparece aqui só como mapa —",
        "> a trava de `esus/client.py` recusa todas.",
        "",
        f"Totais: **{len(legado)}** ações do legado PHP (:9001), **{len(rest)}** caminhos REST (:8001), "
        f"**{len(ops['query'])}** queries e **{len(ops['mutation'])}** mutations GraphQL.",
        "",
        "## 1. Legado PHP (:9001) — por módulo",
        "",
    ]
    for mod in sorted(por_mod):
        md.append(f"### `{mod}` ({len(por_mod[mod])})")
        md.append("")
        md += [f"- `{p}`" for p in por_mod[mod]]
        md.append("")
    md += ["## 2. REST novo (:8001)", ""] + [f"- `{p}`" for p in rest] + [""]
    md += ["## 3. GraphQL — queries", "", ", ".join(f"`{o}`" for o in sorted(ops["query"])), ""]
    md += ["## 4. GraphQL — mutations (recusadas pela trava)", "",
           ", ".join(f"`{o}`" for o in sorted(ops["mutation"])), ""]
    destino = RAIZ / "docs" / "ENDPOINTS.md"
    destino.parent.mkdir(exist_ok=True)
    destino.write_text("\n".join(md), encoding="utf-8")
    print(f"{len(legado)} legado, {len(rest)} REST, {len(ops['query'])} queries, "
          f"{len(ops['mutation'])} mutations -> {destino}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
