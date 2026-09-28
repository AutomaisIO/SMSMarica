"""Catálogo de relatórios do Klinikos a partir dos menus capturados. Não acessa a rede.

Lê `capturas/modulo_*.html` (homes dos módulos, salvas por probe_tela/probe_mapa) e extrai do
RadMenu renderizado (`<a class="rmLink" href=...><span class="rmText">Rótulo</span>`) o caminho
de menu e a URL de cada item. Separa os que são relatório:

- `Relatorios/ParametroRelatorio.aspx?parrel=N&Modulo=X` — tela de parâmetros (datas etc.)
- `<Modulo>/Relatorios/Relatorio.aspx?parrel=N&origem=K` — relatório com parâmetro próprio
- `Relatorios/rptView.aspx?parrel=N&parNum=1&par1=unidDef` — relatório direto (sem parâmetro)

Imprime a tabela `parrel | módulo | caminho no menu | rótulo | URL` e grava
`capturas/catalogo_relatorios.csv`. Sem PII (só rótulos de menu).

Uso:  python catalogo_relatorios.py
"""

from __future__ import annotations

import csv
import pathlib
import re
import sys
from urllib.parse import parse_qs, urlsplit

from bs4 import BeautifulSoup

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
CAP = pathlib.Path(__file__).resolve().parent / "capturas"


def caminho_menu(a) -> str:
    """Sobe pelos <li class="rmItem"> até a raiz e junta os rótulos dos pais."""
    partes = []
    li = a.find_parent("li")
    while li is not None:
        link = li.find("a", recursive=False)
        if link is not None:
            txt = link.find("span", class_="rmText")
            if txt is not None:
                partes.append(txt.get_text(" ", strip=True))
        li = li.find_parent("li")
    return " > ".join(reversed(partes[1:]))  # sem o próprio item


def main() -> int:
    itens: dict[tuple, dict] = {}
    todos = 0
    for arq in sorted(CAP.glob("modulo_*.html")):
        modulo = arq.stem.replace("modulo_", "")
        d = BeautifulSoup(arq.read_text(encoding="utf-8", errors="replace"), "html.parser")
        for a in d.find_all("a", class_="rmLink"):
            href = (a.get("href") or "").replace("&amp;", "&")
            rot = a.find("span", class_="rmText")
            rotulo = rot.get_text(" ", strip=True) if rot else a.get_text(" ", strip=True)
            if not href or not href.lower().endswith(".aspx") and ".aspx?" not in href.lower():
                continue
            todos += 1
            p = urlsplit(href)
            q = {k.lower(): v for k, v in parse_qs(p.query).items()}  # Acesso usa `parRel`
            parrel = (q.get("parrel") or [""])[0]
            eh_rel = bool(parrel) or "/relatorios/" in p.path.lower()
            chave = (p.path.lower(), p.query.lower())
            if chave in itens:
                continue
            itens[chave] = {
                "modulo_home": modulo, "parrel": parrel, "relatorio": eh_rel,
                "caminho": caminho_menu(a), "rotulo": rotulo, "url": href,
                "tipo": ("parametro" if "parametrorelatorio" in p.path.lower()
                         else "rptview" if "rptview" in p.path.lower()
                         else "relatorio" if re.search(r"/relatorios?\.aspx", p.path.lower())
                         else "rel-proprio" if eh_rel
                         else "tela"),
            }

    rels = [v for v in itens.values() if v["relatorio"]]
    telas = [v for v in itens.values() if not v["relatorio"]]
    print(f"{todos} itens de menu lidos; {len(itens)} URLs únicas; {len(rels)} relatórios; {len(telas)} telas\n")
    print("== RELATÓRIOS ==")
    for v in sorted(rels, key=lambda v: (v["modulo_home"], v["caminho"], v["rotulo"])):
        print(f"  {v['parrel']:>5} {v['tipo']:9} {v['modulo_home']:15} {v['caminho'][:55]:55} | {v['rotulo'][:60]:60} {v['url']}")
    print("\n== TELAS (não relatório) ==")
    for v in sorted(telas, key=lambda v: (v["modulo_home"], v["caminho"], v["rotulo"])):
        print(f"        {v['modulo_home']:15} {v['caminho'][:55]:55} | {v['rotulo'][:60]:60} {v['url']}")

    with open(CAP / "catalogo_menu.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=["modulo_home", "tipo", "parrel", "caminho", "rotulo", "url"])
        w.writeheader()
        for v in sorted(itens.values(), key=lambda v: (v["modulo_home"], v["caminho"], v["rotulo"])):
            w.writerow({k: v[k] for k in w.fieldnames})
    print("\n-> capturas/catalogo_menu.csv")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
