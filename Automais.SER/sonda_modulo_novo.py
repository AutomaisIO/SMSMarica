"""Como se ativa o módulo no SER redesenhado?

Fatos já medidos: `goModulo` sumiu; a home entrega o CSS dos "module-card" mas não os cards; a tela
de Solicitação responde 500 sem módulo ativo; e os links do menu usam o padrão Seam
`actionMethod=home.xhtml:...&cid=NNNNN`.

Esta sonda segue a "página principal" pelo actionMethod e procura, no que voltar, o mecanismo de
entrada nos módulos. Leitura pura (GETs).

Uso:  python sonda_modulo_novo.py
"""
from __future__ import annotations

import os
import pathlib
import re
import sys

import httpx
from bs4 import BeautifulSoup
from dotenv import load_dotenv

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

RAIZ = pathlib.Path(__file__).parent
load_dotenv(RAIZ / ".env")
BASE = os.environ.get("SER_BASE_URL", "https://ser.saude.rj.gov.br")
UA = ("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) "
      "Chrome/126.0 Safari/537.36")


def campos_do_form(doc, form_id):
    form = doc.find("form", id=form_id)
    if form is None:
        return {}
    out = {}
    for el in form.find_all(["input", "select", "textarea"]):
        nome = el.get("name")
        if not nome or el.has_attr("disabled"):
            continue
        if (el.get("type") or "").lower() in ("submit", "button", "image", "reset"):
            continue
        out[nome] = el.get("value") or ""
    return out


def viewstate(html):
    m = re.search(r'name="javax\.faces\.ViewState"[^>]*value="([^"]*)"', html)
    return m.group(1) if m else None


def diagnostico(rotulo: str, html: str, cap: pathlib.Path, arquivo: str) -> None:
    (cap / arquivo).write_text(html, encoding="utf-8")
    cards = re.findall(r'<[a-zA-Z]+[^>]*data-module="([^"]+)"[^>]*>', html)
    print(f"  {rotulo}: {len(html)} bytes | cards renderizados: {cards or '(nenhum)'}"
          f" | goModulo: {html.count('goModulo')} | -> {arquivo}")


def main() -> int:
    usuario, senha = os.environ.get("SER_USUARIO", ""), os.environ.get("SER_SENHA", "")
    cap = RAIZ / "capturas"
    cap.mkdir(exist_ok=True)

    with httpx.Client(base_url=BASE, headers={"User-Agent": UA}, timeout=90,
                      follow_redirects=True, verify=False) as c:
        d = BeautifulSoup(c.get("/ser/login").text, "html.parser")
        fl = d.find("form", id="login")
        r = c.post(fl.get("action"), data=campos_do_form(d, "login") | {
            "login": "login", "login:username": usuario, "login:password": senha,
            "login:entrar": "Entrar",
            "javax.faces.ViewState": viewstate(str(fl)) or "j_id1",
        })
        if 'id="login:username"' in r.text:
            print("!! LOGIN FALHOU"); return 1
        print("[1] login OK")

        home = c.get("/ser/home.seam").text
        diagnostico("[2] home.seam         ", home, cap, "nv_home.html")

        cid = None
        m = re.search(r"[?&]cid=(\d+)", home)
        if m:
            cid = m.group(1)
        print(f"    conversa Seam (cid): {cid}")

        # A "página principal" pelo actionMethod do próprio menu.
        alvos = [
            ("[3] paginaPrincipal   ",
             f"/ser/home?actionMethod=home.xhtml%3AmenuAction.navegarParaPaginaPrincipal"
             f"{'&cid=' + cid if cid else ''}"),
            ("[4] home.seam de novo ", "/ser/home.seam"),
        ]
        for rotulo, url in alvos:
            resp = c.get(url)
            diagnostico(rotulo, resp.text, cap,
                        rotulo.strip().split()[-1].replace("/", "_") + ".html")

        # O que a última página oferece de navegação — é aí que o módulo deve aparecer.
        ultima = c.get("/ser/home.seam").text
        doc = BeautifulSoup(ultima, "html.parser")
        print("\n" + "=" * 88)
        print("LINKS COM actionMethod (o padrão de ação do SER novo)")
        print("=" * 88)
        vistos = set()
        for a in doc.find_all("a", href=True):
            href = a["href"]
            if "actionMethod" in href and href not in vistos:
                vistos.add(href)
                print(f"  {a.get_text(strip=True)[:34]!r:36} {href[:110]}")

        print("\n" + "=" * 88)
        print("QUALQUER 'modulo' NO HTML (fora do CSS)")
        print("=" * 88)
        for m in re.finditer(r'<[^>]{0,200}[Mm]odulo[^>]{0,200}>', ultima):
            t = " ".join(m.group(0).split())
            if "style" in t[:20]:
                continue
            print("  ", t[:220])

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
