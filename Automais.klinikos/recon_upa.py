"""Recon da UPA/Santa Rita (build 2025 `U.2025.06.1.26`, app /UPA24H). SOMENTE LEITURA.

Objetivo: levantar a UPA ao nível do Conde — catálogo de relatórios (rótulo + caminho + parrel)
a partir dos menus das home dos módulos, para achar os da ESPINHA (boletim, chegada, cor,
desfecho). A build 2025 tem gate e params próprios (ver docs/APRENDIZADOS.md §7), então aqui
usamos o gate porta+local e capturamos o que os menus da UPA oferecem — sem reusar o mapa do Conde.

Uso:  python recon_upa.py            (usa o .env: BASE=upa24h/starita24h, APP=/UPA24H, leonardo.dantas)
Saída: capturas/upa_modulo_*.html + lista de relatórios no stdout (sem PII).
"""

from __future__ import annotations

import re
import sys
from urllib.parse import urljoin

from klinikos.client import KlinikosSession, sopa, CAP, BASE, APP
from paridade_upa import gate_porta_local

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

# módulos candidatos (o mod pode diferir da build do Conde — tentamos vários)
MODULOS = ["UPA", "Acesso", "Cadastro", "Administracao", "eProntuario"]
RE_REL = re.compile(r'href="([^"]*(?:Relatorio|rptView|ParametroRelatorio)[^"]*)"', re.I)
RE_RM = re.compile(r'(rmText[^>]*>)([^<]{2,70})', re.I)


def achar_mod(s: KlinikosSession) -> dict[str, str]:
    """Descobre os caminhos de home dos módulos a partir do menu principal."""
    r = s.get(f"{APP}/Default.aspx")
    (CAP / "upa_home.html").write_text(r.text, encoding="utf-8")
    d = sopa(r.text)
    mods = {}
    for a in d.find_all("a", href=True):
        m = re.search(r"/(\w+)/Default\.aspx\?mod=(\d+)", a["href"])
        if m:
            mods[m.group(1)] = a["href"]
    return mods


def relatorios_da_pagina(html: str, base_url: str) -> list[tuple[str, str]]:
    """Extrai (rótulo, url) dos links de relatório; o rótulo vem do rmText do RadMenu quando dá."""
    out = []
    d = sopa(html)
    for a in d.find_all("a", href=True):
        if re.search(r"Relatorio|rptView|ParametroRelatorio", a["href"], re.I):
            rot = a.get_text(" ", strip=True) or ""
            sp = a.find("span", class_="rmText")
            if sp:
                rot = sp.get_text(" ", strip=True)
            out.append((rot[:60], urljoin(base_url, a["href"].replace("&amp;", "&"))))
    return out


def main() -> int:
    print(f"host: {BASE}  app: {APP}")
    s = KlinikosSession(); s.login(); gate_porta_local(s)
    print("cookies:", sorted(s.c.cookies.keys()))
    mods = achar_mod(s)
    print("módulos achados:", mods)
    achados: dict[str, tuple[str, str]] = {}
    for nome, href in (mods.items() or [(m, f"{APP}/{m}/Default.aspx") for m in MODULOS]):
        r = s.get(href if href.startswith("/") else urljoin(BASE, href))
        (CAP / f"upa_modulo_{nome}.html").write_text(r.text, encoding="utf-8")
        for rot, url in relatorios_da_pagina(r.text, str(r.url)):
            m = re.search(r"parrel=(\d+)", url, re.I)
            key = (m.group(1) if m else url)
            achados[key] = (rot, re.sub(r"https?://[^/]+", "", url))
    print(f"\n=== relatórios no menu da UPA: {len(achados)} ===")
    for k, (rot, url) in sorted(achados.items(), key=lambda kv: (len(kv[0]), kv[0])):
        print(f"  parrel={k:>5}  {rot[:42]:42}  {url}")
    s.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
