"""O que a home do SER declara HOJE sobre a escolha de módulo?

A sonda anterior provou que o POST do goModulo volta como Ajax-Response vazio: a ação é aceita e
não faz nada. Antes de adivinhar o motivo, ler a fonte primária — o HTML e o JS que a própria home
serve — como manda a regra deste laboratório.

Mostra: o form de módulo, os links/botões de módulo com seus ids, e as chamadas A4J declaradas no
JS (que é onde o nome e o valor do parâmetro aparecem de verdade).

Uso:  python sonda_modulo_home.py
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


def main() -> int:
    usuario, senha = os.environ.get("SER_USUARIO", ""), os.environ.get("SER_SENHA", "")
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
        cap = RAIZ / "capturas"; cap.mkdir(exist_ok=True)
        (cap / "sonda_home.html").write_text(home, encoding="utf-8")
        print(f"[2] home salva ({len(home)} bytes) em capturas/sonda_home.html\n")

        doc = BeautifulSoup(home, "html.parser")

        print("=" * 90)
        print("FORMS DA HOME")
        print("=" * 90)
        for f in doc.find_all("form"):
            print(f"  id={f.get('id')!r:14} action={f.get('action')!r}")
            vs = f.find("input", attrs={"name": "javax.faces.ViewState"})
            print(f"     ViewState no form: {vs.get('value') if vs else '(NENHUM)'}")

        print("\n" + "=" * 90)
        print("VIEWSTATE EM QUALQUER LUGAR DA PÁGINA")
        print("=" * 90)
        for m in re.finditer(r'name="javax\.faces\.ViewState"[^>]*value="([^"]*)"', home):
            print("  ", m.group(1))

        print("\n" + "=" * 90)
        print("OCORRÊNCIAS DE goModulo (HTML e JS) — a fonte primária")
        print("=" * 90)
        for m in re.finditer(r".{280}goModulo.{420}", home, re.S):
            print("  ..." + " ".join(m.group(0).split()) + "...\n")

        print("=" * 90)
        print("PARAM1 / NOMES DE MÓDULO CITADOS")
        print("=" * 90)
        for m in re.finditer(r"param1[^,;\)]{0,90}", home):
            print("  ", " ".join(m.group(0).split()))
        for m in re.finditer(r"['\"](ambulatorial|AMBULATORIAL|ambulatorio|hospitalar)['\"]", home):
            print("   valor citado:", m.group(1))

        print("\n" + "=" * 90)
        print("LINKS/BOTÕES COM TEXTO DE MÓDULO")
        print("=" * 90)
        for a in doc.find_all(["a", "input", "button"]):
            txt = (a.get_text(strip=True) if a.name != "input" else (a.get("value") or "")) or ""
            if any(p in txt.lower() for p in ("ambulat", "hospital", "módulo", "modulo", "entrar")):
                print(f"  <{a.name}> id={a.get('id')!r} onclick={(a.get('onclick') or '')[:150]!r} texto={txt[:40]!r}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
